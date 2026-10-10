using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One run of curl that <see cref="UpstreamCaseRunner"/> asks for: the arguments, the standard
/// streams, and the connectors every connection goes through, so the run stays in process.
/// </summary>
/// <param name="arguments">The command-line arguments, without the program name.</param>
/// <param name="standardOutput">Where curl writes standard output.</param>
/// <param name="standardError">Where curl writes standard error.</param>
/// <param name="standardInput">What curl reads as standard input.</param>
/// <param name="connector">Every TCP connection; an in-memory test server.</param>
/// <param name="datagramConnector">Every UDP channel.</param>
/// <param name="environmentVariables">
/// The whole environment the run reads, as the case's <c>&lt;client&gt;&lt;setenv&gt;</c> sets it;
/// <see langword="null"/> for none.
/// </param>
public sealed class UpstreamCurlInvocation(
    IReadOnlyList<string> arguments,
    Stream standardOutput,
    Stream standardError,
    Stream standardInput,
    IConnector connector,
    IDatagramConnector datagramConnector,
    IReadOnlyDictionary<string, string>? environmentVariables = null)
{
    /// <summary>The command-line arguments, without the program name.</summary>
    public IReadOnlyList<string> Arguments { get; } = arguments;

    /// <summary>Where curl writes standard output.</summary>
    public Stream StandardOutput { get; } = standardOutput;

    /// <summary>Where curl writes standard error.</summary>
    public Stream StandardError { get; } = standardError;

    /// <summary>What curl reads as standard input.</summary>
    public Stream StandardInput { get; } = standardInput;

    /// <summary>Every TCP connection; an in-memory test server.</summary>
    public IConnector Connector { get; } = connector;

    /// <summary>Every UDP channel.</summary>
    public IDatagramConnector DatagramConnector { get; } = datagramConnector;

    /// <summary>Gets the listener curl's active-mode FTP (<c>-P</c>) listens on: the FTP stand-in's in-memory one, which <c>PORT</c> and <c>EPRT</c> connect to; <see langword="null"/> for curl's own TCP listener.</summary>
    public IConnectionListener? ConnectionListener { get; init; }

    /// <summary>
    /// The whole environment the run reads, as the case's <c>&lt;client&gt;&lt;setenv&gt;</c> sets it;
    /// a variable not in it is unset for the run.
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; } = environmentVariables ?? new Dictionary<string, string>(StringComparer.Ordinal);
}
