using Curl.Authentication;
using Curl.Kerberos;

namespace Curl.Console;

/// <summary>
/// Where the hand-built Kerberos route of ADR-0142 finds its inputs: <c>krb5.conf</c> as MIT
/// finds it (<c>KRB5_CONFIG</c>, else <c>/etc/krb5.conf</c>), the default credential cache
/// (<c>KRB5CCNAME</c>, else <c>FILE:/tmp/krb5cc_&lt;uid&gt;</c>), and a KDC client over the
/// given SRV lookup and KDC transport. Each is read when a Negotiate context first asks,
/// never at start-up.
/// </summary>
/// <param name="files">Reads the files.</param>
/// <param name="readEnvironmentVariable">Reads an environment variable; <see langword="null" /> when unset.</param>
/// <param name="readUserId">Gives the user id of the default cache's name.</param>
/// <param name="srvLookup">Looks up KDC SRV records.</param>
/// <param name="transport">Reaches the KDCs.</param>
/// <param name="timeProvider">Gives the time for tickets and authenticators.</param>
internal sealed class HandBuiltKerberosSources(
    IKerberosFileReader files,
    Func<string, string?> readEnvironmentVariable,
    Func<uint> readUserId,
    IKerberosSrvLookup srvLookup,
    IKerberosKdcTransport transport,
    TimeProvider timeProvider)
{
    /// <summary>Reads one of the process's environment variables, as MIT reads <c>KRB5_CONFIG</c> and <c>KRB5CCNAME</c>.</summary>
    /// <param name="name">The variable's name.</param>
    /// <returns>Its value, or <see langword="null" /> when unset.</returns>
    public static string? ReadProcessEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    /// <summary>Reads <c>krb5.conf</c>.</summary>
    /// <returns>The configuration; empty when no file exists.</returns>
    public KerberosConfiguration ReadConfiguration() => new KerberosConfigurationStore(files, readEnvironmentVariable).Read();

    /// <summary>Reads the default credential cache.</summary>
    /// <returns>The cache.</returns>
    public CredentialCache ReadCredentialCache() => new CredentialCacheStore(files, readEnvironmentVariable, readUserId).ReadDefault();

    /// <summary>Makes a KDC client for <paramref name="configuration" />.</summary>
    /// <param name="configuration">The configuration read.</param>
    /// <returns>The client.</returns>
    public KerberosKdcClient CreateKdcClient(KerberosConfiguration configuration) =>
        new(configuration, srvLookup, transport, timeProvider, new SystemKerberosRandomSource());

    /// <summary>Makes the ticket source over these inputs.</summary>
    /// <returns>The ticket source.</returns>
    public KerberosServiceTicketSource CreateTicketSource() => new(ReadConfiguration, ReadCredentialCache, CreateKdcClient);
}
