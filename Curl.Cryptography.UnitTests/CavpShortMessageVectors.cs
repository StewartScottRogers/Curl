using System.Globalization;

namespace Curl.Cryptography;

/// <summary>
/// Reads a NIST CAVP byte-oriented SHA-3 or SHAKE "ShortMsg" response file embedded under
/// <c>KnownAnswers</c>: each vector's message (empty when <c>Len = 0</c>, whose <c>Msg</c>
/// line is a placeholder <c>00</c>) and its expected digest or output.
/// </summary>
internal static class CavpShortMessageVectors
{
    /// <summary>Returns every (message, expected) pair of the embedded file <paramref name="resourceName" />.</summary>
    public static IReadOnlyList<(byte[] Message, byte[] Expected)> Read(string resourceName)
    {
        using Stream stream = typeof(CavpShortMessageVectors).Assembly.GetManifestResourceStream("KnownAnswers." + resourceName)!;
        using StreamReader reader = new(stream);
        List<(byte[] Message, byte[] Expected)> vectors = [];
        int lengthInBits = 0;
        byte[] message = [];
        while (reader.ReadLine() is string line)
        {
            string[] parts = line.Split(" = ", 2);
            switch (parts[0])
            {
                case "Len":
                    lengthInBits = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
                case "Msg":
                    message = Convert.FromHexString(parts[1])[..(lengthInBits / 8)];
                    break;
                case "MD":
                case "Output":
                    vectors.Add((message, Convert.FromHexString(parts[1])));
                    break;
            }
        }

        return vectors;
    }
}
