using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One client connection to an <see cref="RtspServerConnector"/>, answering as upstream's <c>rtspd</c>
/// (<c>tests/server/rtspd.c</c> at <c>curl-8_22_0</c>) does: each request ends at its empty line and the
/// <c>Content-Length</c> bytes after it, and is answered with the <c>&lt;reply&gt;</c> part its path picks,
/// followed by the interleaved RTP packets <see cref="RtspInterleavedPackets"/> builds for that part.
/// </summary>
/// <remarks>
/// As rtspd reads it, the test number is the number after the request path's last slash, past any characters
/// that are not digits; a number over 10000 picks <c>&lt;dataN&gt;</c>, where N is its last four digits, and
/// anything else <c>&lt;data&gt;</c>. The connection stays open for the next request unless the reply holds
/// <c>swsclose</c> or is empty: then, once the reply is read, reads return 0 and later writes are dropped.
/// Every request's bytes are recorded, in the order they complete. A read with nothing to answer waits for the
/// client's next write, as rtspd blocks reading the next request.
/// </remarks>
internal sealed class RtspServerConnection(UpstreamTestCase testCase, SwsServerRecording recording) : IConnection
{
    private const int PartNumberBase = 10000;

    private static readonly SearchValues<byte> Digits = SearchValues.Create("0123456789"u8);

    private readonly Lock gate = new();

    private readonly List<byte> pending = [];

    private readonly List<byte> unreadReply = [];

    private TaskCompletionSource nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool closing;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint { get; init; }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Task written;
        lock (gate)
        {
            if (unreadReply.Count > 0 || closing)
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
            if (!closing)
            {
                pending.AddRange(buffer.Span);
                AnswerCompleteRequests();
            }

            written = nextWrite;
            nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        written.TrySetResult();
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void AnswerCompleteRequests()
    {
        while (!closing && RequestLength(CollectionsMarshal.AsSpan(pending)) is { } length)
        {
            byte[] request = [.. pending.GetRange(0, length)];
            pending.RemoveRange(0, length);
            recording.Record(request);
            Answer(request);
        }
    }

    private void Answer(byte[] request)
    {
        int partNumber = PartNumber(request);
        byte[] reply = Reply(partNumber);
        unreadReply.AddRange(reply);
        unreadReply.AddRange(RtspInterleavedPackets.ForPart((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span, partNumber));
        closing = reply.Length == 0 || reply.AsSpan().IndexOf("swsclose"u8) >= 0;
    }

    private byte[] Reply(int partNumber) =>
        testCase.Find("reply", partNumber == 0 ? "data" : $"data{partNumber.ToString(CultureInfo.InvariantCulture)}") is { } part
            ? UpstreamTestPartBodies.WithoutFinalNewline(UpstreamTestPartBodies.Decoded(part), part)
            : [];

    /// <summary>
    /// The length of the first complete request in <paramref name="received"/>: its headers through the empty
    /// line, then the <c>Content-Length</c> its headers give; <see langword="null"/> while it is incomplete.
    /// </summary>
    /// <param name="received">The bytes the client has written and no request has taken yet.</param>
    /// <returns>The request's length, or <see langword="null"/>.</returns>
    internal static int? RequestLength(ReadOnlySpan<byte> received)
    {
        int end = received.IndexOf("\r\n\r\n"u8);
        if (end < 0)
        {
            return null;
        }

        int length = end + 4 + ContentLength(Encoding.Latin1.GetString(received[..end]));
        return received.Length >= length ? length : null;
    }

    // rtspd reads the first Content-Length: header, in any letter case, as a number after blanks.
    private static int ContentLength(string headers) =>
        headers.Split("\r\n").FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) is { } header
            && int.TryParse(header["Content-Length:".Length..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int length)
            ? length
            : 0;

    /// <summary>
    /// The part number the request's path picks, as rtspd reads it from <c>METHOD path PROTOCOL/x.y</c>: the number
    /// after the path's last slash, past any characters that are not digits, modulo 10000 when it is over 10000,
    /// and 0 otherwise or when the path has no slash.
    /// </summary>
    /// <param name="request">The request's bytes.</param>
    /// <returns>The part number.</returns>
    internal static int PartNumber(ReadOnlySpan<byte> request)
    {
        string[] words = Encoding.Latin1.GetString(request[..request.IndexOf("\r\n"u8)]).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string path = words.Length > 1 ? words[1] : string.Empty;
        int slash = path.LastIndexOf('/');
        ReadOnlySpan<byte> afterSlash = slash < 0 ? [] : Encoding.Latin1.GetBytes(path[(slash + 1)..]);
        ReadOnlySpan<byte> digits = afterSlash[Math.Max(0, afterSlash.IndexOfAny(Digits))..];
        int digitsEnd = digits.IndexOfAnyExcept(Digits);
        digits = digitsEnd < 0 ? digits : digits[..digitsEnd];
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number > PartNumberBase
            ? (int)(number % PartNumberBase)
            : 0;
    }
}
