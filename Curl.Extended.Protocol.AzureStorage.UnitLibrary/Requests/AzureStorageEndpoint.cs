namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>Where an Azure Storage URL's request goes: always HTTPS to <see cref="Host"/> on <see cref="Port"/>.</summary>
/// <param name="Host">The service endpoint's host, such as <c>account.blob.core.windows.net</c>.</param>
/// <param name="Path">The resource path, percent-encoded, such as <c>/container/blob</c>; <c>/</c> for the account itself.</param>
/// <param name="Query">The URL's query without its <c>?</c>, or empty for none.</param>
/// <param name="Port">The TCP port the URL names, or <see cref="DefaultPort"/> when it names none.</param>
public sealed record AzureStorageEndpoint(string Host, string Path, string Query, int Port = AzureStorageEndpoint.DefaultPort)
{
    /// <summary>The HTTPS port used when the URL names no port.</summary>
    public const int DefaultPort = 443;

    /// <summary>Gets the <c>Host</c> header value: the host, with <c>:port</c> only when the port is not <see cref="DefaultPort"/>.</summary>
    public string HostHeader => Port == DefaultPort ? Host : Host + ":" + Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
