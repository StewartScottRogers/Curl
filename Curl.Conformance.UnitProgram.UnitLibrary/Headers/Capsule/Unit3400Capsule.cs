using Curl.Conformance.UnitProgram.Hashes;
using Curl.Networking;
using DecodeStatus = Curl.Networking.CapsuleDatagramChannel.DecodeStatus;

namespace Curl.Conformance.UnitProgram.Headers.Capsule;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit3400.c</c>: <c>capsule_encap_udp_hdr</c>
/// (<see cref="CapsuleDatagramChannel.BuildCapsuleHeader" />), <c>Curl_capsule_encap_udp_datagram</c>
/// (<see cref="CapsuleDatagramChannel.BuildCapsule" />) and <c>Curl_capsule_process_udp_raw</c>
/// (<see cref="CapsuleDatagramChannel.DecodeDatagram" />), check for check: the header at the
/// variable-length-integer boundaries, an encode and decode round trip, the decode paths and a
/// sequential decode of two capsules from one buffer.
/// </summary>
/// <remarks>
/// The C program's <c>bufq</c> is a byte array here, from whose start each decode drops the bytes it
/// consumed. Curl carries HTTP/3 through a proxy on every platform, so every check runs everywhere,
/// as in a curl build with <c>USE_PROXY_HTTP3</c> and <c>USE_NGTCP2</c> (ADR-0726).
/// </remarks>
public sealed class Unit3400Capsule : IUnitProgram
{
    /// <summary>The C program's <c>check_capsule_hdr</c> rows: payload length and the header expected.</summary>
    private static readonly (int PayloadLength, byte[] Header)[] HeaderRows =
    [
        (0, [0x00, 0x01, 0x00]),
        (62, [0x00, 0x3F, 0x00]),
        (63, [0x00, 0x40, 0x40, 0x00]),
        (64, [0x00, 0x40, 0x41, 0x00]),
        (16382, [0x00, 0x7F, 0xFF, 0x00]),
        (16383, [0x00, 0x80, 0x00, 0x40, 0x00, 0x00]),
        (16384, [0x00, 0x80, 0x00, 0x40, 0x01, 0x00]),
    ];

    /// <summary>The C program's <c>check_capsule_result</c> rows: queued bytes, output length, error and bytes read expected.</summary>
    private static readonly (byte[] Capsule, int OutputLength, DecodeStatus Status)[] ResultRows =
    [
        ([], 0, DecodeStatus.EmptyDestination),
        ([], 8, DecodeStatus.NeedMoreBytes),
        ([0x01], 8, DecodeStatus.Malformed),
        ([0x00, 0x40], 8, DecodeStatus.NeedMoreBytes),
        ([0x00, 0x01, 0x01], 8, DecodeStatus.Malformed),
        ([0x00, 0x00, 0x00], 8, DecodeStatus.Malformed),
        ([0x00, 0x04, 0x00, 0x11, 0x22], 8, DecodeStatus.NeedMoreBytes),
    ];

    private static readonly byte[] Payload3Bytes = [0x00, 0x04, 0x00, 0x11, 0x22, 0x33];

