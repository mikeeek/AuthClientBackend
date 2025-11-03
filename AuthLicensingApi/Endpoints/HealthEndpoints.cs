using Microsoft.OpenApi.Models;

namespace AuthLicensingApi.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            status = "ok",
            service = "AuthLicensing API",
            version = "1.0.0",
            timeUtc = DateTime.UtcNow
        }))
        .WithTags("Meta")
        .WithOpenApi(op =>
        {
            op.Summary = "API readiness";
            op.Description = "Simple test health check endpoint.";
            return op;
        });
    }
}
