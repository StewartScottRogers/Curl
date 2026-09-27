namespace Curl.Core;

/// <summary>
/// The command-line options that decide how <see cref="TransferRetrier" /> re-runs a
/// transfer that failed transiently; every member defaults to curl 8.21.0's behaviour when
/// the option is not given.
/// </summary>
public sealed record RetryPolicy
{
    /// <summary>
    /// Gets how many times a transiently failed transfer is run again, per <c>--retry</c>;
    /// zero, curl's default, never retries.
    /// </summary>
    public int Retries { get; init; }

    /// <summary>
    /// Gets the fixed wait before every retry, per <c>--retry-delay</c> (curl takes it in
    /// seconds with millisecond precision). <see cref="TimeSpan.Zero" />, curl's default,
    /// waits one second before the first retry and doubles the wait before each later one,
    /// up to ten minutes. A <c>Retry-After</c> response header overrides either.
    /// </summary>
    public TimeSpan Delay { get; init; }
}
