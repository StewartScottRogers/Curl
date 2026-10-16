using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;

namespace Curl.Mcp;

/// <summary>
/// Turns a checked <see cref="McpTransferRequest" /> into the <see cref="EasyOptions" /> of one easy
/// handle (ADR-0593), setting only the options its fields name and leaving every other at libcurl's
/// default. The method is set as <c>curl -X</c>, <c>-d</c> and <c>-I</c> set it: <c>GET</c> with no
/// body sets nothing, <c>HEAD</c> sets <c>CURLOPT_NOBODY</c>, a body or <c>POST</c> sets
/// <c>CURLOPT_POST</c> and <c>CURLOPT_POSTFIELDS</c>, and any other method, or <c>GET</c> with a
/// body, is <c>CURLOPT_CUSTOMREQUEST</c>.
/// </summary>
public static class McpTransferOptions
{
    /// <summary>The options <paramref name="request" /> asks for; the callbacks are left unset.</summary>
    /// <param name="request">A request <see cref="McpTransferRequestCheck" /> found valid.</param>
    /// <param name="body">The body's bytes, as the check decoded them; <see langword="null" /> for none.</param>
    /// <returns>The handle's options.</returns>
    public static EasyOptions Build(McpTransferRequest request, byte[]? body)
    {
        ArgumentNullException.ThrowIfNull(request);
        EasyOptions defaults = new();
        return defaults with
        {
            Network = defaults.Network with { Url = request.Url },
            Transfer = Transfer(defaults.Transfer, request, body),
            Http = Http(defaults.Http, request, body),
            Tls = Tls(defaults.Tls, request.Insecure),
            Proxy = defaults.Proxy with { Proxy = request.Proxy },
        };
    }

    private static EasyTransferOptions Transfer(EasyTransferOptions defaults, McpTransferRequest request, byte[]? body) =>
        defaults with
        {
            CustomRequest = CustomRequest(request.Method, body),
            NoBody = request.Method == "HEAD",
            Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds ?? 0),
        };

    private static string? CustomRequest(string method, byte[]? body) => method switch
    {
        "HEAD" or "POST" => null,
        "GET" when body is null => null,
        _ => method,
    };

    private static EasyHttpOptions Http(EasyHttpOptions defaults, McpTransferRequest request, byte[]? body)
    {
        bool post = body is not null || request.Method == "POST";
        return defaults with
        {
            HttpHeaders = [.. request.Headers.Select(HeaderLine)],
            UserAgent = request.UserAgent,
            FollowLocation = request.FollowRedirects ? FollowLocation.All : FollowLocation.None,
            Post = post,
            PostFields = post ? new ReadOnlyMemory<byte>(body ?? []) : (ReadOnlyMemory<byte>?)null,
        };
    }

    /// <summary>
    /// The <c>CURLOPT_HTTPHEADER</c> line for <paramref name="header" />: <c>Name: value</c>, or
    /// <c>Name;</c> for an empty value, which libcurl sends as an empty header rather than removing it.
    /// </summary>
    private static string HeaderLine(McpTransferHeader header) =>
        header.Value.Trim().Length == 0 ? $"{header.Name};" : $"{header.Name}: {header.Value.Trim()}";

    private static EasyTlsOptions Tls(EasyTlsOptions defaults, bool insecure) =>
        insecure
            ? defaults with { Origin = defaults.Origin with { VerifyPeer = false, VerifyHost = false } }
            : defaults;
}
