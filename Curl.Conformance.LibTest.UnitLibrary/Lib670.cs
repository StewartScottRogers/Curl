using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

#pragma warning disable CS0618 // curl_formadd is deprecated upstream too; lib670 exercises it.

/// <summary>
/// <c>lib670.c</c>, which serves cases 670 to 673: pausing and unpausing from a MIME (670, 671) or form (672, 673)
/// read callback. A verbose post with headers on of one field <c>field</c> of length 2 whose callback gives <c>A</c>,
/// then pauses, then gives <c>B</c> when the pause lasted at least 5 seconds (<c>A</c> when not), then ends. The
/// transfer runs on a multi handle (670, 672), unpaused from its loop, or on the easy interface (671, 673), unpaused
/// from its transfer-info callback; a pause that lasts 20 seconds writes <c>unpausing failed: drain problem?</c> on
/// stderr. A callback called after its end writes <c>Read callback called after EOF</c> on stderr and aborts the
/// transfer, where upstream's exits.
/// </summary>
/// <param name="timeProvider">The clock upstream's <c>time(NULL)</c> reads.</param>
public sealed class Lib670(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>PAUSE_TIME</c>, in seconds.</summary>
    public const int PauseSeconds = 5;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib670";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: builds the MIME or form post, then transfers it.</summary>
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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            await PostAsync(context, steps, library, easy, cancellationToken).ConfigureAwait(false);
        }
    }

    // Binds the case's MIME tree or form, then transfers it; the form is freed at the end.
    private async ValueTask PostAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlEasy easy, CancellationToken cancellationToken)
    {
        var pooh = new ReadThis(easy, timeProvider, context.StandardError);
        using CurlMime mime = library.MimeInit();
        CurlForm? form = null;
        try
        {
            _ = steps.EasySetOptions(easy, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Verbose, 1L), (CurlOption.Header, 1L)]);
            bool usesMime = context.TestNumber is 670 or 671;
            if (usesMime ? BindMime(context, steps, easy, mime, pooh) : BindForm(context, steps, library, easy, ref form, pooh))
            {
                await TransferAsync(context.TestNumber is 670 or 672, steps, library, easy, pooh, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            CurlForm.Free(form);
        }
    }

    /// <summary>The MIME tree: one part <c>field</c> of length 2 read from <paramref name="pooh"/>, bound as <c>CURLOPT_MIMEPOST</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="mime">The structure to build.</param>
    /// <param name="pooh">The part's reader.</param>
    /// <returns>Whether the program goes on; a refused name is written on stderr.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool BindMime(LibTestContext context, LibTestSteps steps, CurlEasy easy, CurlMime mime, ReadThis pooh)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(mime);
        ArgumentNullException.ThrowIfNull(pooh);
        CurlMimePart part = mime.AddPart();
        steps.Result = part.SetName("field");
        if (steps.Result != CurlCode.Ok)
        {
            context.StandardError.Write($"Something went wrong when building the mime structure: {(int)steps.Result}\n");
            return false;
        }

        // A live part takes any callback, so upstream's check of curl_mime_data_cb before binding is left out.
        _ = part.SetDataCallback(2, pooh.Read, null);
        _ = steps.EasySetOption(easy, CurlOption.Mimepost, mime);
        return true;
    }

    /// <summary>The form: one field <c>field</c> streamed with length 2 through <c>CURLOPT_READFUNCTION</c>, bound as <c>CURLOPT_HTTPPOST</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="form">The form built.</param>
    /// <param name="pooh">The field's reader.</param>
    /// <returns>Whether the program goes on; a refused <c>curl_formadd</c> is written on stderr.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool BindForm(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlEasy easy, ref CurlForm? form, ReadThis pooh)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(pooh);
        CurlFormAddCode code = CurlForm.Add(library, ref form, [new(CurlFormOption.CopyName, "field"), new(CurlFormOption.Stream, pooh), new(CurlFormOption.ContentLength, 2L)]);
        if (code != CurlFormAddCode.Ok)
        {
            context.StandardError.Write($"curl_formadd() = {(int)code}\n");
            return false;
        }

        _ = steps.EasySetOptions(easy, [(CurlOption.Readfunction, (ReadCallback)pooh.Read), (CurlOption.Httppost, form)]);
        return true;
    }

    // The multi interface, unpaused from its loop, or the easy one, unpaused from the transfer-info callback.
    private static async ValueTask TransferAsync(bool usesMulti, LibTestSteps steps, CurlLibrary library, CurlEasy easy, ReadThis pooh, CancellationToken cancellationToken)
    {
        if (!usesMulti)
        {
            _ = steps.EasySetOptions(easy, [(CurlOption.Xferinfofunction, (TransferInfoCallback)pooh.TransferInfo), (CurlOption.Noprogress, 0L)]);
            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            return;
        }

        CurlMulti multi = library.MultiInit();
        await using (multi.ConfigureAwait(false))
        {
            await MultiLoopAsync(steps, multi, easy, pooh, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The multi loop: performs until the transfer ends or a call fails, unpausing once the pause has lasted
    /// <see cref="PauseSeconds"/>, giving up with <see cref="CurlCode.OperationTimedOut"/> after four times that,
    /// and waiting up to half a second between performs. When a multi call failed, the transfer's done code is the result.
    /// </summary>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="multi">The multi handle.</param>
    /// <param name="easy">The handle transferred.</param>
    /// <param name="pooh">The reader whose pause the loop ends.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes once the handle is removed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask MultiLoopAsync(LibTestSteps steps, CurlMulti multi, CurlEasy easy, ReadThis pooh, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(multi);
        ArgumentNullException.ThrowIfNull(pooh);
        CurlMultiCode code = multi.AddHandle(easy);
        bool goesOn = code == CurlMultiCode.Ok;
        while (goesOn)
        {
            (code, goesOn) = await PerformOnceAsync(steps, multi, pooh, cancellationToken).ConfigureAwait(false);
        }

        while (code != CurlMultiCode.Ok && multi.InfoRead(out _) is { } message)
        {
            steps.Result = message.Type == CurlMultiMessageType.Done ? message.Result : steps.Result;
        }

        _ = multi.RemoveHandle(easy);
    }

    // One turn of the loop: perform, then unpause or give up, then wait. Answers the last multi code and whether to go round again.
    private static async ValueTask<(CurlMultiCode Code, bool GoesOn)> PerformOnceAsync(LibTestSteps steps, CurlMulti multi, ReadThis pooh, CancellationToken cancellationToken)
    {
        var (code, running) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
        if (running == 0 || code != CurlMultiCode.Ok)
        {
            return (code, false);
        }

        if (pooh.UnpauseOrGiveUp())
        {
            steps.Result = CurlCode.OperationTimedOut;
            return (code, false);
        }

        (code, _) = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, PauseSeconds * 1000 / 10, cancellationToken).ConfigureAwait(false);
        return (code, code == CurlMultiCode.Ok);
    }

    /// <summary><c>struct t670_ReadThis</c> with its two callbacks: the handle to unpause, when the pause began and the calls made.</summary>
    /// <param name="easy">The handle the callbacks unpause.</param>
    /// <param name="timeProvider">The clock upstream's <c>time(NULL)</c> reads.</param>
    /// <param name="error">The program's stderr.</param>
    public sealed class ReadThis(CurlEasy easy, TimeProvider timeProvider, TextWriter error)
    {
        private long? origin;
        private int count;

        /// <summary><c>t670_read_cb</c>: <c>A</c>, then a pause, then <c>B</c> (or <c>A</c> after too short a pause), then the end.</summary>
        /// <param name="buffer">Where the byte goes.</param>
        /// <returns>1, <see cref="ReadCallbackResult.Pause"/> or 0; after the end, <see cref="ReadCallbackResult.Abort"/>.</returns>
        public long Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            switch (count++)
            {
                case 0:
                    buffer[0] = (byte)'A';
                    return 1;
                case 1:
                    origin = timeProvider.GetTimestamp();
                    return ReadCallbackResult.Pause;
                case 2:
                    buffer[0] = SecondsPaused() >= PauseSeconds ? (byte)'B' : (byte)'A';
                    return 1;
                case 3:
                    return 0;
                default:
                    error.Write("Read callback called after EOF\n");
                    return ReadCallbackResult.Abort;
            }
        }

        /// <summary><c>t670_xferinfo</c>: unpauses, or aborts the transfer when the pause lasted too long.</summary>
        /// <param name="downloadTotal">Unused.</param>
        /// <param name="downloadNow">Unused.</param>
        /// <param name="uploadTotal">Unused.</param>
        /// <param name="uploadNow">Unused.</param>
        /// <returns>0 to go on, or <see cref="CurlCode.AbortedByCallback"/>'s number to abort.</returns>
        public int TransferInfo(long downloadTotal, long downloadNow, long uploadTotal, long uploadNow) =>
            UnpauseOrGiveUp() ? (int)CurlCode.AbortedByCallback : 0;

        /// <summary>
        /// Once paused: unpauses with <c>CURLPAUSE_CONT</c> after <see cref="PauseSeconds"/>, and gives up, writing
        /// <c>unpausing failed: drain problem?</c> on stderr, after four times that.
        /// </summary>
        /// <returns>Whether the pause lasted too long.</returns>
        public bool UnpauseOrGiveUp()
        {
            if (origin is null)
            {
                return false;
            }

            long seconds = SecondsPaused();
            if (seconds >= 4 * PauseSeconds)
            {
                error.Write("unpausing failed: drain problem?\n");
                return true;
            }

            _ = seconds >= PauseSeconds ? easy.Pause(CurlPause.Continue) : CurlCode.Ok;
            return false;
        }

        // Whole seconds since the pause began, as time(NULL) differences count them.
        private long SecondsPaused() => (long)timeProvider.GetElapsedTime(origin!.Value).TotalSeconds;
    }
}
