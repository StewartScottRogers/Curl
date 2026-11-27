namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="Exception.Data" /> keys under which a connector says, on the
/// <see cref="OperationCanceledException" /> a cancellation made it throw, what a caller that ended
/// the connect itself needs to report it as curl does (BL-6074). The timings of such a connect are
/// carried under <see cref="ConnectTimings.CarriedOnCancellationKey" />.
/// </summary>
public static class ConnectCancellationKeys
{
    /// <summary>
    /// The key whose value is <see langword="true" /> when the cancellation arrived while a host name
    /// was being resolved, which curl reports as <c>Resolving timed out</c> when a time limit ended it.
    /// </summary>
    public const string ArrivedWhileResolving = "Curl.ConnectCanceledWhileResolving";

    /// <summary>
    /// The key whose value is a <see cref="Func{TResult}" /> of <see cref="long" /> that numbers the
    /// connect as curl's next connection. A caller calls it once, and only when it reports the connect
    /// as failed: a connect that is not reported, such as one that lost a race, takes no number.
    /// </summary>
    public const string ConnectionNumberTaker = "Curl.ConnectCanceledConnectionNumberTaker";
}
