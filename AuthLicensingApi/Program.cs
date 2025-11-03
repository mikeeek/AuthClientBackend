using AuthLicensingApi.Converters;
using AuthLicensingApi.Data;
using AuthLicensingApi.Endpoints;
using AuthLicensingApi.Extensions;
using AuthLicensingApi.Middleware;
using AuthLicensingApi.Models;
using MongoDB.Driver;
using Serilog;

// Configure Serilog
LoggingExtensions.ConfigureSerilog();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Configure JSON serialization for MongoDB ObjectId
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new ObjectIdJsonConverter());
});

// Read config (appsettings.json is auto-loaded)
var mongoConn = builder.Configuration["Mongo:ConnectionString"]!;
var mongoDbName = builder.Configuration["Mongo:Database"]!;
var bcryptWorkFactor = int.Parse(builder.Configuration["Auth:BcryptWorkFactor"] ?? "12");

// MongoDB setup
var client = new MongoClient(mongoConn);
var db = client.GetDatabase(mongoDbName);
var users = db.GetCollection<User>("users");
var licenses = db.GetCollection<License>("licenses");

// JWT configuration
var jwtKey = builder.Configuration["Auth:JwtKey"]!;
var jwtIssuer = builder.Configuration["Auth:JwtIssuer"]!;
var jwtAudience = builder.Configuration["Auth:JwtAudience"]!;

// Add services using extension methods
builder.Services.AddJwtAuthentication(jwtKey, jwtIssuer, jwtAudience);
builder.Services.AddAuthRateLimiting();
builder.Services.AddSwaggerDocumentation();

// Initialize database (indexes)
await DatabaseInitializer.InitializeDatabaseAsync(db, users, licenses);

var app = builder.Build();

// Middleware pipeline
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Custom middleware
app.UseMiddleware<CorrelationIdMiddleware>();

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthLicensing API v1 - Mikko Salovaara");
    options.DocumentTitle = "AuthLicensing API Docs";
    options.RoutePrefix = string.Empty; // Swagger UI at http://localhost:5080/
});

// Map endpoints
app.MapHealthEndpoints();
app.MapAuthenticationEndpoints(users, licenses, bcryptWorkFactor, jwtKey, jwtIssuer, jwtAudience);
app.MapUserProfileEndpoints(users, licenses);

app.Run();
