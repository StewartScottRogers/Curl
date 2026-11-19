using System.Security.Cryptography;
using System.Text;

namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>Signs a string-to-sign with a storage account key: HMAC-SHA256 under the base64-decoded key, base64 encoded ("Authorize with Shared Key").</summary>
public static class AzureStorageSharedKeySigner
{
    /// <summary>Gives the <c>Authorization</c> header value <c>SharedKey account:signature</c>.</summary>
    /// <param name="account">The storage account name.</param>
    /// <param name="base64Key">The account key, base64 encoded as the portal shows it.</param>
    /// <param name="stringToSign">What <see cref="AzureStorageSharedKeyStringToSign.Build"/> built.</param>
    /// <returns>The header value.</returns>
    /// <exception cref="AzureStorageRequestException">The key is not base64.</exception>
    public static string AuthorizationOf(string account, string base64Key, string stringToSign)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(base64Key);
        ArgumentNullException.ThrowIfNull(stringToSign);
        var key = new byte[base64Key.Length];
        if (!Convert.TryFromBase64String(base64Key, key, out var length) || length == 0)
        {
            throw new AzureStorageRequestException(AzureStorageRequestFailure.InvalidSharedKey, "The account key is not base64.");
        }

        var signature = HMACSHA256.HashData(key.AsSpan(0, length), Encoding.UTF8.GetBytes(stringToSign));
        return "SharedKey " + account + ":" + Convert.ToBase64String(signature);
    }
}
