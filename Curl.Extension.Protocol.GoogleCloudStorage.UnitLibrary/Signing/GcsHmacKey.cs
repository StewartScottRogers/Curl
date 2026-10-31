namespace Curl.Extension.Protocol.GoogleCloudStorage.Signing;

/// <summary>The Cloud Storage HMAC key that signs an XML API request.</summary>
/// <param name="AccessId">The access ID, which the <c>Authorization</c> header names.</param>
/// <param name="Secret">The secret, from which the signing key is derived.</param>
/// <param name="Region">The region of the credential scope; Cloud Storage accepts <see cref="AutoRegion"/> for any bucket.</param>
public sealed record GcsHmacKey(string AccessId, string Secret, string Region = GcsHmacKey.AutoRegion)
{
    /// <summary>The region <c>auto</c>, which the XML API accepts whatever the bucket's location.</summary>
    public const string AutoRegion = "auto";
}
