using System.Text;
using System.Text.Json;

namespace NMS.Shared.Paging;

public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore, int Limit);

/// <summary>Opaque cursor holding the sort key (created_at) and id of the last returned row.</summary>
public sealed record CursorToken(DateTimeOffset CreatedAt, Guid Id)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public static int NormalizeLimit(int? limit) =>
        Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

    public string Encode() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));

    public static CursorToken? TryDecode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<CursorToken>(json);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}
