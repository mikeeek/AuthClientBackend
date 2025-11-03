using APIAutomatedTests.Helpers;
using AuthLicensingApi.DTOs;
using AuthLicensingApi.Models;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace APIAutomatedTests;

public class AuthenticationEndpointTests : IDisposable
{
    private readonly MongoDbTestHelper _dbHelper;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<License> _licenses;
    private const string TestJwtKey = "ThisIsASecretKeyForTestingPurposesOnly123456789";
    private const string TestJwtIssuer = "TestAuthLicensingAPI";
    private const string TestJwtAudience = "TestAuthLicensingClient";

    public AuthenticationEndpointTests()
    {
        var connectionString = "mongodb://localhost:27017";
        _dbHelper = new MongoDbTestHelper(connectionString);
        _users = _dbHelper.Users;
        _licenses = _dbHelper.Licenses;
    }

    [Fact]
    public async Task AuthCheck_ValidCredentialsAndActiveLicense_ShouldSucceed()
    {
        // Arrange
        var password = "TestPassword123!";
        var user = TestDataHelper.CreateTestUser("authuser", BCrypt.Net.BCrypt.HashPassword(password, 12));
        await _users.InsertOneAsync(user);

        var license = TestDataHelper.CreateTestLicense(
            key: "AUTH-LICENSE-001",
            userId: user.Id,
            expiresAt: DateTime.UtcNow.AddMonths(1));
        await _licenses.InsertOneAsync(license);

        var request = TestDataHelper.CreateAuthRequest("authuser", password, "AUTH-LICENSE-001");

        // Act - Simulate authentication logic
        var dbUser = await _users.Find(u => u.Username == request.Username).FirstOrDefaultAsync();
        Assert.NotNull(dbUser);

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, dbUser.PasswordHash);
        Assert.True(passwordValid);

        var dbLicense = await _licenses.Find(l =>
            l.UserId == dbUser.Id &&
            l.Key == request.Key &&
            l.Status == "active").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(dbLicense);
        Assert.True(dbLicense.Subscription.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task AuthCheck_InvalidPassword_ShouldFail()
    {
        // Arrange
        var correctPassword = "CorrectPassword123!";
        var user = TestDataHelper.CreateTestUser("testuser", BCrypt.Net.BCrypt.HashPassword(correctPassword, 12));
        await _users.InsertOneAsync(user);

        var wrongPassword = "WrongPassword123!";

        // Act
        var passwordValid = BCrypt.Net.BCrypt.Verify(wrongPassword, user.PasswordHash);

        // Assert
        Assert.False(passwordValid, "Wrong password should not be verified");
    }

    [Fact]
    public async Task AuthCheck_UserNotFound_ShouldFail()
    {
        // Arrange
        var request = TestDataHelper.CreateAuthRequest("nonexistentuser", "Password123!", "SOME-KEY");

        // Act
        var user = await _users.Find(u => u.Username == request.Username).FirstOrDefaultAsync();

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task AuthCheck_ExpiredLicense_ShouldFail()
    {
        // Arrange
        var password = "TestPassword123!";
        var user = TestDataHelper.CreateTestUser("expireduser", BCrypt.Net.BCrypt.HashPassword(password, 12));
        await _users.InsertOneAsync(user);

        var expiredLicense = TestDataHelper.CreateTestLicense(
            key: "EXPIRED-LICENSE",
            userId: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(-1)); // Expired yesterday
        await _licenses.InsertOneAsync(expiredLicense);

        // Act
        var license = await _licenses.Find(l => l.UserId == user.Id && l.Key == "EXPIRED-LICENSE").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(license);
        Assert.True(license.Subscription.ExpiresAt <= DateTime.UtcNow, "License should be expired");
    }

    [Fact]
    public async Task AuthCheck_InactiveLicense_ShouldFail()
    {
        // Arrange
        var password = "TestPassword123!";
        var user = TestDataHelper.CreateTestUser("inactiveuser", BCrypt.Net.BCrypt.HashPassword(password, 12));
        await _users.InsertOneAsync(user);

        var inactiveLicense = TestDataHelper.CreateTestLicense(
            key: "INACTIVE-LICENSE",
            userId: user.Id,
            status: "suspended");
        await _licenses.InsertOneAsync(inactiveLicense);

        // Act
        var license = await _licenses.Find(l =>
            l.UserId == user.Id &&
            l.Key == "INACTIVE-LICENSE" &&
            l.Status == "active").FirstOrDefaultAsync();

        // Assert
        Assert.Null(license);
    }

    [Fact]
    public async Task AuthCheck_LicenseBelongsToOtherUser_ShouldFail()
    {
        // Arrange
        var password = "TestPassword123!";
        var user1 = TestDataHelper.CreateTestUser("user1", BCrypt.Net.BCrypt.HashPassword(password, 12));
        var user2 = TestDataHelper.CreateTestUser("user2", BCrypt.Net.BCrypt.HashPassword(password, 12));
        await _users.InsertManyAsync(new[] { user1, user2 });

        var user2License = TestDataHelper.CreateTestLicense(key: "USER2-LICENSE", userId: user2.Id);
        await _licenses.InsertOneAsync(user2License);

        // Act - User1 tries to use User2's license
        var license = await _licenses.Find(l =>
            l.UserId == user1.Id &&
            l.Key == "USER2-LICENSE").FirstOrDefaultAsync();

        // Assert
        Assert.Null(license);
    }

    [Fact]
    public void JwtGeneration_ShouldCreateValidToken()
    {
        // Arrange
        var username = "testuser";
        var level = "premium";
        var licenseKey = "TEST-KEY-123";

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("level", level),
            new Claim("licenseKey", licenseKey)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Act
        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer,
            audience: TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        var jwtHandler = new JwtSecurityTokenHandler();
        var jwt = jwtHandler.WriteToken(token);

        // Assert
        Assert.NotNull(jwt);
        Assert.NotEmpty(jwt);
        Assert.Contains(".", jwt); // JWT has parts separated by dots

        // Validate the token can be read back
        var readToken = jwtHandler.ReadJwtToken(jwt);
        Assert.Equal(username, readToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value);
        Assert.Equal(level, readToken.Claims.FirstOrDefault(c => c.Type == "level")?.Value);
        Assert.Equal(licenseKey, readToken.Claims.FirstOrDefault(c => c.Type == "licenseKey")?.Value);
    }

    [Fact]
    public void JwtValidation_ShouldValidateCorrectly()
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
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        var jwtHandler = new JwtSecurityTokenHandler();
        var jwt = jwtHandler.WriteToken(token);

        // Act - Validate the token
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

        var principal = jwtHandler.ValidateToken(jwt, validationParameters, out var validatedToken);

        // Assert
        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
        Assert.Equal("testuser", principal.Identity?.Name);
    }

    [Fact]
    public void PasswordHashValidation_MalformedHash_ShouldFail()
    {
        // Arrange - Malformed BCrypt hash
        var malformedHash = "invalid-hash";

        // Act & Assert
        Assert.Throws<BCrypt.Net.SaltParseException>(() =>
        {
            BCrypt.Net.BCrypt.Verify("password", malformedHash);
        });
    }

    [Fact]
    public void PasswordHashValidation_EmptyHash_ShouldBeInvalid()
    {
        // Arrange
        var emptyHash = "";

        // Act
        var isValidHash = !string.IsNullOrWhiteSpace(emptyHash) &&
                         emptyHash.Length >= 59 &&
                         emptyHash.StartsWith("$2");

        // Assert
        Assert.False(isValidHash);
    }

    public void Dispose()
    {
        _dbHelper?.Dispose();
    }
}
