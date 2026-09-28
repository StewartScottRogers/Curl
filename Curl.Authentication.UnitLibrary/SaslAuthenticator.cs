using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Chooses a SASL mechanism as curl 8.21.0 does and answers PLAIN, LOGIN, EXTERNAL, XOAUTH2
/// and OAUTHBEARER with the bytes curl sends (ADR-0121, ADR-0123).
/// </summary>
/// <param name="credentialEncoding">
/// The encoding the user name, password, authorization identity and token are sent in; see
/// <see cref="CredentialEncoding.ForPlatform" />.
/// </param>
/// <remarks>
/// <para>
/// The preference order is EXTERNAL, GSSAPI, DIGEST-MD5, CRAM-MD5, NTLM, OAUTHBEARER,
/// XOAUTH2, PLAIN, LOGIN. GSSAPI, DIGEST-MD5, CRAM-MD5 and NTLM are not built yet (BL-537,
/// BL-538) and are treated as not offered.
/// </para>
/// <para>
/// Every mechanism here has an initial response, LOGIN's being the user name, as curl sends
/// with <c>--sasl-ir</c>. Without it the handler sends the initial response in answer to the
/// server's first challenge, and <see cref="ISaslExchange.Respond" /> answers the challenges
/// after that: LOGIN's password, and OAUTHBEARER's single <c>0x01</c> byte acknowledging an
/// error continuation.
/// </para>
/// </remarks>
public sealed class SaslAuthenticator(Encoding credentialEncoding) : ISaslAuthenticator
{
    /// <summary>The byte OAUTHBEARER and XOAUTH2 separate their fields with (RFC 7628 section 3.1).</summary>
    private const char FieldSeparator = '\u0001';

    // Each built mechanism's messages: the initial response first, then the answers to the
    // challenges after it. OAUTHBEARER acknowledges an error continuation with 0x01, as curl
    // does; the others answer only what curl answers.
    private static readonly Dictionary<string, Func<SaslRequest, string[]>> MessagesByMechanism =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [SaslMechanismRanking.Plain] = request =>
                [(request.AuthorizationIdentity ?? string.Empty) + "\0" + UserOf(request) + "\0" + PasswordOf(request)],
            [SaslMechanismRanking.Login] = request => [UserOf(request), PasswordOf(request)],
            [SaslMechanismRanking.External] = request => [UserOf(request)],
            [SaslMechanismRanking.XOAuth2] = request =>
                [$"user={UserOf(request)}{FieldSeparator}auth=Bearer {TokenOf(request)}{FieldSeparator}{FieldSeparator}"],
            [SaslMechanismRanking.OAuthBearer] = request =>
                [OAuthBearerMessage(UserOf(request), request.Host, port: null, TokenOf(request)), FieldSeparator.ToString()],
        };

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms) =>
        SaslMechanismRanking.PickFirst(request, offeredMechanisms);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// <paramref name="mechanism" /> is not one this authenticator builds.
    /// </exception>
    public ISaslExchange Begin(string mechanism, SaslRequest request)
    {
        if (!MessagesByMechanism.TryGetValue(mechanism, out Func<SaslRequest, string[]>? buildMessages))
        {
            throw new ArgumentException($"The SASL mechanism '{mechanism}' is not built.", nameof(mechanism));
        }

        byte[][] messages = [.. buildMessages(request).Select(credentialEncoding.GetBytes)];
        return new ScriptedSaslExchange(mechanism.ToUpperInvariant(), messages[0], messages[1..]);
    }

    /// <summary>
    /// Builds the OAUTHBEARER client message (RFC 7628 section 3.1) as curl 8.21.0 does.
    /// </summary>
    /// <param name="user">The user name, sent as the GS2 authorization identity <c>a=</c>.</param>
    /// <param name="host">The server's host name.</param>
    /// <param name="port">
    /// The server's port, which curl always sends; <see langword="null" /> leaves the
    /// <c>port=</c> field out, as curl does for port 0, until <see cref="SaslRequest" />
    /// carries the port.
    /// </param>
    /// <param name="token">The bearer token.</param>
    /// <returns>The message, before encoding.</returns>
    internal static string OAuthBearerMessage(string user, string host, int? port, string token)
    {
        string portField = port is { } value
            ? "port=" + value.ToString(CultureInfo.InvariantCulture) + FieldSeparator
            : string.Empty;
        return $"n,a={user},{FieldSeparator}host={host}{FieldSeparator}{portField}auth=Bearer {token}{FieldSeparator}{FieldSeparator}";
    }

    // curl sends an empty user name, password or token for one that was not given.
    private static string UserOf(SaslRequest request) => request.Credential?.UserName ?? string.Empty;

    private static string PasswordOf(SaslRequest request) => request.Credential is { } credential ? credential.Password : string.Empty;

    private static string TokenOf(SaslRequest request) => request.BearerToken ?? string.Empty;
}
