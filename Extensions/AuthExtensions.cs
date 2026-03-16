using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using pqy_server.Shared;
using System.Text;
using System.Text.Json;

namespace pqy_server.Extensions
{
    public static class AuthExtensions
    {
        public static IServiceCollection AddAppAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("JwtSettings");
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? jwtSettings["Key"];

            if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
                throw new Exception("JWT secret missing or insecure!");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings["Issuer"],
                        ValidAudience = jwtSettings["Audience"],
                        IssuerSigningKey =
                            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        // SignalR WebSocket cannot send headers, so it passes the token via ?access_token=
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken) &&
                                context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/json";

                            var response = ApiResponse<string>.Failure(
                                ResultCode.TokenExpired,
                                "Access token expired or invalid");

                            return context.Response.WriteAsync(
                                JsonSerializer.Serialize(response));
                        }
                    };
                });

            return services;
        }
    }
}
