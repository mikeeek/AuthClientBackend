using AuthLicensingApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

namespace AuthLicensingApi.Endpoints;

public static class UserProfileEndpoints
{
    public static void MapUserProfileEndpoints(
        this WebApplication app,
        IMongoCollection<User> users,
        IMongoCollection<License> licenses)
    {
        app.MapGet("/me", [Authorize] async (HttpContext ctx) =>
        {
            var username = ctx.User.Identity?.Name ?? "(unknown)";
            var level = ctx.User.Claims.FirstOrDefault(c => c.Type == "level")?.Value ?? "(none)";
            var licenseKey = ctx.User.Claims.FirstOrDefault(c => c.Type == "licenseKey")?.Value ?? "(none)";

            var user = await users.Find(u => u.Username == username).FirstOrDefaultAsync();
            var license = await licenses.Find(l => l.Key == licenseKey).FirstOrDefaultAsync();

            if (user is null || license is null)
                return Results.NotFound("User or license not found.");

            return Results.Ok(new
            {
                username,
                level,
                licenseKey,
                licenseStatus = license.Status,
                subscriptionExpiresAt = license.Subscription.ExpiresAt,
                accountCreatedAt = user.CreatedAt
            });
        })
        .RequireRateLimiting("authPolicy") // remove if you prefer unlimited reads
        .WithTags("User Profile")
        .WithOpenApi(op =>
        {
            op.Summary = "Get authenticated user's profile and license info";
            op.Description = "Requires a valid Bearer token in the Authorization header.";
            op.Responses["200"] = new OpenApiResponse { Description = "Profile returned." };
            op.Responses["401"] = new OpenApiResponse { Description = "Missing/invalid JWT." };
            op.Responses["404"] = new OpenApiResponse { Description = "User or license not found." };
            op.Responses["429"] = new OpenApiResponse { Description = "Too many requests (rate limit hit)." };
            return op;
        });
    }
}
