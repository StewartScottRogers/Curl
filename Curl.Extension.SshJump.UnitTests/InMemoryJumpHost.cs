using System.Buffers.Binary;
using System.Text;
using Curl.Conformance.SshServer;
using Curl.Protocol.Abstractions;
using Curl.Ssh;

namespace Curl.Extension.SshJump;

/// <summary>
/// A jump host in memory: curl's connector stand-in, whose every connection is answered by the conformance SSH
/// server's transport and user authentication (user <c>curltest</c>, password <c>curltest-password</c>), after
/// which <see cref="Channel" /> runs the server's side of the channel open (RFC 4254 section 7.2) and beyond.
/// </summary>
/// <param name="channel">The server's side once the client is authenticated: given the transport and the client's <c>SSH_MSG_CHANNEL_OPEN</c> payload.</param>
internal sealed class InMemoryJumpHost(Func<SshServerTransport, byte[], Task> channel) : IConnector
{
    /// <summary>The channel number the server gives its end of the channel.</summary>
    internal const uint ServerChannel = 7;

    private readonly List<ConnectTarget> targets = [];

    /// <summary>Gets the targets curl's connector was asked to reach.</summary>
    internal IReadOnlyList<ConnectTarget> Targets => targets;

    /// <summary>Gets the server's session, which completes once <see cref="Channel" /> has run.</summary>
    internal Task? Session { get; private set; }

    /// <summary>Gets the client's <c>SSH_MSG_CHANNEL_OPEN</c> payload once it has arrived.</summary>
    internal byte[]? ChannelOpen { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<string> HttpOverTlsApplicationProtocols { get; init; } = ["http/1.1"];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        targets.Add(target);
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        Session = RunAsync(new SshServerTransport(server, new SystemSshRandomSource()), server);
        return ValueTask.FromResult(ConnectResult.Connected(client));
    }

    /// <summary>Gives an <c>SSH_MSG_CHANNEL_OPEN_CONFIRMATION</c> for the client's channel 0 (RFC 4254 section 5.1).</summary>
    /// <param name="window">The server's initial window.</param>
    /// <param name="maximumPacketSize">The server's maximum packet size.</param>
    /// <returns>The payload.</returns>
    internal static byte[] Confirmation(uint window = 1 << 20, uint maximumPacketSize = 32768) =>
        [91, .. UInt32(0), .. UInt32(ServerChannel), .. UInt32(window), .. UInt32(maximumPacketSize)];

    /// <summary>Gives an <c>SSH_MSG_CHANNEL_OPEN_FAILURE</c> for the client's channel 0 (RFC 4254 section 5.1).</summary>
    /// <param name="reasonCode">The reason code, such as 2 for <c>SSH_OPEN_CONNECT_FAILED</c>.</param>
    /// <returns>The payload.</returns>
    internal static byte[] Failure(uint reasonCode) =>
        [92, .. UInt32(0), .. UInt32(reasonCode), .. String("connect failed"), .. String(string.Empty)];

    /// <summary>Encodes a <c>uint32</c> (RFC 4251 section 5).</summary>
    /// <param name="value">The value.</param>
    /// <returns>Its four bytes, most significant first.</returns>
    internal static byte[] UInt32(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    /// <summary>Encodes a <c>string</c> (RFC 4251 section 5).</summary>
    /// <param name="text">The text, as UTF-8.</param>
    /// <returns>Its length and bytes.</returns>
    internal static byte[] String(string text) => String(Encoding.UTF8.GetBytes(text));

    /// <summary>Encodes a <c>string</c> (RFC 4251 section 5).</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>Their length and the bytes.</returns>
    internal static byte[] String(byte[] bytes) => [.. UInt32((uint)bytes.Length), .. bytes];

    private async Task RunAsync(SshServerTransport transport, SshServerDuplexConnection server)
    {
        await transport.ExchangeIdentificationAsync(CancellationToken.None);
        await transport.ExchangeKeysAsync(CancellationToken.None);
        await transport.AcceptServiceRequestAsync(CancellationToken.None);
        await SshServerUserAuthentication.AuthenticateAsync(transport, CancellationToken.None);
        ChannelOpen = await transport.ReadPacketAsync(CancellationToken.None);
        await channel(transport, ChannelOpen);
        await server.DisposeAsync();
    }
}
