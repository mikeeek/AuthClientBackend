using MongoDB.Bson.Serialization.Attributes;

namespace AuthLicensingApi.Models;

public class Subscription
{
    [BsonElement("level")] public string Level { get; set; } = default!;
    [BsonElement("expiresAt")] public DateTime ExpiresAt { get; set; }
}
