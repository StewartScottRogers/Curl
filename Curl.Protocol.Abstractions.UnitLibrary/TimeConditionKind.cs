namespace Curl.Protocol.Abstractions;

/// <summary>
/// Which way round a <see cref="TimeCondition" /> compares, matching curl's two
/// <c>-z</c>/<c>--time-cond</c> spellings.
/// </summary>
/// <remarks>
/// <c>-z &lt;date&gt;</c> asks for the resource only when it changed after the date, and
/// <c>-z -&lt;date&gt;</c> — the leading dash is the whole difference — asks for it only
/// when it did not. Naming the direction keeps that dash from becoming a boolean whose
/// meaning has to be looked up.
/// </remarks>
public enum TimeConditionKind
{
    /// <summary>
    /// <c>-z &lt;date&gt;</c>: transfer only when the resource is newer than the value.
    /// </summary>
    IfModifiedSince = 0,

    /// <summary>
    /// <c>-z -&lt;date&gt;</c>: transfer only when the resource is not newer than the
    /// value.
    /// </summary>
    IfUnmodifiedSince,
}
