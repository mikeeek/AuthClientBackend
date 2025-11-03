using AuthLicensingApi.Models;
using MongoDB.Driver;

namespace AuthLicensingApi.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        IMongoDatabase db,
        IMongoCollection<User> users,
        IMongoCollection<License> licenses)
    {
        // Indexes (idempotent)
        await users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Username),
            new CreateIndexOptions { Unique = true }));

        await licenses.Indexes.CreateOneAsync(new CreateIndexModel<License>(
            Builders<License>.IndexKeys.Ascending(l => l.Key),
            new CreateIndexOptions { Unique = true }));

        await licenses.Indexes.CreateOneAsync(new CreateIndexModel<License>(
            Builders<License>.IndexKeys.Ascending(l => l.UserId)));
    }
}
