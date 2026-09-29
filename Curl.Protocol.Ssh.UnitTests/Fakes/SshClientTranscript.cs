using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// Reads back what the client wrote, as the server would: the identification line, then
/// each packet's payload, opening the packets after each <c>NEWKEYS</c> with the next of
/// the client-to-server protections given.
/// </summary>
internal static class SshClientTranscript
{
    /// <summary>
    /// Reads every packet in <paramref name="written" />.
    /// </summary>
    /// <param name="written">Everything the client wrote.</param>
    /// <param name="resetAtNewKeys">Whether the sequence number restarts after each <c>NEWKEYS</c>.</param>
    /// <param name="protections">The client-to-server protection after the first, second, ... <c>NEWKEYS</c>.</param>
    /// <returns>The payloads, in order.</returns>
    internal static async Task<List<byte[]>> PayloadsAsync(byte[] written, bool resetAtNewKeys, params ISshPacketProtection[] protections)
    {
        ScriptedConnection connection = new(written);
        SshConnectionReader connectionReader = new(connection);
        await connectionReader.ReadLineAsync(256, CancellationToken.None);
        SshPacketReader reader = new(connectionReader);
        Queue<ISshPacketProtection> pending = new(protections);
        List<byte[]> payloads = [];
        while (true)
        {
            byte[] payload;
            try
            {
                payload = await reader.ReadAsync(CancellationToken.None);
            }
            catch (EndOfStreamException)
            {
                return payloads;
            }

            payloads.Add(payload);
            if (payload[0] == SshMessageNumber.NewKeys && pending.TryDequeue(out ISshPacketProtection? next))
            {
                reader.ChangeProtection(next);
                if (resetAtNewKeys)
                {
                    reader.ResetSequenceNumber();
                }
            }
        }
    }
}
