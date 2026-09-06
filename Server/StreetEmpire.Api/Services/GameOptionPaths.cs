using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Reflection over <see cref="GameOptions"/> so an admin can retune scalar values at runtime without a
/// restart. Deliberately covers scalars only: the table-shaped settings (storage levels, lab tiers,
/// hideout tiers) are lists whose shape carries meaning, and editing those safely needs a different UI
/// and validation than a single number does. They stay in appsettings.
/// </summary>
public static class GameOptionPaths
{
    /// <summary>Every editable scalar, as a dotted path with its current value.</summary>
    public static List<GameOptionPath> Describe(GameOptions options)
    {
        var found = new List<GameOptionPath>();
        Walk(options, string.Empty, found);
        return found.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool IsKnownPath(GameOptions reference, string path)
        => Resolve(reference, path) is not null;

    /// <summary>
    /// Parses and writes one override. Returns false with a reason rather than throwing, because these
    /// values come from an admin form and every failure should be reportable.
    /// </summary>
    public static bool TryApply(GameOptions options, string path, string value, out string? error)
    {
        var target = Resolve(options, path);
        if (target is null)
        {
            error = $"'{path}' is not an editable setting.";
            return false;
        }

        var (owner, property) = target.Value;
        if (!TryConvert(property.PropertyType, value, out var converted))
        {
            error = $"'{value}' is not a valid {FriendlyType(property.PropertyType)} for '{path}'.";
            return false;
        }

        // A declared range wins where there is one, because it is the specific rule and the sign check
        // below is only the general one. See BoundsOf for what these are and why they are not merely
        // taste: past them a setting stops being a tuning value and starts being a fault.
        if (BoundsOf(property) is { } bounds && converted is not bool)
        {
            var number = Convert.ToDouble(converted, CultureInfo.InvariantCulture);
            if (number < bounds.Minimum || number > bounds.Maximum)
            {
                error = $"'{path}' has to be between {Format(bounds.Minimum)} and {Format(bounds.Maximum)}.";
                return false;
            }
        }
        // Negative tuning values are almost always a typo and several formulas assume non-negative.
        else if (converted is int i && i < 0 || converted is long l && l < 0 || converted is double d && d < 0)
        {
            error = $"'{path}' cannot be negative.";
            return false;
        }

        property.SetValue(owner, converted);
        error = null;
        return true;
    }

    public static string? Read(GameOptions options, string path)
    {
        var target = Resolve(options, path);
        if (target is null)
            return null;

        var (owner, property) = target.Value;
        return Format(property.GetValue(owner));
    }

    private static void Walk(object instance, string prefix, List<GameOptionPath> found)
    {
        foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var path = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
            if (IsScalar(property.PropertyType))
            {
                if (property.CanWrite)
                {
                    var bounds = BoundsOf(property);
                    found.Add(new GameOptionPath(
                        path,
                        FriendlyType(property.PropertyType),
                        Format(property.GetValue(instance)),
                        bounds is null ? null : Format(bounds.Value.Minimum),
                        bounds is null ? null : Format(bounds.Value.Maximum)));
                }
                continue;
            }

            if (!IsNested(property.PropertyType))
                continue;

            var nested = property.GetValue(instance);
            if (nested is not null)
                Walk(nested, path, found);
        }
    }

    /// <summary>Walks a dotted path to the object that owns the final property.</summary>
    private static (object Owner, PropertyInfo Property)? Resolve(object root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = root;
        for (var i = 0; i < segments.Length; i++)
        {
            var property = current.GetType().GetProperty(
                segments[i],
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
                return null;

            if (i == segments.Length - 1)
                return IsScalar(property.PropertyType) && property.CanWrite ? (current, property) : null;

            if (!IsNested(property.PropertyType))
                return null;

            var next = property.GetValue(current);
            if (next is null)
                return null;
            current = next;
        }

        return null;
    }

    private static bool TryConvert(Type type, string value, out object? converted)
    {
        var trimmed = value.Trim();
        if (type == typeof(int) && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            converted = i;
            return true;
        }

        if (type == typeof(long) && long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            converted = l;
            return true;
        }

        if (type == typeof(double) && double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            converted = d;
            return true;
        }

        if (type == typeof(bool) && bool.TryParse(trimmed, out var b))
        {
            converted = b;
            return true;
        }

        converted = null;
        return false;
    }

    private static string Format(object? value)
        => value switch
        {
            null => string.Empty,
            double d => d.ToString("G17", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

    /// <summary>
    /// The limits declared on a setting, or null for one that only has to be a number.
    ///
    /// Read off a [Range] on the property rather than kept in a table here, so the limit lives beside
    /// the value it constrains and is read by whoever is next to change the default. A table in this
    /// file would be a second list to keep in step with the first, and the failure it exists to
    /// prevent is exactly the failure of two lists that disagree.
    ///
    /// These are not opinions about balance. They are the points past which a setting breaks
    /// something: a length the database column cannot hold, a share of one rolled against directly, a
    /// percentage that would pay out more than it took.
    /// </summary>
    private static (double Minimum, double Maximum)? BoundsOf(PropertyInfo property)
    {
        var range = property.GetCustomAttribute<RangeAttribute>();
        if (range is null)
            return null;

        return (Convert.ToDouble(range.Minimum, CultureInfo.InvariantCulture),
                Convert.ToDouble(range.Maximum, CultureInfo.InvariantCulture));
    }

    private static string Format(double value)
        => value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    private static string FriendlyType(Type type)
        => type == typeof(bool) ? "boolean" : type == typeof(double) ? "decimal" : "whole number";

    private static bool IsScalar(Type type)
        => type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(bool);

    /// <summary>Nested options objects recurse; lists and everything foreign are skipped.</summary>
    private static bool IsNested(Type type)
        => type.IsClass
           && type != typeof(string)
           && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
           && type.Namespace == typeof(GameOptions).Namespace;
}

/// <summary>
/// One editable setting. <paramref name="Minimum"/> and <paramref name="Maximum"/> are null for the
/// settings that only have to be a number, and stated for the ones with a real limit so the admin
/// page can say so rather than leaving it to be discovered by being refused.
/// </summary>
public sealed record GameOptionPath(
    string Path,
    string Type,
    string CurrentValue,
    string? Minimum = null,
    string? Maximum = null);
