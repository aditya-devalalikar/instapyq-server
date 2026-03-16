using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Helpers;
using pqy_server.Models.Auth;
using pqy_server.Models.Otp;
using pqy_server.Models.Users;
using pqy_server.Services.EmailService;
using pqy_server.Shared;
using Serilog;
using System.Security.Claims;
using System.Security.Cryptography;

namespace pqy_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;

        public AuthController(AppDbContext context, IConfiguration config, ITokenService tokenService, IEmailService emailService)
        {
            _context = context;
            _config = config;
            _tokenService = tokenService;
            _emailService = emailService;
        }

        // 📧 Send OTP

        // Admin login with email/password (separate from OTP/Google user auth)
        [AllowAnonymous]
        [HttpPost("admin-login")]
        public async Task<IActionResult> AdminLogin([FromBody] AdminLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.ValidationError,
                    "Email and password are required."));
            }

            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserEmail == email && !u.IsDeleted);

            if (user == null || user.PasswordHash == null || user.PasswordSalt == null)
            {
                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.Unauthorized,
                    "Invalid email or password."));
            }

            using var hmac = new HMACSHA512(user.PasswordSalt);
            var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(request.Password));
            var isPasswordValid = CryptographicOperations.FixedTimeEquals(computedHash, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.Unauthorized,
                    "Invalid email or password."));
            }

            var isAdmin = string.Equals(user.Role?.RoleName, RoleConstant.Admin, StringComparison.OrdinalIgnoreCase);

            if (!isAdmin)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<string>.Failure(ResultCode.Forbidden, "Admin access required."));
            }

            var accessToken = _tokenService.CreateAccessToken(user);
            var refreshToken = _tokenService.CreateRefreshToken();

            user.RefreshToken = TokenHelpers.ComputeSha256Hash(refreshToken);
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
                _config.GetValue<int>("JwtSettings:RefreshTokenExpiryDays", 7));

            await _context.SaveChangesAsync();

            Log.Information("Admin login ok. uid={uid}, eml={eml}", user.UserId, email);

            return Ok(ApiResponse<object>.Success(new
            {
                token = accessToken,
                refreshToken
            }, "Admin login successful."));
        }
        [AllowAnonymous]
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            // Rate limit: block if OTP was sent in the last 60 seconds
            var recentExists = await _context.EmailOtps
                .AnyAsync(o => o.Email == email && o.CreatedAt > DateTime.UtcNow.AddSeconds(-60));

            if (recentExists)
            {
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.ValidationError,
                    "Please wait before requesting another OTP."));
            }

            // Rate limit: max 5 OTP requests per hour per email
            var otpCountLastHour = await _context.EmailOtps
                .CountAsync(o => o.Email == email && o.CreatedAt > DateTime.UtcNow.AddHours(-1));

            if (otpCountLastHour >= 5)
            {
                Log.Warning("OTP hourly limit exceeded. eml={eml}", email);
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.ValidationError,
                    "Too many OTP requests. Please try again after an hour."));
            }

            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            _context.EmailOtps.Add(new EmailOtp
            {
                Email = email,
                Code = TokenHelpers.ComputeSha256Hash(code),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendOtpEmail(email, code);
                Log.Information("OTP sent. eml={eml}", email);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "OTP send failed. eml={eml}", email);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<string>.Failure(ResultCode.InternalServerError, "Failed to send OTP. Please try again."));
            }

            return Ok(ApiResponse<string>.Success("OTP sent to your email."));
        }

        // ✅ Verify OTP
        [AllowAnonymous]
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            // Find the most recent unused, unexpired OTP for this email
            var otp = await _context.EmailOtps
                .Where(o => o.Email == email && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                Log.Warning("OTP verify failed (no valid OTP). eml={eml}", email);
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid or expired OTP."));
            }

            // Lockout after 5 failed attempts on this OTP
            if (otp.FailedAttempts >= 5)
            {
                otp.IsUsed = true;
                await _context.SaveChangesAsync();
                Log.Warning("OTP locked out. eml={eml}", email);
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Too many failed attempts. Please request a new OTP."));
            }

            // Block if total failed OTP attempts across all OTPs in the last hour exceeds 10
            var totalFailedLastHour = await _context.EmailOtps
                .Where(o => o.Email == email && o.CreatedAt > DateTime.UtcNow.AddHours(-1))
                .SumAsync(o => (int?)o.FailedAttempts) ?? 0;

            if (totalFailedLastHour >= 10)
            {
                Log.Warning("OTP blocked — too many failures. eml={eml}", email);
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Too many failed attempts. Please try again after an hour."));
            }

            if (otp.Code != TokenHelpers.ComputeSha256Hash(request.Code))
            {
                otp.FailedAttempts++;
                await _context.SaveChangesAsync();
                Log.Warning("OTP wrong code (attempt {N}). eml={eml}", otp.FailedAttempts, email);
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid or expired OTP."));
            }

            otp.IsUsed = true;
            await _context.SaveChangesAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserEmail == email && !u.IsDeleted);
            bool isNewUser = user == null;

            if (isNewUser)
            {
                var username = email.Split('@')[0];
                user = new User
                {
                    Username = username,
                    UserEmail = email,
                    RoleId = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SelectedExamIds = await _context.Exams
                        .Where(e => !e.IsDeleted)
                        .Select(e => e.ExamId)
                        .ToListAsync()
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.SendWelcomeEmail(user.UserEmail!, user.Username!);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Welcome email failed. uid={uid}", user.UserId);
                }
            }

            var accessToken = _tokenService.CreateAccessToken(user!);
            var refreshToken = _tokenService.CreateRefreshToken();

            user!.RefreshToken = TokenHelpers.ComputeSha256Hash(refreshToken);
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
                _config.GetValue<int>("JwtSettings:RefreshTokenExpiryDays", 7));

            if (!string.IsNullOrEmpty(request.DeviceId))
                user.DeviceId = request.DeviceId;

            await _context.SaveChangesAsync();

            Log.Information("OTP login ok. uid={uid}, eml={eml}", user.UserId, email);

            return Ok(ApiResponse<object>.Success(new
            {
                token = accessToken,
                refreshToken,
                isNewUser
            }));
        }

        // 🔐 Google Login
        [AllowAnonymous]
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            GoogleJsonWebSignature.Payload payload;

            try
            {
                var googleClientId = _config["Google:WebClientId"];
                var validationSettings = !string.IsNullOrEmpty(googleClientId)
                    ? new GoogleJsonWebSignature.ValidationSettings { Audience = [googleClientId] }
                    : null;

                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, validationSettings);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Google token invalid. tok={tok}...", 
                    request.IdToken.Length > 10 ? request.IdToken.Substring(0, 10) : "too-short");

                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.Unauthorized,
                    "Invalid Google token"));
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.GoogleId == payload.Subject ||
                    u.UserEmail == payload.Email);

            bool isNewUser = false;

            if (user == null)
            {
                isNewUser = true;

                user = new User
                {
                    Username = payload.Name,
                    UserEmail = payload.Email,
                    GoogleId = payload.Subject,
                    GoogleProfilePicture = payload.Picture,
                    IsGoogleLoginOnly = true,
                    RoleId = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SelectedExamIds = await _context.Exams
                                        .Where(e => !e.IsDeleted)
                                        .Select(e => e.ExamId)
                                        .ToListAsync()
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                Log.Information("New Google user. uid={uid}", user.UserId);
            }

            var accessToken = _tokenService.CreateAccessToken(user);
            var refreshToken = _tokenService.CreateRefreshToken();

            user.RefreshToken = TokenHelpers.ComputeSha256Hash(refreshToken);
            user.RefreshTokenExpiryTime =
                DateTime.UtcNow.AddDays(
                    _config.GetValue<int>("JwtSettings:RefreshTokenExpiryDays", 7));

            if (!string.IsNullOrEmpty(request.DeviceId))
                user.DeviceId = request.DeviceId;

            await _context.SaveChangesAsync();

            if (isNewUser)
            {
                try
                {
                    await _emailService.SendWelcomeEmail(user.UserEmail!, user.Username!);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Welcome email failed. uid={uid}", user.UserId);
                }
            }
            // Remove "Google login ok" to save space

            return Ok(ApiResponse<object>.Success(new
            {
                token = accessToken,
                refreshToken,
                isNewUser
            }));
        }

        // 🔁 Refresh Token
        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.RefreshTokenInvalid,
                    "Refresh token missing"));
            }

            var refreshTokenHash = TokenHelpers.ComputeSha256Hash(request.RefreshToken);

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == refreshTokenHash &&
                u.RefreshTokenExpiryTime > DateTime.UtcNow);

            if (user == null)
            {
                Log.Warning("Refresh token failed. rth={rth}", refreshTokenHash);

                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.RefreshTokenExpired,
                    "Refresh token expired or invalid"));
            }

            // Single-device enforcement: reject if DeviceId doesn't match
            if (!string.IsNullOrEmpty(request.DeviceId)
                && !string.IsNullOrEmpty(user.DeviceId)
                && user.DeviceId != request.DeviceId)
            {
                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.DeviceMismatch,
                    "Your account has been signed in on another device."));
            }

            // Register device on first refresh for users who logged in before this feature
            if (!string.IsNullOrEmpty(request.DeviceId) && string.IsNullOrEmpty(user.DeviceId))
                user.DeviceId = request.DeviceId;

            var newAccessToken = _tokenService.CreateAccessToken(user);
            var newRefreshToken = _tokenService.CreateRefreshToken();

            user.RefreshToken = TokenHelpers.ComputeSha256Hash(newRefreshToken);
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
                _config.GetValue<int>("JwtSettings:RefreshTokenExpiryDays", 7));

            await _context.SaveChangesAsync();

            Log.Information("Refresh token ok. uid={uid}", user.UserId);

            return Ok(ApiResponse<object>.Success(new
            {
                token = newAccessToken,
                refreshToken = newRefreshToken
            }));
        }

        // 🚪 Logout
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                Log.Warning("Logout failed. No uid in token.");
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User ID not found in token."));
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                Log.Warning("Logout failed. User not found. uid={uid}", userId);
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));
            }

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            await _context.SaveChangesAsync();

            Log.Information("Logout ok. uid={uid}", user.UserId);

            return Ok(ApiResponse<string>.Success("Logout successful."));
        }

        // 🔐 Admin-only test endpoint
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpGet("admin-only")]
        public IActionResult AdminOnlyEndpoint()
        {
            Log.Information("Admin endpoint accessed. uid={uid}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return Ok(ApiResponse<string>.Success("Welcome, Admin!"));
        }
    }
}

