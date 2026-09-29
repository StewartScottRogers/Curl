using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Serves the <c>ldap</c> and <c>ldaps</c> schemes as the reference build
/// <see cref="LdapDialect" /> names does (ADR-0166): connects, binds, and leaves with an
/// UnbindRequest.
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port, TLS from the first byte for
/// <c>ldaps</c> (ADR-0005). No <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <param name="dialect">The build to answer as, chosen from the platform by the composition root.</param>
/// <remarks>
/// <para>
/// The port defaults to 389 for <c>ldap</c> and 636 for <c>ldaps</c>. The bind is a simple
/// LDAPv3 bind with <see cref="ITransferContext.Credentials" />' user name as the DN and its
/// password, or the anonymous bind (empty DN and password) without <c>-u</c>; how it fails
/// is <see cref="LdapBind" />'s. A connect failure is returned as the connector reported it.
/// </para>
/// <para>
/// This is the bind only (BL-586). Once bound it sends the UnbindRequest and succeeds with
/// nothing written: the SearchRequest the URL names comes with BL-587, the output with
/// BL-588, and the Windows build's bind without <c>-u</c> - the rootDSE read, then WinLDAP's
/// NTLM bind as the logged-on user, which ADR-0166 decides - with its own task, so the
/// WinLDAP dialect binds anonymously until then. The handler is not registered in <c>Curl.Console</c> until BL-589.
/// </para>
/// </remarks>
public sealed class LdapProtocolHandler(IConnector connector, LdapDialect dialect) : IProtocolHandler
{
    private const int DefaultPort = 389;

    private const int DefaultTlsPort = 636;

    private const string TlsScheme = "ldaps";

    /// <summary>The schemes this handler serves, as curl 8.21.0's <c>--version</c> protocol list names them.</summary>
    private static readonly string[] Schemes = ["ldap", TlsScheme];

    private readonly IConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    private readonly LdapDialect dialect = Enum.IsDefined(dialect)
        ? dialect
        : throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "The LDAP dialect is WinLdap or OpenLdap.");

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ConnectResult connect = await connector.ConnectAsync(TargetOf(context.Url), context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            return await BindAndLeaveAsync(connection, context).ConfigureAwait(false);
        }
    }

    /// <summary>The host and port <paramref name="url" /> names, with TLS from the first byte for <c>ldaps</c>.</summary>
    private static ConnectTarget TargetOf(CurlUrl url)
    {
        bool useTls = url.Scheme == TlsScheme;
        int defaultPort = useTls ? DefaultTlsPort : DefaultPort;
        return new ConnectTarget(url.IdnHost, url.IsDefaultPort ? defaultPort : url.Port, useTls);
    }

    /// <summary>Binds with the transfer's credentials, or anonymously without them, then sends the UnbindRequest.</summary>
    private async ValueTask<TransferResult> BindAndLeaveAsync(IConnection connection, ITransferContext context)
    {
        var exchange = new LdapExchange(connection, new LdapBerWriter(dialect));
        NetworkCredential credentials = context.Credentials ?? new NetworkCredential(string.Empty, string.Empty);
        TransferResult? bindFailure = await LdapBind.BindAsync(
            dialect,
            exchange,
            credentials.UserName,
            credentials.Password,
            context.CancellationToken).ConfigureAwait(false);
        if (bindFailure is not null)
        {
            return bindFailure;
        }

        await exchange.UnbindAsync(context.CancellationToken).ConfigureAwait(false);
        return TransferResult.Success(0);
    }
}
