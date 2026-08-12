using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Mapping;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Support;

/// <summary>Reads the single live-operations row, creating it if an older database lacks the seed.</summary>
internal static class LiveOpsStore
{

    /// <summary>
    /// The single live-ops row, created on demand so a database that predates the seed still works.
    /// </summary>
    internal static async Task<GameSetting> LiveOpsAsync(GameDbContext db, CancellationToken cancellationToken)
    {
        var settings = await db.GameSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (settings is not null)
            return settings;

        settings = new GameSetting { Id = 1 };
        db.GameSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    internal static LiveOpsResponse ToLiveOpsResponse(GameSetting settings)
        => new(
            settings.MaintenanceMode,
            settings.MaintenanceMessage,
            settings.Announcement,
            settings.UpdatedAtUtc,
            settings.UpdatedBy);
}
