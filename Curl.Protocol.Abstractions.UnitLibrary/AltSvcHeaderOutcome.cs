namespace Curl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="IAltSvcStore" /> did with one alternative an <c>Alt-Svc</c> header named:
/// either the <see cref="AltSvcAlternative" /> it added or the <see cref="AltSvcSkipReason" /> it
/// skipped it for, never both (ADR-0409).
/// </summary>
public sealed record AltSvcHeaderOutcome
{
    private AltSvcHeaderOutcome(AltSvcAlternative? added, AltSvcSkipReason? skipReason)
    {
        Added = added;
        SkipReason = skipReason;
    }

    /// <summary>
    /// Gets the alternative the store added, for the handler's <c>Added alt-svc:</c> line, or
    /// <see langword="null" /> when the alternative was skipped.
    /// </summary>
    public AltSvcAlternative? Added { get; }

    /// <summary>
    /// Gets why the store skipped the alternative, or <see langword="null" /> when it added it.
    /// </summary>
    public AltSvcSkipReason? SkipReason { get; }

    /// <summary>
    /// Creates the outcome of an alternative the store added.
    /// </summary>
    /// <param name="alternative">The alternative added.</param>
    /// <returns>The outcome, whose <see cref="Added" /> is <paramref name="alternative" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="alternative" /> is <see langword="null" />.</exception>
    public static AltSvcHeaderOutcome Adding(AltSvcAlternative alternative)
    {
        ArgumentNullException.ThrowIfNull(alternative);
        return new AltSvcHeaderOutcome(alternative, null);
    }

    /// <summary>
    /// Creates the outcome of an alternative the store skipped.
    /// </summary>
    /// <param name="reason">Why the store skipped it.</param>
    /// <returns>The outcome, whose <see cref="SkipReason" /> is <paramref name="reason" />.</returns>
    public static AltSvcHeaderOutcome Skipping(AltSvcSkipReason reason) => new(null, reason);
}
