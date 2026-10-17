using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib677.c</c>: a verbose <c>CURLOPT_CONNECT_ONLY</c> transfer on a multi handle; once it is done, prints
/// <c>Connected fine, extracted socket. Moving on</c> on stdout, then, for 5 seconds in all, waits up to 50
/// milliseconds at a time for its socket to be writable, sends <c>A1 IDLE</c> with <c>curl_easy_send</c>, and waits
/// for it to be readable and reads the reply with <c>curl_easy_recv</c>, at most 1024 bytes. Once the command was
/// sent, the bytes read and a newline are printed on stdout. A failing send or receive writes its code and text on
/// stderr and ends the program with it.
/// </summary>
/// <param name="timeProvider">The clock upstream's <c>time(NULL)</c> reads.</param>
public sealed class Lib677(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>testcmd</c>: the command sent.</summary>
    public const string TestCommand = "A1 IDLE\r\n";

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib677";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>global_init</c>: the connect, then the exchange for 5 seconds.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return;
            }

            await using (easy.ConfigureAwait(false))
            {
                _ = steps.EasySetOptions(easy, [(CurlOption.Verbose, 1L), (CurlOption.Url, context.Url), (CurlOption.ConnectOnly, 1L)]);
                if (multi.AddHandle(easy) == CurlMultiCode.Ok)
                {
                    var exchange = new Exchange(steps, multi, easy, context);
                    if (await exchange.RunAsync(timeProvider, cancellationToken).ConfigureAwait(false))
                    {
                        exchange.PrintReply();
                        _ = multi.RemoveHandle(easy);
                    }
                }
            }
        }
    }

    /// <summary>The program's loop and its state: the socket, whether the command was sent, and the bytes read.</summary>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="multi">The multi handle.</param>
    /// <param name="easy">The connect-only handle.</param>
    /// <param name="context">The program's context.</param>
    public sealed class Exchange(LibTestSteps steps, CurlMulti multi, CurlEasy easy, LibTestContext context)
    {
        private readonly byte[] buffer = new byte[1024];
        private readonly byte[] command = Encoding.ASCII.GetBytes(TestCommand);
        private CurlSocket socket = CurlSocket.Bad;
        private bool sent;
        private int position;

        /// <summary>Runs the loop until 5 seconds have passed by <paramref name="clock"/>.</summary>
        /// <param name="clock">The clock the 5 seconds are counted on.</param>
        /// <param name="cancellationToken">Stops the loop.</param>
        /// <returns>Whether the loop ran to its end; <see langword="false"/> when the program goes to its cleanup.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="clock"/> is <see langword="null"/>.</exception>
        public async ValueTask<bool> RunAsync(TimeProvider clock, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(clock);
            long start = clock.GetTimestamp();
            while (clock.GetElapsedTime(start) < TimeSpan.FromSeconds(5))
            {
                if (!(await steps.MultiPerformAsync(multi, cancellationToken).ConfigureAwait(false)).GoesOn || !ReadMessages())
                {
                    return false;
                }

                CurlWaitEvents ready = await WaitAsync(cancellationToken).ConfigureAwait(false);
                if (ready != CurlWaitEvents.None && !await ExchangeAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Once the command was sent, prints the bytes read and a newline on stdout.</summary>
        public void PrintReply()
        {
            if (sent)
            {
                context.StandardOutput.Write(buffer, 0, position);
                context.StandardOutput.WriteByte((byte)'\n');
            }
        }

        // Reads the multi messages; the handle's done message takes its socket. False when it has none.
        private bool ReadMessages()
        {
            while (multi.InfoRead(out _) is { } message)
            {
                if (message.Type != CurlMultiMessageType.Done || message.Easy != easy)
                {
                    continue;
                }

                _ = easy.GetInfo(CurlInfo.Activesocket, out object? value);
                socket = new CurlSocket((nint)Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                if (socket == CurlSocket.Bad)
                {
                    return false;
                }

                context.StandardOutput.Write("Connected fine, extracted socket. Moving on\n"u8);
            }

            return true;
        }

        // curl_multi_wait for 50 milliseconds, with the socket when there is one; the events it is ready for that were asked.
        private async ValueTask<CurlWaitEvents> WaitAsync(CancellationToken cancellationToken)
        {
            if (socket == CurlSocket.Bad)
            {
                _ = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, 50, cancellationToken).ConfigureAwait(false);
                return CurlWaitEvents.None;
            }

            CurlWaitEvents events = sent ? CurlWaitEvents.PollIn : CurlWaitEvents.PollOut;
            CurlWaitSocket[] wait = [new(socket, events, CurlWaitEvents.None)];
            _ = await multi.WaitAsync(wait, 50, cancellationToken).ConfigureAwait(false);
            return wait[0].ReturnedEvents & events;
        }

        // One send or receive; a socket that moved nothing is dropped. False when the call failed.
        private async ValueTask<bool> ExchangeAsync(CancellationToken cancellationToken)
        {
            var (code, length) = sent
                ? position < buffer.Length ? await easy.RecvAsync(buffer.AsMemory(position), cancellationToken).ConfigureAwait(false) : new(CurlCode.Ok, 0)
                : await easy.SendAsync(command.AsMemory(position), cancellationToken).ConfigureAwait(false);
            if (code == CurlCode.Again)
            {
                return true;
            }

            if (code != CurlCode.Ok)
            {
                steps.Result = code;
                context.StandardError.Write($"curl_easy_{(sent ? "recv" : "send")}() failed, with code {(int)code} ({CurlErrorText.Easy(code)})\n");
                return false;
            }

            Advance(length);
            return true;
        }

        // Counts the bytes moved; the whole command sent turns to reading.
        private void Advance(int length)
        {
            position = sent || length > 0 ? position + length : 0;
            if (!sent && position == command.Length)
            {
                sent = true;
                position = 0;
            }

            socket = length > 0 ? socket : CurlSocket.Bad;
        }
    }
}
