using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Protocols;

namespace Curl.Api;

/// <summary>
/// The HTTP, authentication and per-protocol option records one <see cref="CurlClientOptions"/> or
/// <see cref="CurlRequest"/> carries, each <see langword="null"/> when not set.
/// </summary>
internal readonly record struct CurlProtocolRecords(
    CurlHttpOptions? Http,
    CurlAuthenticationOptions? Authentication,
    CurlFtpOptions? Ftp,
    CurlSshOptions? Ssh,
    CurlMailOptions? Mail,
    CurlTftpOptions? Tftp,
    CurlTelnetOptions? Telnet,
    CurlRtspOptions? Rtsp);

/// <summary>
/// Applies the HTTP, authentication and protocol option records, first the client's then a request's,
/// to an easy handle's <see cref="EasyOptions"/>. A record left <see langword="null"/> leaves its group
/// as it was, so a request's record wins over the client's and a request without one keeps the client's.
/// </summary>
internal static class CurlProtocolOptions
{
    /// <summary><paramref name="options"/> with each record that is set applied to its group.</summary>
    /// <param name="options">The handle's options so far.</param>
    /// <param name="records">The records to apply.</param>
    /// <returns>The options with the records applied.</returns>
    public static EasyOptions Apply(EasyOptions options, CurlProtocolRecords records) =>
        options with
        {
            Http = ApplyHttp(options.Http, records.Http),
            Authentication = Pick(records.Authentication, options.Authentication),
            Protocols = ApplyProtocols(options.Protocols, records),
        };

    private static EasyHttpOptions ApplyHttp(EasyHttpOptions current, CurlHttpOptions? http) =>
        http is null ? current : http.ApplyTo(current);

    private static EasyProtocolOptions ApplyProtocols(EasyProtocolOptions protocols, CurlProtocolRecords records) =>
        protocols with
        {
            Ftp = Pick(records.Ftp, protocols.Ftp),
            Ssh = Pick(records.Ssh, protocols.Ssh),
            Smtp = Pick(records.Mail, protocols.Smtp),
            Tftp = Pick(records.Tftp, protocols.Tftp),
            Telnet = Pick(records.Telnet, protocols.Telnet),
            Rtsp = Pick(records.Rtsp, protocols.Rtsp),
        };

    private static TEasy Pick<TEasy>(IEasyOptionRecord<TEasy>? record, TEasy current) =>
        record is null ? current : record.ToEasy();
}
