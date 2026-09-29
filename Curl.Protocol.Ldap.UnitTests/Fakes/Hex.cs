namespace Curl.Protocol.Ldap.Fakes;

/// <summary>Turns the space-separated hex pairs a recording's transcript shows into bytes.</summary>
public static class Hex
{
    /// <summary>Parses hex pairs, ignoring spaces.</summary>
    /// <param name="pairs">Hex pairs such as <c>30 0c 02 01 01</c>.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Bytes(string pairs) => Convert.FromHexString(pairs.Replace(" ", string.Empty, StringComparison.Ordinal));
}
