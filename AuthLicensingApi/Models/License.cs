using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthLicensingApi.Models;

public class License
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("userId")] public ObjectId UserId { get; set; }
    [BsonElement("key")] public string Key { get; set; } = default!;
    [BsonElement("subscription")] public Subscription Subscription { get; set; } = default!;
    [BsonElement("issuedAt")] public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("status")] public string Status { get; set; } = "active";
}
