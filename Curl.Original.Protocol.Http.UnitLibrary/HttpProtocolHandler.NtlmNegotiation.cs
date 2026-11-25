using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that follows the state of an NTLM negotiation on a connection.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Gives the first request of a redirect hop the plan it goes out with on a pooled connection
    /// that the transfer's earlier hop's NTLM handshake authenticated
    /// (<see cref="HttpRequestOptions.AuthSchemePicked" /> NTLM): without the Type 1 message and
    /// with the body a probe held back (<see cref="HttpRequestPlan.WithoutNtlmType1Probe" />), as
    /// curl 8.22.0's <c>Curl_output_ntlm</c> sends nothing once the connection's NTLM state is past
    /// Type 3 (upstream test1100, BL-3003); <paramref name="plan" /> itself otherwise. A new
    /// connection, and the next transfer of an easy handle, start the handshake again with Type 1
    /// (upstream test2025, test2028 and test2031).
    /// </summary>
    private static HttpRequestPlan OnNtlmAuthenticatedConnection(HttpRequestPlan plan, ConnectResult connect, TransferReport? earlier) =>
        earlier is null && connect.IsReused && plan.Options.RedirectsFollowed > 0 && plan.Options.AuthSchemePicked == HttpAuthSchemes.Ntlm
            ? plan.WithoutNtlmType1Probe()
            : plan;

    /// <summary>
    /// Decides whether a 401 to a request sent with an NTLM Type 1 message carries the Type 2
    /// challenge on a connection it does not keep alive, as an HTTP/1.0 server or
    /// <c>Connection: close</c> ends it: curl 8.22.0's <c>lib/http.c</c> then writes
    /// <see cref="NtlmConnectionClosureLine" /> before the head's empty line and sets
    /// <c>authproblem</c>, so the 401 ends the transfer without the Type 3 message, which would
    /// have to go on a new connection the handshake never reached (measured, upstream test159, BL-3006).
    /// </summary>
    private static bool ClosesWhileNegotiatingNtlm(HttpRequestPlan plan, HttpResponseHead head)
    {
        HttpRequestOptions options = plan.Options;
        return head.StatusLine.StatusCode == 401
            && IsNtlmType1(plan.Authorization)
            && ValuesOf(head, "WWW-Authenticate").Any(IsNtlmType2Challenge)
            && !HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, options.PassesTransferCoding, options.IgnoreContentLength, options.TransferEncoding, IsThroughHttpProxy(options));
    }

    /// <summary>
    /// Reports <see cref="NtlmConnectionClosureLine" /> for a 401 <see cref="ClosesWhileNegotiatingNtlm" />
    /// accepts, after the head's last header and before its empty line, as curl 8.22.0 writes it
    /// (measured, BL-3006).
    /// </summary>
    private static void ReportNtlmConnectionClosure(HttpRequestPlan plan, HttpResponseHead head)
    {
        if (ClosesWhileNegotiatingNtlm(plan, head))
        {
            plan.Context.Events.ReportInfo(NtlmConnectionClosureLine);
        }
    }

    /// <summary>
    /// Decides whether a <c>WWW-Authenticate</c> value is an NTLM challenge that carries a
    /// message, <c>NTLM</c> and a token, rather than the bare <c>NTLM</c> that offers the scheme.
    /// </summary>
    private static bool IsNtlmType2Challenge(string challenge)
    {
        string trimmed = challenge.Trim();
        return trimmed.Length > 5 && trimmed.StartsWith("NTLM ", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The line curl 8.22.0 writes before a 401's empty line when the connection closes with an
    /// NTLM Type 2 challenge (<see cref="ClosesWhileNegotiatingNtlm" />, measured, BL-3006).
    /// </summary>
    private const string NtlmConnectionClosureLine = "Connection closure while negotiating auth (HTTP 1.0?)";

    /// <summary>
    /// Decides whether a <c>Proxy-Authorization</c> value is an NTLM Type 3 message: <c>NTLM</c>
    /// and the base64 of <c>NTLMSSP\0</c> followed by the message type 3, as
    /// <see cref="IsNtlmType1" /> reads Type 1.
    /// </summary>
    private static bool IsNtlmType3(string value) =>
        value.StartsWith(NtlmType3Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Decides whether a retry sends one side no value because that side's NTLM Type 3 went out
    /// in an earlier request, libcurl's <c>NTLMSTATE_LAST</c>: <paramref name="next" /> is
    /// <see langword="null" />, and <paramref name="previous" /> was the Type 3 or the request
    /// before already followed one (<paramref name="wentOutBefore" />).
    /// </summary>
    private static bool SendsNothingAfterNtlmType3(bool wentOutBefore, string? previous, string? next) =>
        next is null && (wentOutBefore || (previous is not null && IsNtlmType3(previous)));

    /// <summary>The start of every NTLM Type 3 value, as <see cref="NtlmType1Prefix" /> is of Type 1.</summary>
    private const string NtlmType3Prefix = "NTLM TlRMTVNTUAADAAAA";
}
