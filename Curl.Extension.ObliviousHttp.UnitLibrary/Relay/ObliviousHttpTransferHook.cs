using System.Security.Cryptography;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.ObliviousHttp.BinaryHttp;
using Curl.Extension.ObliviousHttp.Encapsulation;

namespace Curl.Extension.ObliviousHttp.Relay;

/// <summary>
/// The <c>--ohttp-relay</c> transfer hook (ADR-1157), the client of RFC 9458 section 2: reads the gateway's key
/// configuration (<see cref="KeyConfigurationSource" />), encodes the transfer's request as Binary HTTP
/// (<see cref="InnerRequest" />), encapsulates it (section 4.3), POSTs it to the relay as <c>message/ohttp-req</c>
/// (section 5), decapsulates the <c>message/ohttp-res</c> answer (section 4.4) and writes the response it carries
/// (<see cref="InnerResponseOutput" />). A transfer that is not HTTP runs unchanged.
/// </summary>
/// <param name="relay">The relay resource's URL, as <c>--ohttp-relay</c> gave it.</param>
/// <param name="keys">The key configuration's file or URL, as <c>--ohttp-keys</c> gave it, or <see langword="null" /> for the target's well-known gateway.</param>
internal sealed class ObliviousHttpTransferHook(string relay, string? keys) : ITransferHook
{
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context);
        }

        try
        {
            return await RunObliviouslyAsync(context, next);
        }
        catch (ObliviousTransferException exception)
        {
            return exception.Result;
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException or CryptographicException)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, "Oblivious HTTP: " + exception.Message);
        }
    }

    private async Task<TransferResult> RunObliviouslyAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        InnerRequest.ThrowIfNotRelayable(context);
        CurlUrl relayUrl = HookRequest.ParseUrl(relay, ObliviousHttpExtension.RelayOption.Name);
        byte[] keyConfigurations = await KeyConfigurationSource.ReadAsync(keys, context, next);
        KeyConfigurationChoice choice = KeyConfigurationList.SelectFirstSupported(KeyConfigurationList.Parse(keyConfigurations));
        BinaryHttpRequest inner = InnerRequest.Of(context);
        using ObliviousRequest request = ObliviousRequest.Encapsulate(choice, BinaryHttpRequestEncoder.EncodeKnownLength(inner));
        byte[] encapsulatedResponse = await HookRequest.SendAsync(
            new HookRequest("relay", relayUrl, "POST", request.EncapsulatedRequest.ToArray(), ObliviousHttpMediaTypes.EncapsulatedRequest, ObliviousHttpMediaTypes.EncapsulatedResponse),
            context,
            next);
        BinaryHttpResponse response = BinaryHttpResponseDecoder.Decode(request.DecapsulateResponse(encapsulatedResponse));
        return await InnerResponseOutput.WriteAsync(response, inner.Method, context);
    }
}
