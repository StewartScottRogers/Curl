using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One client connection to a <see cref="GopherServerConnector"/>, answering as sws does in its
/// Gopher mode: the request ends at its first CRLF, the reply is the <c>&lt;reply&gt;</c> part the
/// selector picks, sent as written with no headers, and the connection closes after it.
/// </summary>
/// <remarks>
/// As sws's <c>ProcessRequest</c> does, the test number is read after the selector's last slash,
/// past any characters that are not digits; a number over 10000 picks <c>&lt;dataN&gt;</c>, where N
/// is its last four digits, and anything else <c>&lt;data&gt;</c>, read as sws's <c>getpart</c> reads it.
/// Every byte the client writes up to the end of the request is recorded; bytes after it are dropped.
/// A read before the request is complete waits for the client's next write; once the reply is read,
/// reads return 0.
/// </remarks>
internal sealed class GopherServerConnection(UpstreamTestCase testCase, SwsServerRecording recording) : IConnection
{
    private readonly Lock gate = new();

    private readonly List<byte> request = [];

    private readonly List<byte> unreadReply = [];

    private TaskCompletionSource nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool answered;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint { get; init; }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Task written;
        lock (gate)
        {
            if (answered)
            {
                int count = Math.Min(buffer.Length, unreadReply.Count);
                CollectionsMarshal.AsSpan(unreadReply)[..count].CopyTo(buffer.Span);
                unreadReply.RemoveRange(0, count);
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
            if (!answered)
            {
                ReadRequest(buffer.Span);
            }

            written = nextWrite;
            nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        written.TrySetResult();
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void ReadRequest(ReadOnlySpan<byte> received)
    {
        int searchFrom = Math.Max(0, request.Count - 1);
        request.AddRange(received);
        int end = CollectionsMarshal.AsSpan(request)[searchFrom..].IndexOf("\r\n"u8);
        if (end < 0)
        {
            recording.Record(received);
            return;
        }

        int requestLength = searchFrom + end + 2;
        recording.Record(received[..(received.Length - (request.Count - requestLength))]);
        answered = true;
        unreadReply.AddRange(Reply(CollectionsMarshal.AsSpan(request)[..requestLength]));
    }

    private byte[] Reply(ReadOnlySpan<byte> selector)
    {
        int partNumber = PartNumber(selector);
        return testCase.Find("reply", partNumber == 0 ? "data" : $"data{partNumber}") is { } part
            ? UpstreamTestPartBodies.WithoutFinalNewline(UpstreamTestPartBodies.Decoded(part), part)
            : [];
    }

    // sws: strrchr '/', skip non-digits, strtol; a test number over 10000 carries the part number,
    // and a selector with no slash names no test. The selector ends in CRLF, so the digits end before it.
    private static int PartNumber(ReadOnlySpan<byte> selector)
    {
        int slash = selector.LastIndexOf((byte)'/');
        if (slash < 0)
        {
            return 0;
        }

        ReadOnlySpan<byte> afterSlash = selector[(slash + 1)..];
        ReadOnlySpan<byte> digits = afterSlash[Math.Max(0, afterSlash.IndexOfAny(Digits))..];
        digits = digits[..digits.IndexOfAnyExcept(Digits)];
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number > 10000
            ? (int)(number % 10000)
            : 0;
    }

    private static readonly SearchValues<byte> Digits = SearchValues.Create("0123456789"u8);
}
