namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="IOException" /> a secure <see cref="IConnection" /> throws when a read
/// finds the TLS connection ended without the server's <c>close_notify</c>, carrying the
/// message the platform's curl build prints for it (ADR-0157, ADR-0221).
/// </summary>
/// <remarks>
/// Every curl build fails such a read with exit 56 (<see cref="CurlExitCode.RecvError" />),
/// whether or not the transfer knew how many bytes were still to come; a read that ends at
/// the server's <c>close_notify</c> returns 0 instead. Protocol handlers report the message
/// as the transfer's exit 56 failure.
/// </remarks>
/// <param name="message">
/// The message curl prints, such as
/// <c>schannel: server closed abruptly (missing close_notify)</c>.
/// </param>
public sealed class MissingCloseNotifyException(string message) : IOException(message);
