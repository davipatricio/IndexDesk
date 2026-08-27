using System.Text;
using System.Text.Json;
using IndexDesk.Modules.Auth.Domain.Dtos;

namespace IndexDesk.Modules.Auth.Services;

/// <summary>
/// Pure parser/serializer between the <c>users.preferences</c> jsonb payload and
/// <see cref="UserPreferencesDto" />. Unknown keys are preserved (round-trip merge);
/// missing keys fall back to defaults (<c>hideValues = false</c>).
/// </summary>
public static class UserPreferencesCodec
{
    private const string HideValuesKey = "hideValues";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static UserPreferencesDto Parse(string? preferences)
    {
        if (string.IsNullOrWhiteSpace(preferences))
        {
            return new UserPreferencesDto(HideValues: false);
        }

        try
        {
            using var document = JsonDocument.Parse(preferences);
            var root = document.RootElement;

            if (root.ValueKind is not JsonValueKind.Object)
            {
                return new UserPreferencesDto(HideValues: false);
            }

            var hideValues =
                root.TryGetProperty(HideValuesKey, out var element)
                && element.ValueKind is JsonValueKind.True or JsonValueKind.False
                && element.GetBoolean();

            return new UserPreferencesDto(hideValues);
        }
        catch (JsonException)
        {
            return new UserPreferencesDto(HideValues: false);
        }
    }

    /// <summary>
    /// Merges <paramref name="hideValues" /> into the stored jsonb, preserving unknown
    /// keys. Returns the compact JSON string to persist (never null).
    /// </summary>
    public static string Serialize(string? current, bool hideValues)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            WritePreservedKeys(writer, current);
            writer.WriteBoolean(HideValuesKey, hideValues);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WritePreservedKeys(Utf8JsonWriter writer, string? current)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(current);
            var root = document.RootElement;

            if (root.ValueKind is not JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == HideValuesKey)
                {
                    continue;
                }

                property.WriteTo(writer);
            }
        }
        catch (JsonException)
        {
            // Corrupted jsonb: drop unknown keys, keep the typed ones.
        }
    }
}
