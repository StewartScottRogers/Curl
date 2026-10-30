namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// Gives what the HTTP handler tells as a request starts waiting for <c>100 Continue</c>: the start of
    /// <paramref name="socketWait" />'s expect-100 deadline (<see cref="TransferSocketWait.BeginExpectContinueWait" />, BL-3864).
    /// </summary>
    /// <param name="socketWait">The socket-action transfer's wait, or <see langword="null" /> outside socket-action mode.</param>
    /// <returns>The begin hook, or <see langword="null" /> without a wait.</returns>
    internal static Action<TimeSpan>? ContinueWaitBeginsOf(TransferSocketWait? socketWait) =>
        socketWait is null ? null : socketWait.BeginExpectContinueWait;

    /// <summary>
    /// Gives what the HTTP handler tells as a request's wait for <c>100 Continue</c> ends: the end of
    /// <paramref name="socketWait" />'s expect-100 deadline (<see cref="TransferSocketWait.EndExpectContinueWait" />, BL-3864).
    /// </summary>
    /// <param name="socketWait">The socket-action transfer's wait, or <see langword="null" /> outside socket-action mode.</param>
    /// <returns>The end hook, or <see langword="null" /> without a wait.</returns>
    internal static Action? ContinueWaitEndsOf(TransferSocketWait? socketWait) =>
        socketWait is null ? null : socketWait.EndExpectContinueWait;
}
