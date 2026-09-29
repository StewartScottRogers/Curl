using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Applies <c>-z</c>/<c>--time-cond</c> to the time <c>MDTM</c> reported, as curl 8.21.0's
/// <c>ftp_state_mdtm_resp</c> does (BL-637).
/// </summary>
internal static class FtpTimeCondition
{
    /// <summary>
    /// Decides whether the transfer goes on, and the <c>-v</c> line curl writes about it.
    /// </summary>
    /// <param name="condition">The condition to apply.</param>
    /// <param name="modifiedUtc">The time <c>MDTM</c> reported, or <see langword="null" /> when unknown.</param>
    /// <returns>
    /// Whether the transfer goes on, and the line to report, or <see langword="null" /> for none.
    /// </returns>
    /// <remarks>
    /// Measured on curl 8.21.0: a time or a condition at or before the Unix epoch, or an
    /// unknown time, skips the comparison and transfers. Otherwise an if-modified-since
    /// condition transfers only a strictly newer file, and an if-unmodified-since one a file
    /// no newer than it, equality included.
    /// </remarks>
    internal static (bool IsMet, string? VerboseLine) Check(TimeCondition condition, DateTimeOffset? modifiedUtc)
    {
        long fileSeconds = modifiedUtc?.ToUnixTimeSeconds() ?? 0;
        long conditionSeconds = condition.Value.ToUnixTimeSeconds();
        return fileSeconds <= 0 || conditionSeconds <= 0
            ? (true, FtpTransferMessages.SkippingTimeComparison)
            : Compare(condition.Kind, fileSeconds > conditionSeconds);
    }

    /// <summary>
    /// Applies a condition to a known time: if-modified-since goes on only for a newer file,
    /// if-unmodified-since only for one that is not newer.
    /// </summary>
    private static (bool IsMet, string? VerboseLine) Compare(TimeConditionKind kind, bool newer) =>
        kind == TimeConditionKind.IfModifiedSince
            ? (newer, newer ? null : FtpTransferMessages.NotNewEnough)
            : (!newer, newer ? FtpTransferMessages.NotOldEnough : null);
}
