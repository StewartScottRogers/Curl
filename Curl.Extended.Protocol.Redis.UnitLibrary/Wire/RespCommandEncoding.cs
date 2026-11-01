using System.Globalization;
using System.Text;

namespace Curl.Extended.Protocol.Redis.Wire;

/// <summary>Writes a command as RESP3's "Sending commands to a Redis server" says: an array of bulk strings.</summary>
internal static class RespCommandEncoding
{
    /// <summary>Gives <paramref name="arguments"/>, the command name first, as one RESP array of bulk strings.</summary>
    internal static byte[] Encode(IReadOnlyList<byte[]> arguments)
    {
        using MemoryStream encoded = new();
        WriteAscii(encoded, string.Create(CultureInfo.InvariantCulture, $"*{arguments.Count}\r\n"));
        foreach (byte[] argument in arguments)
        {
            WriteAscii(encoded, string.Create(CultureInfo.InvariantCulture, $"${argument.Length}\r\n"));
            encoded.Write(argument);
            WriteAscii(encoded, "\r\n");
        }

        return encoded.ToArray();
    }

    /// <summary>Gives the UTF-8 bytes of each of <paramref name="arguments"/>.</summary>
    internal static IReadOnlyList<byte[]> Arguments(params string[] arguments) => [.. arguments.Select(Encoding.UTF8.GetBytes)];

    private static void WriteAscii(MemoryStream stream, string text) => stream.Write(Encoding.ASCII.GetBytes(text));
}
