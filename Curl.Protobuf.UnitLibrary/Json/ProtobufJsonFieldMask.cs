using System.Text;
using System.Text.Json;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// The JSON form of <c>google.protobuf.FieldMask</c>: one string of its paths joined by commas,
/// each path's snake_case names written in lowerCamelCase ("foo_bar.baz" as "fooBar.baz").
/// </summary>
internal static class ProtobufJsonFieldMask
{
    private const string FieldMaskName = "google.protobuf.FieldMask";

    /// <summary>Whether messages of this type are a FieldMask.</summary>
    public static bool IsFieldMask(DynamicMessageType type) => type.FullName == FieldMaskName;

    /// <summary>The paths as one comma-separated string of lowerCamelCase paths.</summary>
    public static string Format(DynamicMessage message) =>
        string.Join(',', ((message.FindValue(1) as List<object>) ?? []).Select(path => ToLowerCamelCase((string)path)));

    /// <summary>Reads a comma-separated string of lowerCamelCase paths into the FieldMask's snake_case paths.</summary>
    public static ProtobufJsonError Read(JsonElement element, DynamicMessage message)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return ProtobufJsonError.WrongType;
        }

        var text = element.GetString()!;
        var paths = message.Repeated(message.Type.FindField(1)!);
        foreach (var path in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            paths.Add(ToSnakeCase(path));
        }

        return ProtobufJsonError.None;
    }

    private static string ToLowerCamelCase(string path)
    {
        var camel = new StringBuilder(path.Length);
        var upperNext = false;
        foreach (var character in path)
        {
            if (character == '_')
            {
                upperNext = true;
                continue;
            }

            camel.Append(upperNext ? char.ToUpperInvariant(character) : character);
            upperNext = false;
        }

        return camel.ToString();
    }

    private static string ToSnakeCase(string path)
    {
        var snake = new StringBuilder(path.Length + 4);
        foreach (var character in path)
        {
            snake.Append(char.IsAsciiLetterUpper(character) ? "_" + char.ToLowerInvariant(character) : character.ToString());
        }

        return snake.ToString();
    }
}
