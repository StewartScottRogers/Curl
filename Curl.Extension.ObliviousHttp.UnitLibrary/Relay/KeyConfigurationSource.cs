using Curl.Extension.ObliviousHttp.Encapsulation;

namespace Curl.Extension.ObliviousHttp.Relay;

/// <summary>
/// Gives the gateway's <c>application/ohttp-keys</c> bytes (RFC 9458 section 3.2): read from the file
/// <c>--ohttp-keys</c> names, fetched with GET from the http:// or https:// URL it names, or, without the
/// option, fetched from the target origin's <c>ohttp-gateway</c> well-known URI, where RFC 9540
/// ("Gateway Location" and "Key Configuration Fetching") places a gateway and its key configurations.
/// </summary>
internal static class KeyConfigurationSource
{
    /// <summary>The well-known URI suffix RFC 9540 registers for a target's gateway.</summary>
    public const string WellKnownGatewayPath = "/.well-known/ohttp-gateway";

    /// <summary>Gives the key configuration bytes from <paramref name="keys" />.</summary>
    /// <param name="keys">The file or URL <c>--ohttp-keys</c> gave, or <see langword="null" /> for the target's well-known gateway.</param>
    /// <param name="context">The transfer.</param>
    /// <param name="next">Curl's handler, which fetches a URL.</param>
    /// <exception cref="ObliviousTransferException">The file cannot be read (exit 26) or the fetch failed.</exception>
    public static async Task<byte[]> ReadAsync(string? keys, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (keys is null)
        {
            return await FetchAsync(WellKnownGatewayOf(context.Url), context, next);
        }

        return IsUrl(keys)
            ? await FetchAsync(HookRequest.ParseUrl(keys, ObliviousHttpExtension.KeysOption.Name), context, next)
            : await ReadFileAsync(keys, context.CancellationToken);
    }

    /// <summary>The target origin's <c>ohttp-gateway</c> well-known URI.</summary>
    public static CurlUrl WellKnownGatewayOf(CurlUrl target) =>
        CurlUrl.Parse(target.Scheme + "://" + InnerRequest.AuthorityOf(target) + WellKnownGatewayPath);

    private static bool IsUrl(string keys) =>
        keys.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || keys.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static Task<byte[]> FetchAsync(CurlUrl url, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        HookRequest.SendAsync(new HookRequest("gateway", url, "GET", [], null, ObliviousHttpMediaTypes.KeyConfigurations), context, next);

    private static async Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw ObliviousTransferException.Failing(CurlExitCode.ReadError, $"Failed to read the Oblivious HTTP key configuration file {path}");
        }
    }
}
