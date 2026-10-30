namespace Curl.Extension.Dane.Wire;

/// <summary>
/// A record's RDATA read into its fields; one derived record per type <see cref="DnssecResponseDecoder" /> types.
/// </summary>
public abstract record DnsRecordData;
