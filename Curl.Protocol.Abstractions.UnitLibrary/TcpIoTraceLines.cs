namespace Curl.Protocol.Abstractions;

/// <summary>
/// How a connection's I/O is written as curl 8.21.0's TCP filter lines under
/// <c>--trace-config tcp</c>, <c>network</c> or <c>all</c>: <c>[&lt;FilterName&gt;] send(len=&lt;n&gt;) -&gt; 0, &lt;n&gt;</c>
/// after each write and <c>[&lt;FilterName&gt;] recv(len=&lt;length&gt;) -&gt; 0, &lt;n&gt;</c> after each
/// read (ADR-0357's BL-1259 amendment).
/// </summary>
/// <param name="FilterName">
/// The filter's name in the lines: <c>TCP</c> for a transfer's first connection, <c>TCP-1</c> for its
/// second, such as FTP's data connection.
/// </param>
/// <param name="ReceiveLength">
/// The length every <c>recv</c> line gives, such as FTP's control connection's 900; or
/// <see langword="null" /> to give the length of each read's buffer, for a reader that sizes its
/// reads as curl does.
/// </param>
/// <param name="WritesWouldBlockReads">
/// <see langword="true" /> to write a read that cannot complete at once first as curl's would-block
/// result, <c>recv(len=&lt;length&gt;) -&gt; 81, 0</c>; <see langword="false" /> to write only the
/// read's result.
/// </param>
public sealed record TcpIoTraceLines(string FilterName, int? ReceiveLength, bool WritesWouldBlockReads);
