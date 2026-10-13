using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace QuotaFloat.Core;

public static class Json
{
    public static JsonElement Get(this JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) ? p : default;
    public static string? Str(this JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() : null;
    public static long? Num(this JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var n) ? n : null;
    public static double? Double(this JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var n) ? n : null;
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
public record QuotaWindow(double Used, int Minutes, long Reset)
{
    public double Remaining => Math.Clamp(100 - Used, 0, 100);
    public static QuotaWindow? Parse(JsonElement e)
    {
        var used = e.Get("usedPercent").Double(); var mins = e.Get("windowDurationMins").Num(); var reset = e.Get("resetsAt").Num();
        return used is >= 0 && mins is > 0 && reset is > 0 ? new(used.Value, (int)mins.Value, reset.Value) : null;
    }
}
public record ResetCredits(int AvailableCount, int? NearestExpiryCount, long? NearestExpiryAt, bool DetailsComplete)
{
    public static ResetCredits? Parse(JsonElement result)
    {
        var data = result.Get("rateLimitResetCredits");
        var count = data.Get("availableCount").Num();
        if (count is null or < 0 or > int.MaxValue) return null;
        var details = data.Get("credits");
        if (count == 0) return new(0, 0, null, true);
        if (details.ValueKind != JsonValueKind.Array) return new((int)count.Value, null, null, false);
        var available = details.EnumerateArray().Where(c => c.Get("status").Str() == "available").ToArray();
        var expirations = available.Select(c => c.Get("expiresAt").Num()).Where(t => t is > 0).Select(t => t!.Value).ToArray();
        var complete = available.Length >= count;
        if (expirations.Length == 0) return new((int)count.Value, null, null, complete);
        var nearest = expirations.Min();
        return new((int)count.Value, expirations.Count(t => t == nearest), nearest, complete);
    }
}
public static class QuotaAlerts
{
    public const double LowRemainingPercent = 20;
    public const int ResetExpiryDays = 3;

    public static bool IsLow(QuotaWindow? window, DateTimeOffset now) =>
        window != null && window.Reset > now.ToUnixTimeSeconds() && window.Remaining <= LowRemainingPercent;

    public static bool IsResetExpiring(ResetCredits? resets, DateTimeOffset now) =>
        resets is { AvailableCount: > 0, NearestExpiryCount: > 0, NearestExpiryAt: long expiry }
        && expiry > now.ToUnixTimeSeconds()
        && expiry <= now.AddDays(ResetExpiryDays).ToUnixTimeSeconds();
}
public record QuotaSnapshot(string Account, string Plan, DateTimeOffset CapturedAt, QuotaWindow? FiveHour, QuotaWindow? Week, ResetCredits? Resets = null)
{
    public static QuotaSnapshot Parse(string account, string plan, JsonElement result)
    {
        var bucket = result.Get("rateLimitsByLimitId").Get("codex");
        if (bucket.ValueKind != JsonValueKind.Object) bucket = result.Get("rateLimits");
        var windows = new[] { QuotaWindow.Parse(bucket.Get("primary")), QuotaWindow.Parse(bucket.Get("secondary")) };
        return new(account, plan, DateTimeOffset.UtcNow, windows.FirstOrDefault(w => w?.Minutes == 300), windows.FirstOrDefault(w => w?.Minutes == 10080), ResetCredits.Parse(result));
    }
}
public record DayUsage(string Day, long Tokens);
public record QuotaPoint(long Time, double? Five, double? Week, long? FiveReset, long? WeekReset);
public record DashboardData(List<DayUsage> OfficialDays, List<QuotaPoint> Quotas);
