using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Builds and reads the TFTP packets a transfer uses: the read and write requests,
/// DATA packets and acknowledgements it sends (RFC 1350 section 5), and the block size
/// an option acknowledgement carries (RFC 2347, RFC 2348).
/// </summary>
internal static class TftpPackets
{
    /// <summary>The opcode of a read request.</summary>
    internal const ushort ReadRequestOpcode = 1;

    /// <summary>The opcode of a write request.</summary>
    internal const ushort WriteRequestOpcode = 2;

    /// <summary>The opcode of a DATA packet.</summary>
    internal const ushort DataOpcode = 3;

    /// <summary>The opcode of an acknowledgement.</summary>
    internal const ushort AcknowledgementOpcode = 4;

    /// <summary>The opcode of an ERROR packet.</summary>
    internal const ushort ErrorOpcode = 5;

    /// <summary>The opcode of an option acknowledgement.</summary>
    internal const ushort OptionAcknowledgementOpcode = 6;

    /// <summary>
    /// The length of the opcode and block number that precede a DATA packet's payload.
    /// </summary>
    internal const int DataHeaderLength = 4;

    /// <summary>The block size RFC 1350 fixes when no option says otherwise.</summary>
    internal const int DefaultBlockSize = 512;

    /// <summary>The smallest block size RFC 2348 allows.</summary>
    internal const int MinimumBlockSize = 8;

    /// <summary>The largest block size RFC 2348 allows.</summary>
    internal const int MaximumBlockSize = 65464;

    /// <summary>
    /// Gets the <c>blksize</c> a request asks for, or <see langword="null" /> when
    /// <c>--tftp-no-options</c> means the request carries no options at all.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <returns>
    /// <see cref="ITransferContext.TftpBlockSize" /> clamped to 8-65464, as curl 8.21.0
    /// sends it; 512 when it is not given or is 0, as curl sends for
    /// <c>--tftp-blksize 0</c>.
    /// </returns>
    internal static int? RequestedBlockSize(ITransferContext context) =>
        context.TftpNoOptions
            ? null
            : context.TftpBlockSize is null or 0
                ? DefaultBlockSize
                : Math.Clamp(context.TftpBlockSize.Value, MinimumBlockSize, MaximumBlockSize);

    /// <summary>
    /// Builds the read request curl 8.21.0 sends: octet mode, then <c>tsize 0</c>,
    /// <c>blksize</c> and <c>timeout</c>, each string null-terminated, or octet mode
    /// alone when there are no options.
    /// </summary>
    /// <param name="fileName">The file name from the URL path, decoded.</param>
    /// <param name="blockSize">
    /// The <c>blksize</c> to ask for, or <see langword="null" /> to send no options.
    /// </param>
    /// <param name="timeoutSeconds">
    /// The <c>timeout</c> to send: the retry schedule's
    /// <see cref="TftpRetrySchedule.RetrySeconds" />, 6 by default.
    /// </param>
    /// <returns>The whole datagram.</returns>
    internal static byte[] BuildReadRequest(string fileName, int? blockSize, int timeoutSeconds) =>
        BuildRequest(ReadRequestOpcode, fileName, 0, blockSize, timeoutSeconds);

    /// <summary>
    /// Builds the write request curl 8.21.0 sends: octet mode, then <c>tsize</c> with the
    /// upload's length, <c>blksize</c> and <c>timeout</c>, each string null-terminated,
    /// or octet mode alone when there are no options.
    /// </summary>
    /// <param name="fileName">The file name from the URL path, decoded.</param>
    /// <param name="transferSize">
    /// The upload's length in bytes, or 0 when it is not known, as curl sends for an
    /// upload read from a pipe.
    /// </param>
    /// <param name="blockSize">
    /// The <c>blksize</c> to ask for, or <see langword="null" /> to send no options.
    /// </param>
    /// <param name="timeoutSeconds">
    /// The <c>timeout</c> to send: the retry schedule's
    /// <see cref="TftpRetrySchedule.RetrySeconds" />, 6 by default.
    /// </param>
    /// <returns>The whole datagram.</returns>
    internal static byte[] BuildWriteRequest(string fileName, long transferSize, int? blockSize, int timeoutSeconds) =>
        BuildRequest(WriteRequestOpcode, fileName, transferSize, blockSize, timeoutSeconds);

