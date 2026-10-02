namespace Curl.Networking;

/// <summary>
/// Finds a host's ECHConfigList in DNS, as curl 8.21.0 does under <c>--ech true</c> or <c>hard</c>
/// with no <c>ecl:</c> list: from the <c>ech</c> parameter of the host's HTTPS record, which curl
/// asks the DoH server for (ADR-0312, ADR-0326).
/// </summary>
public interface IEchConfigListLookup
{
    /// <summary>Looks up the ECHConfigList of <paramref name="host" /> on <paramref name="port" />.</summary>
    /// <param name="host">The host the transfer connects to.</param>
    /// <param name="port">The port it connects to.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The list's bytes, or <see langword="null" /> when the host has none.</returns>
    ValueTask<byte[]?> FindEchConfigListAsync(string host, int port, CancellationToken cancellationToken);
}
