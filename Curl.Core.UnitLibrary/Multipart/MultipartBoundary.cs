using System.Security.Cryptography;

namespace Curl.Core.Multipart;

/// <summary>
/// Makes the boundary curl 8.21.0 separates multipart parts with: 24 dashes followed by
/// 22 random letters and digits.
/// </summary>
public static class MultipartBoundary
{
    /// <summary>The number of characters in a boundary.</summary>
    public const int Length = 46;

    private const string Dashes = "------------------------";

    private const string Alphanumerics = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>Creates a new random boundary.</summary>
    /// <returns>24 dashes followed by 22 characters drawn from <c>A-Z</c>, <c>a-z</c> and <c>0-9</c>.</returns>
    public static string CreateRandom() =>
        Dashes + RandomNumberGenerator.GetString(Alphanumerics, Length - Dashes.Length);
}
