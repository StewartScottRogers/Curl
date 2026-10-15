using Curl.Api.Callbacks;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;

namespace Curl.Api.Options;

/// <summary>
/// Every <c>curl_easy_setopt</c> option of one easy handle (ADR-0506, "The engine's input is a
/// CLI-neutral option model"): one property per option group, each defaulting to that group's
/// default record, so a new instance is a fresh handle's options and <c>curl_easy_reset</c> is
/// "back to <c>new EasyOptions()</c>".
/// </summary>
public sealed record EasyOptions
{
    /// <summary>The behaviour options (verbosity, progress, signals and the like). Default: every one at libcurl's default.</summary>
    public EasyBehaviourOptions Behaviour { get; init; } = new();

    /// <summary>The transfer options (range, resume, upload, speed limits, timeout). Default: every one at libcurl's default.</summary>
    public EasyTransferOptions Transfer { get; init; } = new();

    /// <summary>The connection, name resolution and connection-reuse options. Default: every one at libcurl's default.</summary>
    public EasyNetworkOptions Network { get; init; } = new();

    /// <summary>The TLS options. Default: every one at libcurl's default.</summary>
    public EasyTlsOptions Tls { get; init; } = new();

    /// <summary>The authentication options. Default: every one at libcurl's default.</summary>
    public EasyAuthenticationOptions Authentication { get; init; } = new();

    /// <summary>The proxy options. Default: every one at libcurl's default.</summary>
    public EasyProxyOptions Proxy { get; init; } = new();

    /// <summary>The HTTP options. Default: every one at libcurl's default.</summary>
    public EasyHttpOptions Http { get; init; } = new();

    /// <summary>The protocol-specific options (FTP, SSH, SMTP, TFTP, TELNET, RTSP, WebSocket). Default: every one at libcurl's default.</summary>
    public EasyProtocolOptions Protocols { get; init; } = new();

    /// <summary>The <c>CURLOPT_*FUNCTION</c> callbacks. Default: every one unset.</summary>
    public EasyCallbackOptions Callbacks { get; init; } = new();
}
