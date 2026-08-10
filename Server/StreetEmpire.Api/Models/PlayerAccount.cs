namespace StreetEmpire.Api.Models;

public sealed class PlayerAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsBot { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Player? Player { get; set; }
}
