namespace Curl.Extension.Protocol.AzureStorage.Requests;

/// <summary>The credential an Azure Storage request is authorized with.</summary>
/// <param name="Kind">How the request is authorized.</param>
/// <param name="Value">The SAS token or bearer token; empty for <see cref="AzureStorageCredentialKind.Anonymous"/>.</param>
public sealed record AzureStorageCredential(AzureStorageCredentialKind Kind, string Value)
{
    /// <summary>The environment variable holding a shared access signature.</summary>
    public const string SasTokenVariable = "AZURE_STORAGE_SAS_TOKEN";

    /// <summary>The environment variable holding a Microsoft Entra ID access token.</summary>
    public const string BearerTokenVariable = "AZURE_STORAGE_BEARER_TOKEN";

    /// <summary>Gets the credential that authorizes nothing.</summary>
    public static AzureStorageCredential Anonymous { get; } = new(AzureStorageCredentialKind.Anonymous, string.Empty);

    /// <summary>
    /// Chooses the credential: curl's <c>--oauth2-bearer</c> first, then <see cref="BearerTokenVariable"/>,
    /// then <see cref="SasTokenVariable"/>, else anonymous. An empty value counts as unset.
    /// </summary>
    /// <param name="oauth2Bearer">The <c>--oauth2-bearer</c> value, or <see langword="null"/> when it was not given.</param>
    /// <param name="readEnvironmentVariable">Reads an environment variable, returning <see langword="null"/> when it is unset.</param>
    /// <returns>The credential to authorize with.</returns>
    public static AzureStorageCredential Choose(string? oauth2Bearer, Func<string, string?> readEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        var bearer = string.IsNullOrEmpty(oauth2Bearer) ? readEnvironmentVariable(BearerTokenVariable) : oauth2Bearer;
        if (!string.IsNullOrEmpty(bearer))
        {
            return new AzureStorageCredential(AzureStorageCredentialKind.Bearer, bearer);
        }

        var sas = readEnvironmentVariable(SasTokenVariable);
        return string.IsNullOrEmpty(sas) ? Anonymous : new AzureStorageCredential(AzureStorageCredentialKind.SharedAccessSignature, sas);
    }
}
