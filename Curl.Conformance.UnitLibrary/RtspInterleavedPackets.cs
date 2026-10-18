using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// The interleaved RTP packets upstream's <c>rtspd</c> (<c>tests/server/rtspd.c</c> at <c>curl-8_22_0</c>)
/// sends after a reply, read from the case's <c>&lt;servercmd&gt;</c> lines
/// <c>rtp: part P channel C size S</c> and <c>rtp: part P channel C size S size_err E</c>.
/// </summary>
/// <remarks>
/// Each line whose P is the reply's part number adds, in file order, one packet: <c>$</c>, the channel's low
/// byte, S + E as a 16-bit length in network order, and S payload bytes repeating <c>$_1234\n\0Rsdf</c>, so a
/// nonzero E makes the length lie about the payload, as rtspd's does.
/// </remarks>
internal static class RtspInterleavedPackets
{
    private static readonly byte[] PayloadPattern = "$_1234\n\0Rsdf"u8.ToArray();

    // Interpreted, not source-generated, so no generated code counts against the coverage gate.
    private static readonly Regex RtpCommand = new(
        @"^rtp: part (?<part>-?\d+) channel (?<channel>-?\d+) size (?<size>\d+)(?: size_err (?<error>-?\d+))?",
        RegexOptions.CultureInvariant);

    /// <summary>The packets rtspd sends after the reply to part <paramref name="partNumber"/>.</summary>
    /// <param name="serverCommands">The <c>&lt;servercmd&gt;</c> body, after expansion.</param>
    /// <param name="partNumber">The part number of the request being answered.</param>
    /// <returns>The packets, one after another; empty when no line names the part.</returns>
    public static byte[] ForPart(ReadOnlySpan<byte> serverCommands, int partNumber)
    {
        List<byte> packets = [];
        foreach (Match command in Encoding.Latin1.GetString(serverCommands).Split('\n').Select(line => RtpCommand.Match(line)))
        {
            if (command.Success && Number(command, "part") == partNumber)
            {
                AddPacket(packets, Number(command, "channel"), Number(command, "size"), command.Groups["error"].Success ? Number(command, "error") : 0);
            }
        }

        return [.. packets];
    }

    private static int Number(Match command, string group) => int.Parse(command.Groups[group].Value, System.Globalization.CultureInfo.InvariantCulture);

    private static void AddPacket(List<byte> packets, int channel, int size, int sizeError)
    {
        int codedSize = size + sizeError;
        packets.AddRange([(byte)'$', (byte)channel, (byte)(codedSize >> 8), (byte)codedSize]);
        for (int i = 0; i < size; i++)
        {
            packets.Add(PayloadPattern[i % PayloadPattern.Length]);
        }
    }
}
