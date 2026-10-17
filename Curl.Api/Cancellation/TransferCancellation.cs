using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Errors;
using Curl.Api.Options;

namespace Curl.Api;

/// <summary>
/// Carries a caller's <see cref="CancellationToken"/> into one transfer the way libcurl can see it
/// (ADR-0639): once the token is cancelled, the transfer's <c>CURLOPT_XFERINFOFUNCTION</c>
/// (<see cref="Progress"/>) answers abort, so the engine ends the transfer with
/// <c>CURLE_ABORTED_BY_CALLBACK</c>, and the handle is unpaused (<c>curl_easy_pause(CURLPAUSE_CONT)</c>)
/// so a paused transfer runs on far enough to see the abort. Dispose it before the handle.
/// </summary>
internal sealed class TransferCancellation : IDisposable
{
    /// <summary>What <see cref="Progress"/> answers once the token is cancelled: any non-zero value but <see cref="ProgressCallbackResult.Continue"/> aborts.</summary>
    public const int Abort = 1;

    private readonly IEasyTransfer _easy;
    private readonly Action? _onCancel;
    private readonly CancellationTokenRegistration _registration;
    private volatile bool _cancelled;

    /// <summary>Starts watching <paramref name="cancellationToken"/> for the transfer on <paramref name="easy"/>.</summary>
    /// <param name="easy">The handle whose transfer the token cancels.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <param name="onCancel">
    /// Called on cancellation before the handle is unpaused, so a callback that paused the transfer
    /// can answer differently when it is called again; <see langword="null"/> for none.
    /// </param>
    public TransferCancellation(IEasyTransfer easy, CancellationToken cancellationToken, Action? onCancel = null)
    {
        _easy = easy;
        _onCancel = onCancel;
        _registration = cancellationToken.Register(Cancel);
    }

    /// <summary>The <c>CURLOPT_XFERINFOFUNCTION</c>: 0 to carry on, <see cref="Abort"/> once the token is cancelled.</summary>
    public TransferInfoCallback Progress => (_, _, _, _) => _cancelled ? Abort : 0;

    /// <summary><paramref name="options"/> with <see cref="Progress"/> as the transfer-info callback and <c>CURLOPT_NOPROGRESS</c> off, so the engine calls it.</summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The options to perform the transfer with.</returns>
    public EasyOptions ApplyTo(EasyOptions options) =>
        options with
        {
            Behaviour = options.Behaviour with { NoProgress = false },
            Callbacks = options.Callbacks with { TransferInfo = Progress },
        };

    /// <summary>
    /// Throws for a failed transfer: <see cref="OperationCanceledException"/> when
    /// <paramref name="cancellationToken"/> was cancelled, otherwise the <see cref="CurlException"/> for
    /// <paramref name="code"/>. Returns for <see cref="CurlCode.Ok"/>.
    /// </summary>
    /// <param name="code">The transfer's result.</param>
    /// <param name="errorText">The handle's error text for <paramref name="code"/>.</param>
    /// <param name="cancellationToken">The caller's token, as it stood when the transfer ended.</param>
    /// <exception cref="OperationCanceledException">The transfer failed and the caller had cancelled it.</exception>
    /// <exception cref="CurlException">The transfer failed for any other reason, a timeout included.</exception>
    public static void ThrowIfFailed(CurlCode code, string errorText, CancellationToken cancellationToken)
    {
        if (code != CurlCode.Ok)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        CurlException.ThrowIfFailed(code, errorText);
    }

    /// <summary>Stops watching the token; a cancellation after this no longer touches the handle.</summary>
    public void Dispose() => _registration.Dispose();

    private void Cancel()
    {
        _cancelled = true;
        _onCancel?.Invoke();
        _easy.Pause(CurlPause.Continue);
    }
}
