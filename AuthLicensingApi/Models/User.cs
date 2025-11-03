using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthLicensingApi.Models;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("username")] public string Username { get; set; } = default!;
    [BsonElement("passwordHash")] public string PasswordHash { get; set; } = default!;
    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
