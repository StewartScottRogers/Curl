using System.Buffers.Binary;
using Curl.Conformance.Extended.Harness;
using Curl.Http2;

namespace Curl.Conformance.Extended.Grpc;

/// <summary>Reads the client connection preface and HTTP/2 frames (RFC 9113 sections 3.4 and 4.1) from a reference connection.</summary>
internal static class GrpcReferenceFrameReader
{
    private static readonly byte[] Preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();

    /// <summary>Reads the client connection preface.</summary>
    /// <returns><see langword="true" /> when the client sent it; <see langword="false" /> when it closed first or sent other bytes.</returns>
    public static async Task<bool> ReadPrefaceAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        byte[] received = new byte[Preface.Length];
        return await FillAsync(connection, received, cancellationToken).ConfigureAwait(false) && received.AsSpan().SequenceEqual(Preface);
    }

    /// <summary>Reads the next frame.</summary>
    /// <returns>The frame; <see langword="null" /> when the client closed the connection, before or inside a frame.</returns>
    public static async Task<Http2Frame?> ReadAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        byte[] header = new byte[Http2FrameCodec.HeaderLength];
        if (!await FillAsync(connection, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        byte[] payload = new byte[(header[0] << 16) | (header[1] << 8) | header[2]];
        return await FillAsync(connection, payload, cancellationToken).ConfigureAwait(false)
            ? new Http2Frame((Http2FrameType)header[3], header[4], BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(5)) & int.MaxValue, payload)
            : null;
    }

    private static async Task<bool> FillAsync(ReferenceConnection connection, byte[] buffer, CancellationToken cancellationToken)
    {
        for (int filled = 0; filled < buffer.Length;)
        {
            int read = await connection.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }
}
