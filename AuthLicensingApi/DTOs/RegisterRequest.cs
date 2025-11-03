namespace AuthLicensingApi.DTOs;

public record RegisterRequest(string Username, string Password, string? LicenseKey);
