using System.Net;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The settings of the curl tool that one transfer's setup and context read and that libcurl's easy
/// options have no <c>CURLOPT_*</c> for, or that the engine reads here as the tool resolved them: the host
/// (<c>Curl.Console</c>, from the command line) builds it and hands it to the engine (ADR-0539).
/// </summary>
/// <param name="Silent"><c>-s</c>: hides curl's progress and warnings.</param>
/// <param name="ShowError"><c>-S</c>: shows an error under <c>-s</c>.</param>
/// <param name="FailMode"><c>--fail</c> and <c>--fail-with-body</c>: what the HTTP handler does with a status of 400 or more.</param>
/// <param name="CreateFileMode"><c>--create-file-mode</c>, or <see langword="null" /> for the handlers' default.</param>
/// <param name="Insecure"><c>-k</c>: no host or certificate check, and no known-hosts check for SSH.</param>
/// <param name="Passphrase"><c>--pass</c>, or <see langword="null" />.</param>
/// <param name="Engine"><c>--engine</c>, or <see langword="null" />.</param>
/// <param name="EchModeIsMalformed">Whether <c>--ech</c> gave a mode libcurl refuses.</param>
/// <param name="PostData">The <c>-d</c> body as the tool resolved it, or <see langword="null" />.</param>
/// <param name="ProxyCredentials"><c>-U</c>, or <see langword="null" />.</param>
/// <param name="AllowedProtocols"><c>--proto</c>, or <see langword="null" /> for curl's default.</param>
/// <param name="AllowedRedirectProtocols"><c>--proto-redir</c>, or <see langword="null" /> for curl's default.</param>
/// <param name="SslLevel">How strictly the FTP and mail handlers require TLS (<c>--ssl</c>, <c>--ssl-reqd</c> and their kin).</param>
/// <param name="FtpSslControlOnly"><c>--ftp-ssl-control</c>.</param>
/// <param name="SslRequired"><c>--ssl-reqd</c>.</param>
/// <param name="ListOnly"><c>-l</c> / <c>--list-only</c>.</param>
/// <param name="UploadFlags"><c>--upload-flags</c>.</param>
/// <param name="ProxyHeaders"><c>--proxy-header</c> lines.</param>
/// <param name="ProxyTunnel"><c>-p</c>: tunnel through the HTTP proxy.</param>
/// <param name="OverUnixSocket">Whether <c>--unix-socket</c> or <c>--abstract-unix-socket</c> was given.</param>
/// <param name="HappyEyeballsTimeout">How long a <c>--http3</c> transfer's QUIC handshake runs alone.</param>
/// <param name="AppendUrlQuery">Appends the <c>-G</c> / <c>--url-query</c> query to a URL.</param>
public sealed record EngineTransferSettings(
    bool Silent,
    bool ShowError,
    HttpFailMode FailMode,
    UnixFileMode? CreateFileMode,
    bool Insecure,
    string? Passphrase,
    string? Engine,
    bool EchModeIsMalformed,
    ReadOnlyMemory<byte>? PostData,
    NetworkCredential? ProxyCredentials,
    IReadOnlySet<string>? AllowedProtocols,
    IReadOnlySet<string>? AllowedRedirectProtocols,
    TransportSecurityLevel SslLevel,
    bool FtpSslControlOnly,
    bool SslRequired,
    bool ListOnly,
    ImapUploadFlags UploadFlags,
    IReadOnlyList<string> ProxyHeaders,
    bool ProxyTunnel,
    bool OverUnixSocket,
    TimeSpan HappyEyeballsTimeout,
    Func<string, string> AppendUrlQuery);
