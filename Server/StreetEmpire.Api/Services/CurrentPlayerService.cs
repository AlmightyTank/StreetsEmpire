using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class CurrentPlayerService(GameDbContext db, IHttpContextAccessor accessor)
{
    public async Task<Player?> GetAsync(CancellationToken cancellationToken = default)
    {
        var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var accountId))
            return null;

        return await db.Players
            .Include(x => x.Account)
            .Include(x => x.Hideout)
            .Include(x => x.Crew)
            .SingleOrDefaultAsync(x => x.AccountId == accountId, cancellationToken);
    }
}
