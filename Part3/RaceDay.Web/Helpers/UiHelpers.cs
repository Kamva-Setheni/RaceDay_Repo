using System.Globalization;

namespace RaceDay.Web.Helpers;

/// <summary>Small presentation helpers shared by the views (no business logic).</summary>
public static class UiHelpers
{
    public static string TypeCss(string? type) => type switch
    {
        "Walk" => "type-walk",
        "Cycle" => "type-cycle",
        _ => "type-run"
    };

    public static string TypeIcon(string? type) => type switch
    {
        "Run" => "🏃",
        "Walk" => "🚶",
        "Cycle" => "🚴",
        _ => "🏁"
    };

    /// <summary>Colour coding for enrolment and result statuses (green, amber, red, grey).</summary>
    public static string StatusCss(string? status) => status switch
    {
        "Confirmed" or "Finished" => "status-confirmed",
        "Pending" => "status-pending",
        "DNF" or "DSQ" => "status-danger",
        _ => "status-muted"
    };

    public static string FormatTime(TimeSpan? time) =>
        time is null
            ? "-"
            : $"{(int)time.Value.TotalHours:00}:{time.Value.Minutes:00}:{time.Value.Seconds:00}";

    public static string Money(decimal amount) =>
        amount == 0 ? "Free" : "R" + amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string DateLong(DateTime date) =>
        date.ToString("ddd, d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture);

    public static string DateShort(DateTime date) =>
        date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static bool IsUpcoming(DateTime date) => date >= DateTime.Today;

    /// <summary>1 becomes 1st, 2 becomes 2nd, 47 becomes 47th, 112 becomes 112th.</summary>
    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13) return $"{number}th";
        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        };
    }

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }
}
