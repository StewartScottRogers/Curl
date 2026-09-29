namespace Curl.Kerberos;

/// <summary>Reads test vectors as the RFCs print them: hexadecimal, with spaces between bytes or groups.</summary>
internal static class Hex
{
    public static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));
}
