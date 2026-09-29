namespace Curl.Protocol.Ssh.Connection;

/// <summary>
/// The connection-protocol message numbers of RFC 4254 section 9 that this library reads
/// or writes.
/// </summary>
internal static class SshConnectionMessageNumber
{
    /// <summary><c>SSH_MSG_GLOBAL_REQUEST</c>: a request about the whole connection, such as OpenSSH's keep-alive.</summary>
    internal const byte GlobalRequest = 80;

    /// <summary><c>SSH_MSG_REQUEST_FAILURE</c>: the answer to a global request the receiver does not grant.</summary>
    internal const byte RequestFailure = 82;

    /// <summary><c>SSH_MSG_CHANNEL_OPEN</c>: the client asks for a channel.</summary>
    internal const byte ChannelOpen = 90;

    /// <summary><c>SSH_MSG_CHANNEL_OPEN_CONFIRMATION</c>: the server grants the channel, with its own number, window and packet size.</summary>
    internal const byte ChannelOpenConfirmation = 91;

    /// <summary><c>SSH_MSG_CHANNEL_OPEN_FAILURE</c>: the server refuses the channel.</summary>
    internal const byte ChannelOpenFailure = 92;

    /// <summary><c>SSH_MSG_CHANNEL_WINDOW_ADJUST</c>: the sender may be sent that many more bytes.</summary>
    internal const byte ChannelWindowAdjust = 93;

    /// <summary><c>SSH_MSG_CHANNEL_DATA</c>: bytes of the channel's stream.</summary>
    internal const byte ChannelData = 94;

    /// <summary><c>SSH_MSG_CHANNEL_EXTENDED_DATA</c>: bytes of a side stream, such as the subsystem's standard error.</summary>
    internal const byte ChannelExtendedData = 95;

    /// <summary><c>SSH_MSG_CHANNEL_EOF</c>: the sender sends no more data.</summary>
    internal const byte ChannelEof = 96;

    /// <summary><c>SSH_MSG_CHANNEL_CLOSE</c>: the sender closes the channel.</summary>
    internal const byte ChannelClose = 97;

    /// <summary><c>SSH_MSG_CHANNEL_REQUEST</c>: a request about one channel, such as starting a subsystem.</summary>
    internal const byte ChannelRequest = 98;

    /// <summary><c>SSH_MSG_CHANNEL_SUCCESS</c>: the channel request was granted.</summary>
    internal const byte ChannelSuccess = 99;

    /// <summary><c>SSH_MSG_CHANNEL_FAILURE</c>: the channel request was refused.</summary>
    internal const byte ChannelFailure = 100;
}
