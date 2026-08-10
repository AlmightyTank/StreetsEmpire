namespace StreetEmpire.Api.Contracts;

public sealed record RegisterRequest(string? Username, string? Password, string? PlayerName);
public sealed record LoginRequest(string? Username, string? Password);
public sealed record AuthResponse(Guid PlayerId, string PlayerName, string Username);
