namespace Curl.Conformance;

/// <summary>
/// One protocol's answers on one connection to a <see cref="LineProtocolServerConnector"/>, the
/// part of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) that differs between FTP,
/// SMTP, IMAP and POP3: the greeting sent on connect and the reply to each command line.
/// </summary>
internal interface ILineProtocolResponder
{
    /// <summary>Gets the bytes the server sends as soon as the client connects; empty when it sends nothing first.</summary>
    ReadOnlyMemory<byte> Greeting { get; }

    /// <summary>Answers one command line the client sent.</summary>
    /// <param name="commandLine">The line, without its CRLF, decoded as Latin-1.</param>
    /// <returns>The reply to send, and whether the server closes the connection after it.</returns>
    LineProtocolReply Answer(string commandLine);
}
