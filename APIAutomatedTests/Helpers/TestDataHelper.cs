using AuthLicensingApi.DTOs;
using AuthLicensingApi.Models;
using MongoDB.Bson;

namespace APIAutomatedTests.Helpers;

public static class TestDataHelper
{
    public static User CreateTestUser(string username = "testuser", string? passwordHash = null)
    {
        return new User
        {
            Id = ObjectId.GenerateNewId(),
            Username = username,
            PasswordHash = passwordHash ?? BCrypt.Net.BCrypt.HashPassword("TestPassword123", 12),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static License CreateTestLicense(
        string key = "TEST-LICENSE-KEY",
        ObjectId? userId = null,
        string level = "premium",
        DateTime? expiresAt = null,
        string status = "active")
    {
        return new License
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId ?? ObjectId.Empty,
            Key = key,
            Status = status,
            IssuedAt = DateTime.UtcNow,
            Subscription = new Subscription
            {
                Level = level,
                ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMonths(1)
            }
        };
    }

    public static RegisterRequest CreateRegisterRequest(
        string username = "newuser",
        string password = "Password123",
        string? licenseKey = null)
    {
        return new RegisterRequest(username, password, licenseKey);
    }

    public static AuthRequest CreateAuthRequest(
        string username = "testuser",
        string password = "TestPassword123",
        string key = "TEST-LICENSE-KEY")
    {
        return new AuthRequest(username, password, key);
    }
}
