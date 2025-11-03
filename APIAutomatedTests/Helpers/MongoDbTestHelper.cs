using AuthLicensingApi.Models;
using MongoDB.Driver;

namespace APIAutomatedTests.Helpers;

public class MongoDbTestHelper : IDisposable
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly string _databaseName;

    public IMongoCollection<User> Users { get; }
    public IMongoCollection<License> Licenses { get; }

    public MongoDbTestHelper(string connectionString)
    {
        _client = new MongoClient(connectionString);
        _databaseName = $"test_auth_db_{Guid.NewGuid():N}";
        _database = _client.GetDatabase(_databaseName);

        Users = _database.GetCollection<User>("users");
        Licenses = _database.GetCollection<License>("licenses");
    }

    public async Task CleanupAsync()
    {
        await _client.DropDatabaseAsync(_databaseName);
    }

    public void Dispose()
    {
        CleanupAsync().GetAwaiter().GetResult();
    }
}
