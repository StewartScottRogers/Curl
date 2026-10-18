using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib571.c</c>: RTP interleaved in RTSP. The file <c>libtest_arg2</c> names is created first for the bodies
/// (<c>Could not open the protocol dump file</c> and <see cref="LibTestErrorCodes.MajorBad"/> when it cannot be).
/// Verbose <c>SETUP</c>, <c>PLAY</c>, <c>DESCRIBE</c> and <c>PLAY</c> requests hand every RTP packet to
/// <see cref="RtpWriter"/>; then <c>RECEIVE</c> requests follow until a transfer fails or 19 packets have come. The
/// checks of <c>easy_setopt</c> calls that cannot fail are left out.
/// </summary>
public sealed class Lib571 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib571";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        FileStream? protocolFile = LibTestRtspRequests.CreateOutput(context.Argument2);
        if (protocolFile is null)
        {
            context.StandardError.Write("Could not open the protocol dump file\n");
            return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
        }

        await using (protocolFile.ConfigureAwait(false))
        {
            return await LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, protocolFile, cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="protocolFile">The open file the bodies are written to.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, Stream protocolFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var rtp = new RtpWriter(context.StandardOutput, context.StandardError);
            var requests = new LibTestRtspRequests(steps, easy, context.Url);
            steps.EasySetOption(easy, CurlOption.Url, context.Url);
            if (await requests.PerformNextAsync(
                    [
                        (CurlOption.Interleavefunction, (InterleaveCallback)rtp.Write),
                        (CurlOption.Timeout, 30L),
                        (CurlOption.Verbose, 1L),
                        (CurlOption.Writedata, protocolFile),
                        (CurlOption.RtspTransport, "RTP/AVP/TCP;interleaved=0-1"),
                        (CurlOption.RtspRequest, (long)RtspRequest.Setup),
                    ],
                    cancellationToken).ConfigureAwait(false)
                && await requests.PerformNextAsync([(CurlOption.RtspRequest, (long)RtspRequest.Play)], cancellationToken).ConfigureAwait(false)
                && await requests.PerformNextAsync([(CurlOption.RtspRequest, (long)RtspRequest.Describe)], cancellationToken).ConfigureAwait(false)
                && await requests.PerformNextAsync([(CurlOption.RtspRequest, (long)RtspRequest.Play)], cancellationToken).ConfigureAwait(false))
            {
                context.StandardError.Write("PLAY COMPLETE\n");
                await ReceiveRestAsync(context, steps, easy, rtp, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// <c>RECEIVE</c> requests, each after <c>LOOPY LOOP!</c>, while the last succeeded and fewer than 19 packets have come.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="rtp">The program's packet counter.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the loop ends.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask ReceiveRestAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, RtpWriter rtp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(rtp);
        bool goesOn = true;
        while (goesOn && rtp.PacketCount < 19)
        {
            context.StandardError.Write("LOOPY LOOP!\n");
            goesOn = await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.RtspRequest, (long)RtspRequest.Receive)], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <c>rtp_write</c>: the interleave callback. Each packet is <c>$</c>, its channel, its 16-bit length and its payload,
    /// which should repeat <see cref="RtpData"/>; the size and channel are printed to stdout, a length that does not match
    /// the packet ends the transfer, a corrupt payload is printed and let through, and each packet taken is counted to
    /// stderr.
    /// </summary>
    /// <param name="output">The program's stdout.</param>
    /// <param name="error">The program's stderr.</param>
    public sealed class RtpWriter(Stream output, TextWriter error)
    {
        /// <summary>The 12 bytes every payload repeats.</summary>
        public static ReadOnlySpan<byte> RtpData => "$_1234\n\0Rsdf"u8;

        /// <summary>The packets taken so far.</summary>
        public int PacketCount { get; private set; }

        /// <summary>Takes one packet.</summary>
        /// <param name="data">The packet, from its <c>$</c>.</param>
        /// <returns>The packet's length when it is taken; otherwise 0, failing the transfer (a packet is never empty, so upstream's 1 for one is left out).</returns>
        public long Write(ReadOnlySpan<byte> data)
        {
            int channel = data[1];
            int codedSize = (data[2] << 8) | data[3];
            int messageSize = data.Length - 4;
            Print($"RTP: message size {messageSize}, channel {channel}\n");
            if (messageSize != codedSize)
            {
                Print($"RTP embedded size ({codedSize}) does not match the write size ({messageSize}).\n");
                return 0;
            }

            ReadOnlySpan<byte> payload = data[4..];
            for (int i = 0; i < messageSize; i += RtpData.Length)
            {
                CheckPayload(payload[i..], messageSize - i);
            }

            PacketCount++;
            error.Write($"packet count is {PacketCount}\n");
            return data.Length;
        }

        private void CheckPayload(ReadOnlySpan<byte> rest, int remaining)
        {
            if (remaining > RtpData.Length)
            {
                if (!rest[..RtpData.Length].SequenceEqual(RtpData))
                {
                    Print($"RTP PAYLOAD CORRUPTED [{AsCString(rest)}]\n");
                }
            }
            else if (!rest[..remaining].SequenceEqual(RtpData[..remaining]))
            {
                Print($"RTP PAYLOAD END CORRUPTED ({remaining}), [{AsCString(rest)}]\n");
            }
        }

        /// <summary>The bytes up to the first NUL, as <c>%s</c> prints them.</summary>
        private static string AsCString(ReadOnlySpan<byte> bytes)
        {
            int end = bytes.IndexOf((byte)0);
            return Encoding.Latin1.GetString(end < 0 ? bytes : bytes[..end]);
        }

        private void Print(string text) => output.Write(Encoding.Latin1.GetBytes(text));
    }
}
