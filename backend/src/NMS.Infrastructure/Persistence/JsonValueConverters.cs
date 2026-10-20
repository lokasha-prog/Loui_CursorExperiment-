using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NMS.Infrastructure.Persistence;

internal static class JsonText
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}

/// <summary>Stores a .NET object as JSON text in an nvarchar(max) column.</summary>
public sealed class JsonValueConverter<T>() : ValueConverter<T, string>(v => JsonText.Write(v), v => JsonText.Read<T>(v))
    where T : class;

public sealed class JsonValueComparer<T>() : ValueComparer<T>(
    (a, b) => JsonText.Write(a) == JsonText.Write(b),
    v => JsonText.Write(v).GetHashCode(),
    v => JsonText.Read<T>(JsonText.Write(v)))
    where T : class;
