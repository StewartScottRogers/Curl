using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One client connection to a <see cref="LineProtocolServerConnector"/>. The greeting waits to be
/// read from the start; every complete command line the client writes is answered at once, and
/// the reply waits to be read. A read with nothing waiting waits for the client's next write, as
/// <c>ftpserver.pl</c> blocks reading a command, and returns 0 once the server has closed.
/// </summary>
/// <remarks>
/// Bytes are recorded as they are written while the server still reads them; once a reply has
/// closed the connection, the rest of that write and every later one are dropped unrecorded.
/// </remarks>
internal sealed class LineProtocolServerConnection : IConnection
{
    private readonly ILineProtocolResponder responder;

    private readonly SwsServerRecording recording;

    private readonly Lock gate = new();

    private readonly List<byte> unreadReplyBytes = [];

    private readonly List<byte> unansweredLineBytes = [];

    private TaskCompletionSource nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool closed;

    public LineProtocolServerConnection(ILineProtocolResponder responder, SwsServerRecording recording)
    {
        this.responder = responder;
        this.recording = recording;
        unreadReplyBytes.AddRange(responder.Greeting.Span);
    }

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint { get; init; }

    public EndPoint? LocalEndPoint { get; init; }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Task written;
        lock (gate)
        {
            if (unreadReplyBytes.Count > 0 || closed)
            {
                int count = Math.Min(buffer.Length, unreadReplyBytes.Count);
                CollectionsMarshal.AsSpan(unreadReplyBytes)[..count].CopyTo(buffer.Span);
                unreadReplyBytes.RemoveRange(0, count);
                return count;
            }

            written = nextWrite.Task;
        }

        await written.WaitAsync(cancellationToken);
        return await ReadAsync(buffer, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        TaskCompletionSource written;
        lock (gate)
        {
            ReadCommandLines(buffer.Span);
            written = nextWrite;
            nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        written.TrySetResult();
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Records and answers the written bytes line by line, up to the end of the write or a close.
    private void ReadCommandLines(ReadOnlySpan<byte> written)
    {
        while (!closed && !written.IsEmpty)
        {
            int lineFeed = written.IndexOf((byte)'\n');
            int taken = lineFeed < 0 ? written.Length : lineFeed + 1;
            recording.Record(written[..taken]);
            unansweredLineBytes.AddRange(written[..taken]);
            written = written[taken..];
            AnswerCompleteLine();
        }
    }

    // A line ends at CRLF, as ftpserver.pl reads it; a lone line feed stays part of the line.
    private void AnswerCompleteLine()
    {
        int count = unansweredLineBytes.Count;
        if (count < 2 || unansweredLineBytes[count - 1] != '\n' || unansweredLineBytes[count - 2] != '\r')
        {
            return;
        }

        string commandLine = Encoding.Latin1.GetString([.. unansweredLineBytes], 0, count - 2);
        unansweredLineBytes.Clear();
        LineProtocolReply reply = responder.Answer(commandLine);
        unreadReplyBytes.AddRange(reply.Bytes.Span);
        closed = reply.ClosesConnection;
    }
}
