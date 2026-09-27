using Curl.Cli;
using Curl.Core;

namespace Curl.Console;

/// <summary>
/// Maps the retry options of a parsed command line onto the <see cref="RetryPolicy" />
/// <see cref="TransferRetrier" /> applies: <c>--retry</c>, <c>--retry-delay</c>,
/// <c>--retry-max-time</c> and <c>--retry-all-errors</c>.
/// </summary>
internal static class RetryPolicyMapping
{
    /// <summary>
    /// Copies the retry options from <paramref name="options" />.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>
    /// The policy: <see cref="CommandLineOptions.RetryCount" />, capped at
    /// <see cref="int.MaxValue" /> (a run cannot retry more often than that in practice);
    /// <see cref="CommandLineOptions.RetryDelay" /> and <see cref="CommandLineOptions.RetryMaxTime" />,
    /// or zero, curl's default, when not given; and <see cref="CommandLineOptions.RetryAllErrors" />.
    /// </returns>
    internal static RetryPolicy FromCommandLine(CommandLineOptions options) =>
        new()
        {
            Retries = (int)Math.Min(options.RetryCount, int.MaxValue),
            Delay = options.RetryDelay ?? TimeSpan.Zero,
            MaxTime = options.RetryMaxTime ?? TimeSpan.Zero,
            RetryAllErrors = options.RetryAllErrors,
        };
}
