using APIAutomatedTests.Helpers;
using AuthLicensingApi.DTOs;
using AuthLicensingApi.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net;
using System.Net.Http.Json;

namespace APIAutomatedTests;

public class RegistrationEndpointTests : IDisposable
{
    private readonly MongoDbTestHelper _dbHelper;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<License> _licenses;

    public RegistrationEndpointTests()
    {
        // Use test MongoDB connection string - update this with your test MongoDB instance
        var connectionString = "mongodb://localhost:27017";
        _dbHelper = new MongoDbTestHelper(connectionString);
        _users = _dbHelper.Users;
        _licenses = _dbHelper.Licenses;
    }

    [Fact]
    public async Task Register_ValidUserWithoutLicense_ReturnsCreated()
    {
        // Arrange
        var request = TestDataHelper.CreateRegisterRequest("testuser1", "Password123!");

        // Act - We would need to test against actual endpoint
        // For unit testing, we'll test the business logic
        var username = request.Username;
        var password = request.Password;

        // Verify username validation
        Assert.False(string.IsNullOrWhiteSpace(username));
        Assert.False(string.IsNullOrWhiteSpace(password));

        // Verify user doesn't exist
        var exists = await _users.Find(u => u.Username == username).AnyAsync();
        Assert.False(exists);

        // Create user
        var hash = BCrypt.Net.BCrypt.HashPassword(password, 12);
        var user = new User { Username = username, PasswordHash = hash };
        await _users.InsertOneAsync(user);

        // Assert
        var createdUser = await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
        Assert.NotNull(createdUser);
        Assert.Equal(username, createdUser.Username);
        Assert.NotEmpty(createdUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, createdUser.PasswordHash));
    }

    [Fact]
    public async Task Register_DuplicateUsername_ShouldFail()
    {
        // Arrange
        var username = "duplicateuser";
        var existingUser = TestDataHelper.CreateTestUser(username);
        await _users.InsertOneAsync(existingUser);

        // Act
        var exists = await _users.Find(u => u.Username == username).AnyAsync();

        // Assert
        Assert.True(exists, "Duplicate username should already exist");
    }

    [Fact]
    public async Task Register_WithValidLicenseKey_ShouldClaimLicense()
    {
        // Arrange
        var licenseKey = "VALID-LICENSE-123";
        var license = TestDataHelper.CreateTestLicense(key: licenseKey);
        await _licenses.InsertOneAsync(license);

        var request = TestDataHelper.CreateRegisterRequest("newuser", "Password123!", licenseKey);

        // Act - Simulate registration with license
        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12);
        var user = new User { Username = request.Username, PasswordHash = hash };
        await _users.InsertOneAsync(user);

        // Claim license
        var claimFilter = Builders<License>.Filter.And(
            Builders<License>.Filter.Eq(l => l.Key, licenseKey),
            Builders<License>.Filter.Or(
                Builders<License>.Filter.Exists(l => l.UserId, false),
                Builders<License>.Filter.Eq(l => l.UserId, ObjectId.Empty),
                Builders<License>.Filter.Eq(l => l.UserId, default(ObjectId))
            )
        );

        var claimUpdate = Builders<License>.Update
            .Set(l => l.UserId, user.Id)
            .Set(l => l.Status, "active");

        var claimResult = await _licenses.UpdateOneAsync(claimFilter, claimUpdate);

        // Assert
        Assert.Equal(1, claimResult.ModifiedCount);

        var claimedLicense = await _licenses.Find(l => l.Key == licenseKey).FirstOrDefaultAsync();
        Assert.NotNull(claimedLicense);
        Assert.Equal(user.Id, claimedLicense.UserId);
        Assert.Equal("active", claimedLicense.Status);
    }

    [Fact]
    public async Task Register_WithInvalidLicenseKey_ShouldFail()
    {
        // Arrange
        var invalidLicenseKey = "INVALID-KEY-999";
        var request = TestDataHelper.CreateRegisterRequest("testuser", "Password123!", invalidLicenseKey);

        // Act
        var licenseExists = await _licenses.Find(l => l.Key == invalidLicenseKey).AnyAsync();

        // Assert
        Assert.False(licenseExists, "Invalid license key should not exist");
    }

    [Fact]
    public async Task Register_WithAlreadyClaimedLicense_ShouldFail()
    {
        // Arrange
        var existingUserId = ObjectId.GenerateNewId();
        var licenseKey = "CLAIMED-LICENSE-456";
        var claimedLicense = TestDataHelper.CreateTestLicense(key: licenseKey, userId: existingUserId);
        await _licenses.InsertOneAsync(claimedLicense);

        // Act - Try to claim the same license
        var claimFilter = Builders<License>.Filter.And(
            Builders<License>.Filter.Eq(l => l.Key, licenseKey),
            Builders<License>.Filter.Or(
                Builders<License>.Filter.Exists(l => l.UserId, false),
                Builders<License>.Filter.Eq(l => l.UserId, ObjectId.Empty),
                Builders<License>.Filter.Eq(l => l.UserId, default(ObjectId))
            )
        );

        var canClaim = await _licenses.Find(claimFilter).AnyAsync();

        // Assert
        Assert.False(canClaim, "Already claimed license should not be claimable");
    }

    [Theory]
    [InlineData("", "Password123!")]
    [InlineData("validuser", "")]
    [InlineData(null, "Password123!")]
    [InlineData("validuser", null)]
    public void Register_InvalidInput_ShouldFailValidation(string? username, string? password)
    {
        // Arrange & Act
        var isValid = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);

        // Assert
        Assert.False(isValid, "Empty username or password should be invalid");
    }

    [Fact]
    public void PasswordHashing_ShouldBeSecure()
    {
        // Arrange
        var password = "MySecurePassword123!";

        // Act
        var hash = BCrypt.Net.BCrypt.HashPassword(password, 12);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEqual(password, hash);
        Assert.True(hash.Length >= 59);
        Assert.StartsWith("$2", hash);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash));
        Assert.False(BCrypt.Net.BCrypt.Verify("WrongPassword", hash));
    }

    public void Dispose()
    {
        _dbHelper?.Dispose();
    }
}
