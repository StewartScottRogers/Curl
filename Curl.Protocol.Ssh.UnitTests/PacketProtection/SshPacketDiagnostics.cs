using System.Buffers.Binary;
using Curl.Testing;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// Writes what a packet protection test arranges and gets - a binary packet with its decoded
/// length, padding length and message number, and hex strings compared against bytes - as
/// <c>ARRANGE</c>, <c>BYTES</c>, <c>ASSERT</c> and <c>DIFF</c> lines through the shared
/// <see cref="TestDiagnostics" /> helper (BL-1626).
/// </summary>
internal static class SshPacketDiagnostics
{
    /// <summary>
    /// Writes a binary packet as an ARRANGE line with its packet_length, padding_length and
    /// message number decoded, then the packet's bytes.
    /// </summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the packet is.</param>
    /// <param name="packet">The packet, length field first.</param>
    public static void ArrangePacket(this TestDiagnostics diagnostics, string label, byte[] packet)
    {
        diagnostics.Arrange(label, DescribePacket(packet));
        diagnostics.Bytes(label, packet);
    }

    /// <summary>Writes the ASSERT line between expected hex and actual bytes, then their first difference.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is compared.</param>
    /// <param name="expectedHex">The expected bytes as upper-case hex.</param>
    /// <param name="actual">The actual bytes.</param>
    public static void AssertHex(this TestDiagnostics diagnostics, string label, string expectedHex, byte[] actual)
    {
        diagnostics.Assert(label, expectedHex, Convert.ToHexString(actual));
        diagnostics.Diff(label, Convert.FromHexString(expectedHex), actual);
    }

    /// <summary>Describes a binary packet's header fields, or says it is shorter than one.</summary>
    /// <param name="packet">The packet, length field first.</param>
    /// <returns>The description.</returns>
    private static string DescribePacket(byte[] packet) =>
        packet.Length < 6
            ? $"{packet.Length} bytes, shorter than a packet header"
            : $"{packet.Length} bytes, packet_length {BinaryPrimitives.ReadUInt32BigEndian(packet)}, padding_length {packet[4]}, message {packet[5]}";
}
