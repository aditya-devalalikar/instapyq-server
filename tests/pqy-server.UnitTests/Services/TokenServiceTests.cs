using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using pqy_server.Data;
using pqy_server.Models.Users;
using Xunit;

namespace pqy_server.UnitTests.Services;

public class TokenServiceTests
{
    private static TokenService CreateService(string dbName)
    {
        // Arrange: fake configuration
        var configData = new Dictionary<string, string>
        {
            { "JwtSettings:Key", "THIS_IS_A_TEST_SECRET_KEY_1234567890123456" },
            { "JwtSettings:Issuer", "test-issuer" },
            { "JwtSettings:Audience", "test-audience" },
            { "JwtSettings:ExpiresInMinutes", "60" }
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Arrange: In-memory DbContext
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new AppDbContext(options);

        return new TokenService(config, context);
    }

    // =====================================================
    // Refresh Token Tests
    // =====================================================

    [Fact]
    public void CreateRefreshToken_ShouldReturnNonEmptyToken()
    {
        // Arrange
        var service = CreateService(nameof(CreateRefreshToken_ShouldReturnNonEmptyToken));

        // Act
        var refreshToken = service.CreateRefreshToken();

        // Assert
        refreshToken.Should().NotBeNullOrWhiteSpace();
        refreshToken.Length.Should().BeGreaterThan(50);
    }

    // =====================================================
    // Access Token Tests (basic)
    // =====================================================

    [Fact]
    public void CreateAccessToken_ShouldReturnJwtToken()
    {
        // Arrange
        var service = CreateService(nameof(CreateAccessToken_ShouldReturnJwtToken));

        var user = new User
        {
            UserId = 1,
            Username = "testuser",
            UserEmail = "test@example.com",
            RoleId = 2 // User
        };

        // Act
        var accessToken = service.CreateAccessToken(user);

        // Assert
        accessToken.Should().NotBeNullOrWhiteSpace();
        accessToken.Split('.').Length.Should().Be(3); // JWT = header.payload.signature
    }
}
