using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// The outcome of a TLSA lookup: its status and, when <see cref="TlsaLookupStatus.Secure" />, the TLSA records.
/// </summary>
/// <param name="Status">What the lookup proved.</param>
/// <param name="Records">The validated TLSA records; empty unless <paramref name="Status" /> is secure.</param>
public sealed record TlsaLookupResult(TlsaLookupStatus Status, IReadOnlyList<TlsaRecordData> Records)
{
    /// <summary>Gives a result with no records.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The result.</returns>
    public static TlsaLookupResult WithoutRecords(TlsaLookupStatus status) => new(status, []);
}
