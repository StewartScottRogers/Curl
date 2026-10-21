using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that emulates upstream's SMB test server, <c>smbserver.py</c>
/// (<c>tests/smbserver.py</c> at <c>curl-8_22_0</c>), on <see cref="SmbPort"/>, and hands every
/// other connection to the server it wraps. No socket is opened.
/// </summary>
/// <remarks>
/// Each connection is an <see cref="SmbServerConnection"/>, serving the share <c>TESTS</c> whose
/// every file holds the case's <c>&lt;reply&gt;&lt;data&gt;</c> (empty when the case has none), as
/// upstream's server serves <c>TESTS/&lt;n&gt;</c> from that part.
/// </remarks>
/// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
/// <param name="backend">The server every connection not to <see cref="SmbPort"/> reaches.</param>
public sealed class SmbServerConnector(UpstreamTestCase testCase, IConnector backend) : IConnector
{
    /// <summary>The port of upstream's SMB server, <c>%SMBPORT</c>.</summary>
    public const int SmbPort = 9020;

    /// <summary>The <c>&lt;server&gt;</c> name whose cases this stand-in lets run.</summary>
    public static IReadOnlyList<string> EmulatedServers { get; } = ["smb"];

    private readonly byte[] fileContent = testCase.Find("reply", "data") is { } data ? UpstreamTestPartBodies.Decoded(data) : [];

    /// <summary>Opens an in-memory connection to the SMB emulation when <paramref name="target"/>'s port is <see cref="SmbPort"/>, otherwise a connection to the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the wrapped server.</param>
    /// <returns>The connection's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == SmbPort
            ? ValueTask.FromResult(ConnectResult.Connected(new SmbServerConnection(fileContent)
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, SmbPort),
            }))
            : backend.ConnectAsync(target, cancellationToken);
}
