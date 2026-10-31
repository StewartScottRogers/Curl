namespace Curl.Extension.Protocol.DnsQuery.Wire;

/// <summary>What encoding or decoding a DNS message gave: a value, or the reason there is none.</summary>
/// <typeparam name="T">The value's type.</typeparam>
/// <param name="Value">The value, or <see langword="null" /> when the work failed.</param>
/// <param name="Failure">Why the work failed, or <see langword="null" /> when it succeeded.</param>
internal sealed record DnsOutcome<T>(T? Value, string? Failure)
    where T : class
{
    /// <summary>Gives a successful outcome.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The outcome.</returns>
    public static DnsOutcome<T> Succeeded(T value) => new(value, null);

    /// <summary>Gives a failed outcome.</summary>
    /// <param name="failure">Why the work failed.</param>
    /// <returns>The outcome.</returns>
    public static DnsOutcome<T> Failed(string failure) => new(null, failure);
}
