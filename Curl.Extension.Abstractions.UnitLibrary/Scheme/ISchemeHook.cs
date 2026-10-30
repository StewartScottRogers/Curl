using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Abstractions.Scheme;

/// <summary>
/// What an activated extension supplies in place of curl's handler for some of curl's own schemes (ADR-1048):
/// the schemes it serves and the handler that serves them, as an extension's option runs <c>ws://</c> and
/// <c>wss://</c> as RFC 8441 extended CONNECT. Every other scheme stays curl's.
/// </summary>
public interface ISchemeHook
{
    /// <summary>Gets the option that activated the hook, named when two hooks claim curl's schemes.</summary>
    ExtensionOption Option { get; }

    /// <summary>Gets the schemes the hook serves in place of curl's handler, in lower case.</summary>
    IReadOnlyCollection<string> Schemes { get; }

    /// <summary>Gives the handler that serves the hook's schemes in place of <paramref name="curlHandler" />.</summary>
    /// <param name="curlHandler">Curl's handler for one or more of the hook's schemes, for a hook that falls back to it.</param>
    /// <param name="services">The option group's connector and a loopback listener (ADR-1039).</param>
    /// <returns>The extension's handler.</returns>
    IProtocolHandler HandlerOver(IProtocolHandler curlHandler, TransferHookServices services);
}
