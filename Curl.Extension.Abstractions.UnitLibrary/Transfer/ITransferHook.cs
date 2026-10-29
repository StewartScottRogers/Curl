namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// What an extension activated by its option does to one transfer (ADR-1007): adds request headers,
/// observes or transforms the response body, and reads the status, headers and timings when the
/// transfer ends. Each member does nothing unless the extension implements it. Hooks run after curl's
/// own processing on the way in and before it on the way out.
/// </summary>
public interface ITransferHook
{
    /// <summary>Gives the request headers to add after curl's own (<c>-H</c> and the rest), as <c>Name: value</c> lines.</summary>
    /// <param name="context">The transfer as curl's options built it.</param>
    /// <returns>The headers to add; none by default. Ignored for a transfer that is not HTTP.</returns>
    IEnumerable<string> RequestHeadersFor(ITransferContext context) => [];

    /// <summary>
    /// Gives the stream the response body is written to before it reaches <paramref name="output" />,
    /// where curl's own output processing begins. The hook may pass bytes through, observe or rewrite
    /// them; it is flushed once the handler has written the whole body.
    /// </summary>
    /// <param name="output">Where the body goes next.</param>
    /// <returns>The stream to write the body to; <paramref name="output" /> itself by default.</returns>
    Stream ResponseBodyOver(Stream output) => output;

    /// <summary>
    /// Reads the finished transfer: its exit code and its <see cref="TransferResult.Report" />, with the
    /// response code, response headers and <see cref="TransferReport.Timings" />. Called before curl reports
    /// the transfer (<c>-w</c>, exit code).
    /// </summary>
    /// <param name="result">The handler's result.</param>
    void TransferFinished(TransferResult result)
    {
    }

    /// <summary>
    /// Asks, once the transfer has ended and every hook has read it, for the same transfer to run again
    /// after a delay (ADR-1009), as Server-Sent Events reconnect after their reconnection time. The transfer
    /// is waited for on its <see cref="ITransferContext.TimeProvider" /> and repeated with every hook's
    /// <see cref="RequestHeadersFor" /> asked afresh and the body written through the same streams; the
    /// first hook, in request order, that names a delay decides it. Cancelling the transfer ends the wait,
    /// and the last result is the transfer's.
    /// </summary>
    /// <param name="result">The result of the transfer that just ended.</param>
    /// <returns>How long to wait before repeating; <see langword="null" />, by default, not to repeat.</returns>
    TimeSpan? RepeatAfter(TransferResult result) => null;
}
