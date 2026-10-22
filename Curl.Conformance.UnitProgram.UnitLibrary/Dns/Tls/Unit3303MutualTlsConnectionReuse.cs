using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Engine;

namespace Curl.Conformance.UnitProgram.Dns.Tls;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit3303.c</c>: <c>Curl_ssl_conn_config_match</c>, here the
/// equality of two option groups' <see cref="OptionGroupConnectionSettings"/>, which decides whether a
/// pooled connection is reused. The connection is made with the client certificate <c>client.pem</c>,
/// key <c>client.key</c>, pass phrase <c>secret</c> and both types <c>PEM</c>; the C program's six checks,
/// in order: the same options match; pass phrase <c>wrong</c>, key <c>other.key</c>, key type <c>DER</c>
/// and certificate type <c>P12</c> each do not; the options restored match again. Curl has no
/// <c>data-&gt;set</c> or <c>struct connectdata</c>: each side is an <see cref="EasyTlsOptions"/> read
/// through <see cref="OptionGroupConnectionSettings.Of"/>, and only the observable part, reuse or not, is
/// compared. Each failing check writes the C program's message to standard error and counts once.
/// </summary>
/// <param name="reuses">Whether a connection made with the first options is reused for the second; settings equality unless a test injects another.</param>
public sealed class Unit3303MutualTlsConnectionReuse(Func<EasyTlsOptions, EasyTlsOptions, bool> reuses) : IUnitProgram
{
    private static readonly EasyTlsOptions Connection = new()
    {
        Origin = new TlsPeerOptions
        {
            Certificate = "client.pem",
            Key = "client.key",
            KeyPassword = "secret",
            CertificateType = "PEM",
            KeyType = "PEM",
        },
    };

    private static readonly (EasyTlsOptions Transfer, bool Reused, string Message)[] Checks =
    [
        (Connection, true, "identical mTLS config should match"),
        (WithOrigin(origin => origin with { KeyPassword = "wrong" }), false, "different key_passwd must not reuse conn"),
        (WithOrigin(origin => origin with { Key = "other.key" }), false, "different key must not reuse conn"),
        (WithOrigin(origin => origin with { KeyType = "DER" }), false, "different key_type must not reuse conn"),
        (WithOrigin(origin => origin with { CertificateType = "P12" }), false, "different cert_type must not reuse conn"),
        (WithOrigin(origin => origin with { }), true, "restored mTLS config should match"),
    ];

    /// <summary>Creates the port over <see cref="OptionGroupConnectionSettings"/> equality.</summary>
    public Unit3303MutualTlsConnectionReuse()
        : this((connection, transfer) => SettingsOf(connection) == SettingsOf(transfer))
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit3303";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int failures = 0;
        foreach ((EasyTlsOptions transfer, bool reused, string message) in Checks)
        {
            if (reuses(Connection, transfer) != reused)
            {
                await context.StandardError.WriteAsync($"unit3303.c Assertion FAILED: {message}\n").ConfigureAwait(false);
                failures++;
            }
        }

        return new UnitProgramResult(failures);
    }

    private static EasyTlsOptions WithOrigin(Func<TlsPeerOptions, TlsPeerOptions> change) =>
        Connection with { Origin = change(Connection.Origin) };

    private static OptionGroupConnectionSettings SettingsOf(EasyTlsOptions tls) =>
        OptionGroupConnectionSettings.Of(new EngineToolOptions(false, false, null, null), new EasyNetworkOptions(), tls, new EasyHttpOptions(), new EasyProxyOptions());
}