    /// <inheritdoc />
    public string ProgramName => "unit3400";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        CheckHeaderBoundaries(checks);
        CheckEncodeDecodeRoundTrip(checks);
        CheckDecodePaths(checks);
        CheckSequentialDecode(checks);
        return Task.FromResult(checks.Result);
    }

    // test_capsule_encap_udp_hdr_boundaries
    private static void CheckHeaderBoundaries(UnitChecks checks)
    {
        foreach (var (payloadLength, expected) in HeaderRows)
        {
            byte[] header = CapsuleDatagramChannel.BuildCapsuleHeader(payloadLength);
            checks.FailUnless(header.Length == expected.Length, "capsule header length mismatch");
            checks.FailUnless(header.AsSpan().SequenceEqual(expected), "capsule header bytes mismatch");
        }
    }

    // test_capsule_encode_decode_roundtrip
    private static void CheckEncodeDecodeRoundTrip(UnitChecks checks)
    {
        byte[] payload = new byte[128];
        for (int i = 0; i < payload.Length; ++i)
        {
            payload[i] = (byte)i;
        }

        foreach (int payloadLength in (int[])[7, 64])
        {
            byte[] queue = CapsuleDatagramChannel.BuildCapsule(payload.AsSpan(0, payloadLength));
            byte[] output = new byte[128];
            int read = Process(ref queue, output, output.Length, out DecodeStatus status);
            checks.FailUnless(status == DecodeStatus.Decoded, "failed to decode UDP datagram");
            checks.FailUnless(read == payloadLength, "decoded payload length mismatch");
            checks.FailUnless(output.AsSpan(0, payloadLength).SequenceEqual(payload.AsSpan(0, payloadLength)), "decoded payload bytes mismatch");
            checks.FailUnless(queue.Length == 0, "decoded capsule must be consumed");
        }
    }

    // test_capsule_decode_paths
    private static void CheckDecodePaths(UnitChecks checks)
    {
        byte[] output = new byte[8];
        foreach (var (capsule, outputLength, expected) in ResultRows)
        {
            byte[] queue = capsule;
            int read = Process(ref queue, output, outputLength, out DecodeStatus status);
            checks.FailUnless(status == expected, "unexpected capsule error");
            checks.FailUnless(read == 0, "unexpected capsule read size");
        }

        byte[] oversized = Payload3Bytes;
        int oversizedRead = Process(ref oversized, output, 2, out DecodeStatus oversizedStatus);
        checks.FailUnless(oversizedStatus == DecodeStatus.Malformed, "expected RECV_ERROR for short output buffer");
        checks.FailUnless(oversizedRead == 0, "expected zero read on short output buffer");
        checks.FailUnless(oversized.Length == 0, "oversized capsule must be discarded");

        byte[] empty = [0x00, 0x01, 0x00];
        int emptyRead = Process(ref empty, output, output.Length, out DecodeStatus emptyStatus);
        checks.FailUnless(emptyStatus == DecodeStatus.Decoded, "zero-length UDP payload should succeed");
        checks.FailUnless(emptyRead == 0, "zero-length UDP payload should read zero");
        checks.FailUnless(empty.Length == 0, "zero-length capsule must be consumed");

        byte[] normal = Payload3Bytes;
        Array.Clear(output);
        int normalRead = Process(ref normal, output, output.Length, out DecodeStatus normalStatus);
        checks.FailUnless(normalStatus == DecodeStatus.Decoded, "payload decode should succeed");
        checks.FailUnless(normalRead == 3, "payload decode size mismatch");
        checks.FailUnless(output.AsSpan(0, 3).SequenceEqual((byte[])[0x11, 0x22, 0x33]), "payload decode bytes mismatch");
        checks.FailUnless(normal.Length == 0, "payload capsule must be consumed");
    }

    // test_capsule_sequential_decode
    private static void CheckSequentialDecode(UnitChecks checks)
    {
        byte[] queue = [0x00, 0x04, 0x00, 0x11, 0x22, 0x33, 0x00, 0x04, 0x00, 0xAA, 0xBB, 0xCC];
        byte[] output = new byte[8];

        int firstRead = Process(ref queue, output, output.Length, out DecodeStatus firstStatus);
        checks.FailUnless(firstStatus == DecodeStatus.Decoded, "sequential: first capsule decode failed");
        checks.FailUnless(firstRead == 3, "sequential: first capsule size mismatch");
        checks.FailUnless(output.AsSpan(0, 3).SequenceEqual((byte[])[0x11, 0x22, 0x33]), "sequential: first capsule bytes mismatch");

        Array.Clear(output);
        int secondRead = Process(ref queue, output, output.Length, out DecodeStatus secondStatus);
        checks.FailUnless(secondStatus == DecodeStatus.Decoded, "sequential: second capsule decode failed");
        checks.FailUnless(secondRead == 3, "sequential: second capsule size mismatch");
        checks.FailUnless(output.AsSpan(0, 3).SequenceEqual((byte[])[0xAA, 0xBB, 0xCC]), "sequential: second capsule bytes mismatch");
        checks.FailUnless(queue.Length == 0, "sequential: buffer must be empty after two capsules");

        int lastRead = Process(ref queue, output, output.Length, out DecodeStatus lastStatus);
        checks.FailUnless(lastStatus == DecodeStatus.NeedMoreBytes, "sequential: empty queue should return AGAIN");
        checks.FailUnless(lastRead == 0, "sequential: empty queue should read zero bytes");
    }

    // Curl_capsule_process_udp_raw over the queue: decodes into the first outputLength bytes of output and drops what it consumed.
    private static int Process(ref byte[] queue, byte[] output, int outputLength, out DecodeStatus status)
    {
        status = CapsuleDatagramChannel.DecodeDatagram(queue, output.AsSpan(0, outputLength), out int consumed, out int read);
        queue = queue[consumed..];
        return read;
    }
}
