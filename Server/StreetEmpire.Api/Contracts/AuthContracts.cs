namespace StreetEmpire.Api.Contracts;

/// <param name="City">The town to set up in. Ground is contested inside a town, so this is the map
/// the player will actually be playing on. Omitted falls back to the first configured city.</param>
public sealed record RegisterRequest(string? Username, string? Password, string? PlayerName, string? City = null);
public sealed record LoginRequest(string? Username, string? Password);
public sealed record AuthResponse(Guid PlayerId, string PlayerName, string Username);