    /// <summary>
    /// Builds the DATA packet that carries block <paramref name="blockNumber" />.
    /// </summary>
    /// <param name="blockNumber">The block's number; the first block is 1.</param>
    /// <param name="payload">The block's bytes, shorter than the block size only in the last block.</param>
    /// <returns>The whole datagram.</returns>
    internal static byte[] BuildData(ushort blockNumber, ReadOnlySpan<byte> payload)
    {
        var packet = new byte[DataHeaderLength + payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(packet, DataOpcode);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), blockNumber);
        payload.CopyTo(packet.AsSpan(DataHeaderLength));
        return packet;
    }

    /// <summary>
    /// Builds the acknowledgement of <paramref name="blockNumber" />.
    /// </summary>
    /// <param name="blockNumber">The block acknowledged; 0 acknowledges an OACK.</param>
    /// <returns>The four-byte datagram.</returns>
    internal static byte[] BuildAcknowledgement(ushort blockNumber)
    {
        var packet = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(packet, AcknowledgementOpcode);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), blockNumber);
        return packet;
    }

    /// <summary>
    /// Reads the 16-bit big-endian field at <paramref name="offset" />: the opcode at 0,
    /// the block number or error code at 2.
    /// </summary>
    /// <param name="packet">The received datagram, at least four bytes long.</param>
    /// <param name="offset">The byte offset of the field.</param>
    /// <returns>The field's value.</returns>
    internal static ushort ReadField(ReadOnlySpan<byte> packet, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(packet[offset..]);

    /// <summary>
    /// Reads the block size an option acknowledgement grants.
    /// </summary>
    /// <param name="options">
    /// The OACK's body after its opcode: null-terminated name and value pairs.
    /// </param>
    /// <returns>
    /// The <c>blksize</c> value when it is present and within 8 to 65464, otherwise
    /// <see cref="DefaultBlockSize" />.
    /// </returns>
    internal static int ReadAcknowledgedBlockSize(ReadOnlySpan<byte> options)
    {
        var fields = Encoding.UTF8.GetString(options).Split('\0');
        for (var index = 0; index + 1 < fields.Length; index += 2)
        {
            if (string.Equals(fields[index], "blksize", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(fields[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var blockSize)
                && blockSize is >= MinimumBlockSize and <= MaximumBlockSize)
            {
                return blockSize;
            }
        }

        return DefaultBlockSize;
    }

    /// <summary>
    /// Builds a read or write request with curl 8.21.0's options, or none.
    /// </summary>
    /// <param name="opcode">The request's opcode.</param>
    /// <param name="fileName">The file name from the URL path, decoded.</param>
    /// <param name="transferSize">The <c>tsize</c> option's value.</param>
    /// <param name="blockSize">
    /// The <c>blksize</c> option's value, or <see langword="null" /> to send no options.
    /// </param>
    /// <param name="timeoutSeconds">The <c>timeout</c> option's value.</param>
    /// <returns>The whole datagram.</returns>
    private static byte[] BuildRequest(ushort opcode, string fileName, long transferSize, int? blockSize, int timeoutSeconds)
    {
        string[] fields = blockSize is { } requested
            ?
            [
                fileName,
                "octet",
                "tsize",
                transferSize.ToString(CultureInfo.InvariantCulture),
                "blksize",
                requested.ToString(CultureInfo.InvariantCulture),
                "timeout",
                timeoutSeconds.ToString(CultureInfo.InvariantCulture),
            ]
            : [fileName, "octet"];

        using var packet = new MemoryStream();
        packet.WriteByte(0);
        packet.WriteByte((byte)opcode);
        foreach (var field in fields)
        {
            packet.Write(Encoding.UTF8.GetBytes(field));
            packet.WriteByte(0);
        }

        return packet.ToArray();
    }
}
