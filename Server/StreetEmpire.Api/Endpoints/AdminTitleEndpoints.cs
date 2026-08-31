using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Endpoints;

internal static class AdminTitleEndpoints
{
    internal static void MapAdminTitleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/titles", async (
            CurrentPlayerService current,
            TitleService titles,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            return Results.Ok(new
            {
                criteria = TitleService.CriteriaCatalog(),
                titles = await titles.CustomTitlesAsync(ct)
            });
        }).RequireAuthorization();

        app.MapPost("/api/admin/titles", async (
            AdminCustomTitleRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            TitleService titles,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            try
            {
                var now = DateTime.UtcNow;
                var title = await titles.CreateCustomTitleAsync(request, admin.Account, now, ct);
                admins.Record(admin.Account, "CreateTitle", null, $"created title: {title.Title}", request.Reason, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(TitleService.ToAdminResponse(title));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPut("/api/admin/titles/{id:long}", async (
            long id,
            AdminCustomTitleRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            TitleService titles,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            try
            {
                var now = DateTime.UtcNow;
                var title = await titles.UpdateCustomTitleAsync(id, request, admin.Account, now, ct);
                if (title is null) return Results.NotFound(new { error = "Title not found." });

                admins.Record(admin.Account, "EditTitle", null, $"edited title: {title.Title}", request.Reason, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(TitleService.ToAdminResponse(title));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }
}
