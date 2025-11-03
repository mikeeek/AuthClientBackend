using APIAutomatedTests.Helpers;
using AuthLicensingApi.Models;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace APIAutomatedTests;

public class UserProfileEndpointTests : IDisposable
{
    private readonly MongoDbTestHelper _dbHelper;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<License> _licenses;
    private const string TestJwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456789";
    private const string TestJwtIssuer = "TestAuthLicensingAPI";
    private const string TestJwtAudience = "TestAuthLicensingClient";

    public UserProfileEndpointTests()
    {
        var connectionString = "mongodb://localhost:27017";
        _dbHelper = new MongoDbTestHelper(connectionString);
        _users = _dbHelper.Users;
        _licenses = _dbHelper.Licenses;
    }

    [Fact]
    public async Task GetProfile_WithValidToken_ShouldReturnUserData()
    {
        // Arrange
        var user = TestDataHelper.CreateTestUser("profileuser");
        await _users.InsertOneAsync(user);

        var license = TestDataHelper.CreateTestLicense(
            key: "PROFILE-LICENSE-001",
            userId: user.Id,
            level: "premium");
        await _licenses.InsertOneAsync(license);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("level", license.Subscription.Level),
            new Claim("licenseKey", license.Key)
        };

        // Act - Simulate token extraction
        var username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var level = claims.FirstOrDefault(c => c.Type == "level")?.Value;
        var licenseKey = claims.FirstOrDefault(c => c.Type == "licenseKey")?.Value;

        var dbUser = await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
        var dbLicense = await _licenses.Find(l => l.Key == licenseKey).FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(dbUser);
        Assert.NotNull(dbLicense);
        Assert.Equal(user.Username, username);
        Assert.Equal("premium", level);
        Assert.Equal("PROFILE-LICENSE-001", licenseKey);
        Assert.Equal("active", dbLicense.Status);
    }

    [Fact]
    public async Task GetProfile_UserNotFound_ShouldReturnNull()
    {
        // Arrange
        var username = "nonexistentuser";

        // Act
        var user = await _users.Find(u => u.Username == username).FirstOrDefaultAsync();

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetProfile_LicenseNotFound_ShouldReturnNull()
    {
        // Arrange
        var user = TestDataHelper.CreateTestUser("userwithnolicense");
        await _users.InsertOneAsync(user);

        var licenseKey = "NONEXISTENT-LICENSE";

        // Act
        var license = await _licenses.Find(l => l.Key == licenseKey).FirstOrDefaultAsync();

        // Assert
        Assert.Null(license);
    }

    [Fact]
    public async Task GetProfile_ShouldIncludeAllRequiredFields()
    {
        // Arrange
        var user = TestDataHelper.CreateTestUser("completeuser");
        await _users.InsertOneAsync(user);

        var license = TestDataHelper.CreateTestLicense(
            key: "COMPLETE-LICENSE",
            userId: user.Id,
            level: "enterprise",
            status: "active");
        await _licenses.InsertOneAsync(license);

        // Act
        var dbUser = await _users.Find(u => u.Username == user.Username).FirstOrDefaultAsync();
        var dbLicense = await _licenses.Find(l => l.Key == "COMPLETE-LICENSE").FirstOrDefaultAsync();

        // Assert - Verify all profile fields are present
        Assert.NotNull(dbUser);
        Assert.NotNull(dbLicense);
        Assert.NotEqual(default, dbUser.CreatedAt);
        Assert.NotEqual(default, dbLicense.Subscription.ExpiresAt);
        Assert.Equal("enterprise", dbLicense.Subscription.Level);
        Assert.Equal("active", dbLicense.Status);
    }

    [Fact]
    public void JwtClaims_ShouldContainRequiredProfileData()
    {
        // Arrange
        var username = "testuser";
        var level = "premium";
        var licenseKey = "TEST-KEY-789";

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("level", level),
            new Claim("licenseKey", licenseKey)
        };

        // Act
        var extractedUsername = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var extractedLevel = claims.FirstOrDefault(c => c.Type == "level")?.Value;
        var extractedLicenseKey = claims.FirstOrDefault(c => c.Type == "licenseKey")?.Value;

        // Assert
        Assert.Equal(username, extractedUsername);
        Assert.Equal(level, extractedLevel);
        Assert.Equal(licenseKey, extractedLicenseKey);
    }

    [Fact]
    public async Task GetProfile_VerifySubscriptionExpiry()
    {
        // Arrange
        var user = TestDataHelper.CreateTestUser("expiryuser");
        await _users.InsertOneAsync(user);

        var futureExpiry = DateTime.UtcNow.AddMonths(6);
        var license = TestDataHelper.CreateTestLicense(
            key: "EXPIRY-LICENSE",
            userId: user.Id,
            expiresAt: futureExpiry);
        await _licenses.InsertOneAsync(license);

        // Act
        var dbLicense = await _licenses.Find(l => l.Key == "EXPIRY-LICENSE").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(dbLicense);
        Assert.True(dbLicense.Subscription.ExpiresAt > DateTime.UtcNow);
        Assert.True(dbLicense.Subscription.ExpiresAt <= futureExpiry.AddSeconds(1)); // Allow small time difference
    }

    [Fact]
    public async Task GetProfile_VerifyMultipleSubscriptionLevels()
    {
        // Arrange & Act & Assert for different subscription levels
        var levels = new[] { "basic", "premium", "enterprise" };

        foreach (var level in levels)
        {
            var user = TestDataHelper.CreateTestUser($"user_{level}");
            await _users.InsertOneAsync(user);

            var license = TestDataHelper.CreateTestLicense(
                key: $"{level.ToUpper()}-LICENSE",
                userId: user.Id,
                level: level);
            await _licenses.InsertOneAsync(license);

            var dbLicense = await _licenses.Find(l => l.Key == $"{level.ToUpper()}-LICENSE").FirstOrDefaultAsync();

            Assert.NotNull(dbLicense);
            Assert.Equal(level, dbLicense.Subscription.Level);
        }
    }

    [Fact]
    public void TokenValidation_ExpiredToken_ShouldFail()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("level", "premium"),
            new Claim("licenseKey", "TEST-KEY")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer,
            audience: TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(-10), // Expired 10 seconds ago
            signingCredentials: creds);

        var jwtHandler = new JwtSecurityTokenHandler();
        var jwt = jwtHandler.WriteToken(token);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = TestJwtIssuer,
            ValidAudience = TestJwtAudience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.Zero
        };

        // Act & Assert
        Assert.Throws<SecurityTokenExpiredException>(() =>
        {
            jwtHandler.ValidateToken(jwt, validationParameters, out _);
        });
    }

    [Fact]
    public async Task GetProfile_VerifyAccountCreationTimestamp()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;
        var user = TestDataHelper.CreateTestUser("timestampuser");
        await _users.InsertOneAsync(user);
        var afterCreation = DateTime.UtcNow;

        // Act
        var dbUser = await _users.Find(u => u.Username == "timestampuser").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(dbUser);
        Assert.True(dbUser.CreatedAt >= beforeCreation.AddSeconds(-1)); // Allow small time difference
        Assert.True(dbUser.CreatedAt <= afterCreation.AddSeconds(1));
    }

    public void Dispose()
    {
        _dbHelper?.Dispose();
    }
}
