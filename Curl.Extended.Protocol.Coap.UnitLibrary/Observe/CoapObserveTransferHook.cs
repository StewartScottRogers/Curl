using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extended.Protocol.Coap.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Coap.Observe;

/// <summary>
/// The <c>--coap-observe</c> option (ADR-1064): gives a <c>coap://</c> or <c>coaps://</c> transfer to the CoAP handler as a
/// <see cref="CoapObserveContext" />, so the handler registers an observation (RFC 7641 section 3.1) and writes
/// each notification until <c>--max-time</c> or interruption. Every other transfer passes through unchanged.
/// </summary>
public sealed class CoapObserveTransferHook : ITransferHook
{
    private CoapObserveTransferHook()
    {
    }

    /// <summary>Gets the option that activates the hook.</summary>
    public static ExtensionOption Option { get; } = new(
        "coap-observe",
        ExtensionOptionArgument.None,
        "Observe a coap:// or coaps:// resource (RFC 7641): write each notification's payload and a newline as it arrives, until --max-time");

    /// <summary>Gives the hook when <paramref name="values" /> hold <see cref="Option" />, otherwise <see langword="null" />.</summary>
    /// <param name="values">An option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" />.</returns>
    public static ITransferHook? ActivatedBy(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new CoapObserveTransferHook() : null;
    }

    /// <inheritdoc />
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        next(IsCoap(context.Url) ? new CoapObserveContext(context) : context);

    private static bool IsCoap(CurlUrl url) =>
        string.Equals(url.Scheme, "coap", StringComparison.OrdinalIgnoreCase) || CoapRequest.IsSecure(url);
}
