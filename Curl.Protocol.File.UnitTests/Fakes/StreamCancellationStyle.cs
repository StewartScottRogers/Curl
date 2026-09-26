namespace Curl.Protocol.File.Fakes;

/// <summary>
/// How a <see cref="CancellingStream" /> reports that one of its operations was
/// cancelled.
/// </summary>
/// <remarks>
/// The two failing styles are both real: <see cref="FileStream" /> and
/// <see cref="MemoryStream" /> return a cancelled <see cref="ValueTask" /> — which
/// surfaces at the <c>await</c> as <see cref="TaskCanceledException" /> and not as
/// <see cref="OperationCanceledException" /> — while a stream wrapper is free to throw
/// <see cref="TaskCanceledException" /> from the call itself. Neither style produces the
/// exception type a caller of the handler expects, which is precisely why the handler has
/// to narrow them.
/// </remarks>
public enum StreamCancellationStyle
{
    /// <summary>
    /// The operation succeeds and the stream never inspects the token. A stream like this
    /// is what makes the handler's own <c>ThrowIfCancellationRequested</c> at the top of
    /// each loop body observable: with nothing else honouring the token, only that guard
    /// can end the transfer.
    /// </summary>
    None = 0,

    /// <summary>
    /// The operation returns <c>ValueTask.FromCanceled</c>, as a real
    /// <see cref="FileStream" /> or <see cref="MemoryStream" /> does.
    /// </summary>
    FaultedValueTask,

    /// <summary>
    /// The operation throws <see cref="TaskCanceledException" /> from the call itself.
    /// </summary>
    ThrownTaskCanceledException,
}
