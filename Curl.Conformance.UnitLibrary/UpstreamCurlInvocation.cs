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
public sealed class UpstreamCurlInvocation(
    IReadOnlyList<string> arguments,
    Stream standardOutput,
    Stream standardError,
    Stream standardInput,
    IConnector connector,
    IDatagramConnector datagramConnector)
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
}
