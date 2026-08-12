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

/// <summary>Small shared formatters used across endpoint groups.</summary>
internal static class Formatting
{

    internal static string FormatDuration(int seconds)
    {
        var minutes = seconds / 60;
        var remainder = seconds % 60;
        return minutes <= 0
            ? $"{seconds} second{(seconds == 1 ? string.Empty : "s")}"
            : $"{minutes}m {remainder:00}s";
    }

    // Escapes a search term so ILIKE treats its wildcards as literal characters. Callers pass "\" as
    // the ESCAPE character to match.
    internal static string ToLikePattern(string query)
        => $"%{query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

    internal static string? Blank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static double AverageMorale(Player player)
        => Math.Round((player.HoeHappiness + player.ThugHappiness) / 2, 2);
}
