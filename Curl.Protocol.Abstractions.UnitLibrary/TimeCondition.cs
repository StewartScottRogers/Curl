namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <c>-z</c>/<c>--time-cond</c> condition a transfer is subject to: a timestamp and
/// the direction it is compared in.
/// </summary>
/// <param name="Value">
/// The timestamp to compare the resource's last-write time against. The command-line
/// layer has already parsed curl's accepted date spellings into this value.
/// </param>
/// <param name="Kind">Which way round the comparison runs.</param>
/// <remarks>
/// A handler compares this against the last-write time of the resource it opened — for
/// <c>file://</c>, <see cref="FileOpenResult.LastWriteTimeUtc" /> — and skips the body
/// when the condition is not met. curl treats a skipped transfer as a success, not an
/// error, so the exit code stays <see cref="CurlExitCode.Ok" />.
/// </remarks>
public sealed record TimeCondition(DateTimeOffset Value, TimeConditionKind Kind);
