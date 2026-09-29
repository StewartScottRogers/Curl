using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="ISaslAuthenticator" /> may authenticate a mail transfer with
/// (ADR-0121).
/// </summary>
/// <param name="Credential">
/// <see cref="ITransferContext.Credentials" />, from <c>-u</c>/<c>--user</c> or the URL's user
/// information; <see langword="null" /> when there is none.
/// </param>
/// <param name="AuthorizationIdentity">
/// <see cref="MailRequestOptions.SaslAuthorizationIdentity" />, from <c>--sasl-authzid</c>;
/// <see langword="null" /> when it was not given.
/// </param>
/// <param name="BearerToken">
/// <see cref="MailRequestOptions.BearerToken" />, from <c>--oauth2-bearer</c>;
/// <see langword="null" /> when it was not given.
/// </param>
/// <param name="RequiredMechanism">
/// The mechanism named by <c>AUTH=&lt;mech&gt;</c> in the login options; <see langword="null" />
/// or <c>*</c> allows any mechanism.
/// </param>
/// <param name="ServiceName">
/// The SASL service name: <c>smtp</c>, <c>pop</c> or <c>imap</c>, or
/// <see cref="MailRequestOptions.ServiceName" /> when <c>--service-name</c> overrides it.
/// </param>
/// <param name="Host">The URL's host, for GSSAPI, DIGEST-MD5 and OAUTHBEARER.</param>
/// <param name="Port">
/// The connection's port: the URL's, or the scheme's default when the URL names none.
/// OAUTHBEARER sends it as <c>port=</c> (ADR-0123); <c>0</c> leaves that field out, as curl
/// does for port 0.
/// </param>
public sealed record SaslRequest(
    NetworkCredential? Credential,
    string? AuthorizationIdentity,
    string? BearerToken,
    string? RequiredMechanism,
    string ServiceName,
    string Host,
    int Port = 0);
