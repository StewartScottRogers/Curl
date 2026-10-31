using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ObliviousHttp.Relay;

/// <summary>
/// A request the hook sends through curl's handler on its own account, to the relay or to the gateway's key
/// configuration resource, whose body it captures instead of writing it to the transfer's output.
/// </summary>
/// <param name="Role">What the hook calls the server in an error message: <c>relay</c> or <c>gateway</c>.</param>
/// <param name="Url">Where the request goes.</param>
/// <param name="Method">The request method.</param>
/// <param name="Body">The request content; empty for none, so the transfer's own body is never sent.</param>
/// <param name="ContentType">The request's <c>Content-Type</c>, or <see langword="null" /> for none.</param>
/// <param name="AcceptedType">The media type the response must carry, sent as <c>Accept</c>.</param>
internal sealed record HookRequest(string Role, CurlUrl Url, string Method, byte[] Body, string? ContentType, string AcceptedType)
{
    private const int Ok = 200;

    /// <summary>Parses <paramref name="text" />, given to <c>--<paramref name="optionName" /></c>, as a URL.</summary>
    /// <exception cref="ObliviousTransferException">It is not a URL: exit 3, as curl gives for a malformed URL.</exception>
    public static CurlUrl ParseUrl(string text, string optionName) =>
        CurlUrl.TryParse(text, pathAsIs: false, out CurlUrl? url)
            ? url
            : throw ObliviousTransferException.Failing(CurlExitCode.UrlMalformat, $"--{optionName} is not a URL: {text}");

    /// <summary>Sends <paramref name="request" /> through <paramref name="next" /> and gives the response body.</summary>
    /// <exception cref="ObliviousTransferException">
    /// The transfer failed (its own result), the answer was not 200 (exit 22) or not of the accepted type (exit 8).
    /// </exception>
    public static async Task<byte[]> SendAsync(HookRequest request, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        using MemoryStream received = new();
        TransferResult result = await next(context
            .WithRequest(request.Url, request.Method, request.Body)
            .WithoutOwnHeaders()
            .WithRequestHeaders(request.Headers())
            .WithOutputs(received, null, null));
        request.ThrowIfNotAnswered(result);
        return received.ToArray();
    }

    private IEnumerable<string> Headers() =>
        ContentType is null ? ["Accept: " + AcceptedType] : ["Content-Type: " + ContentType, "Accept: " + AcceptedType];

    private void ThrowIfNotAnswered(TransferResult result)
    {
        if (!result.IsSuccess)
        {
            throw new ObliviousTransferException(result);
        }

        int code = result.Report?.ResponseCode ?? 0;
        if (code != Ok)
        {
            throw ObliviousTransferException.Failing(CurlExitCode.HttpReturnedError, $"The Oblivious HTTP {Role} returned error: {code}");
        }

        string? type = result.Report!.ContentType;
        if (!IsAccepted(type))
        {
            throw ObliviousTransferException.Failing(CurlExitCode.WeirdServerReply, $"The Oblivious HTTP {Role} answered with Content-Type {type ?? "(none)"}, not {AcceptedType}");
        }
    }

    private bool IsAccepted(string? type) =>
        type is not null && string.Equals(type.Split(';', 2)[0].Trim(), AcceptedType, StringComparison.OrdinalIgnoreCase);
}
