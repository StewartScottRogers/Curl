using System.Collections.Concurrent;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Answers with <c>Authorization: Negotiate &lt;base64&gt;</c> as curl 8.21.0 does
/// (RFC 4559, ADR-0142, ADR-0176, ADR-0227): the first token of a Negotiate context for the
/// service <c>HTTP</c> on the URL's host, or the <c>--service-name</c> (for a proxy
/// <c>--proxy-service-name</c>) service when given, with the <c>--delegation</c> level
/// (ADR-0188), from <paramref name="securityContexts" />; and, when a 401 carries the
/// acceptor's token back, the same context's next token. When no token can be made - no
/// ticket, no logged-on user's credential, no mechanism - it answers nothing, and the
/// transfer ends on the 401 with exit 0, as both platform curls do, and the context's failure
/// is reported to <see cref="HttpAuthRequest.Events" /> in the platform curl's words
/// (ADR-0231).
/// </summary>
/// <param name="securityContexts">Makes the Negotiate context; ADR-0142's router in production.</param>
/// <param name="options">The service names and delegation level; <see cref="NegotiateOptions.Default" /> when <see langword="null" />.</param>
/// <param name="wordsFailuresAsSspi">
/// <see langword="true" /> to word a context's failure as curl's SSPI build does,
/// <see langword="false" /> as its GSS-API build does; <see langword="null" /> for this
/// platform's curl: SSPI on Windows, GSS-API elsewhere.
/// </param>
/// <remarks>
/// An explicit <c>-u user:password</c> is passed on (a <c>DOMAIN\user</c> or
/// <c>DOMAIN/user</c> name split into its domain and user, as curl's SSPI build splits it),
/// where only SSPI uses it; <c>-u :</c> and no <c>-u</c> mean the default credentials. A
/// context that needs another leg is kept, keyed by the header value its token made, until
/// the request that sent it draws a continuation, because a Kerberos context cannot be made
/// again: its authenticator is fresh every time (ADR-0227).
/// </remarks>
public sealed class NegotiateHttpAuthenticator(ISecurityContextFactory securityContexts, NegotiateOptions? options = null, bool? wordsFailuresAsSspi = null)
{
    /// <summary>The service name of an HTTP acceptor's principal, before <c>--service-name</c> changes it.</summary>
    public const string HttpServiceName = "HTTP";

    /// <summary>What every header value this authenticator makes starts with.</summary>
    public const string SchemePrefix = "Negotiate ";

    private readonly NegotiateOptions options = options ?? NegotiateOptions.Default;

    private readonly bool wordsFailuresAsSspi = wordsFailuresAsSspi ?? OperatingSystem.IsWindows();

    private readonly ConcurrentDictionary<string, ISecurityContext> contextsAwaitingALeg = new(StringComparer.Ordinal);

    /// <summary>Makes the first <c>Negotiate</c> header value for <paramref name="request" />.</summary>
    /// <param name="request">The request being authorised.</param>
    /// <param name="cancellationToken">Cancels a KDC exchange.</param>
    /// <returns>The header value, or <see langword="null" /> when no token can be made.</returns>
    public async ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ISecurityContext context = securityContexts.Create(ContextRequestFor(request));
        return await StepAsync(context, ReadOnlyMemory<byte>.Empty, request.Events, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Steps a new context for <paramref name="request" /> and disposes of it without making a
    /// header value, as curl's <c>Curl_input_negotiate</c> does for a 401 it will not answer
    /// because no <c>-u</c> was given: nothing is sent, but a failure is still reported to
    /// <see cref="HttpAuthRequest.Events" /> (measured, BL-843 Notes).
    /// </summary>
    /// <param name="request">The request being authorised.</param>
    /// <param name="cancellationToken">Cancels a KDC exchange.</param>
    /// <returns>A task that completes when the context has stepped.</returns>
    public async ValueTask StepWithoutAnsweringAsync(HttpAuthRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using ISecurityContext context = securityContexts.Create(ContextRequestFor(request));
        ReportFailure(await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false), request.Events);
    }

    /// <summary>
    /// Makes the next <c>Negotiate</c> header value from the acceptor's token in
    /// <paramref name="challenges" />, as curl's <c>Curl_input_negotiate</c> does: only for the
    /// context that made <paramref name="sentAuthorization" /> and still needs a leg, and only
    /// when the challenge carries a token that decodes; anything else ends the transfer on the
    /// 401.
    /// </summary>
    /// <param name="request">The request being authorised; a failure is reported to its <see cref="HttpAuthRequest.Events" />.</param>
    /// <param name="sentAuthorization">The header value the request that drew the challenges sent.</param>
    /// <param name="challenges">The response's challenges.</param>
    /// <param name="cancellationToken">Cancels the context's step.</param>
    /// <returns>The header value, or <see langword="null" /> to take the response as the result.</returns>
    public async ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sentAuthorization);
        ArgumentNullException.ThrowIfNull(challenges);
        if (!contextsAwaitingALeg.TryRemove(sentAuthorization, out ISecurityContext? context))
        {
            return null;
        }

        if (DecodeBase64(HttpChallengeSchemes.NegotiateTokenOf(challenges)) is not { Length: > 0 } incomingToken)
        {
            context.Dispose();
            return null;
        }

        return await StepAsync(context, incomingToken, request.Events, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the security context request <paramref name="request" /> comes to.</summary>
    /// <param name="request">The request being authorised.</param>
    /// <returns>
    /// A Negotiate request for the server's (or proxy's) service on the URL's host, with the
    /// delegation level and the explicit credential if any.
    /// </returns>
    internal SecurityContextRequest ContextRequestFor(HttpAuthRequest request)
    {
        string serviceName = (request.IsProxy ? options.ProxyServiceName : options.ServiceName) ?? HttpServiceName;
        SecurityContextRequest defaults = new(SecurityMechanism.Negotiate, serviceName, request.Url.IdnHost) { Delegation = options.Delegation };
        if (request.Credential is not { UserName.Length: > 0 } credential)
        {
            return defaults;
        }

        (string? domain, string user) = SplitDomain(credential);
        return defaults with { UserName = user, Password = credential.Password, Domain = domain };
    }

    private static (string? Domain, string User) SplitDomain(NetworkCredential credential)
    {
        string name = credential.UserName;
        int separator = name.IndexOfAny(['\\', '/']);
        return separator < 0
            ? (credential.Domain.Length == 0 ? null : credential.Domain, name)
            : (name[..separator], name[(separator + 1)..]);
    }

    private static byte[]? DecodeBase64(string? text)
    {
        try
        {
            return text is null ? null : Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Steps <paramref name="context" /> with <paramref name="incomingToken" /> and makes the
    /// header value from its token, keeping the context for the next leg when it needs one
    /// and disposing of it otherwise; a failure is reported to <paramref name="events" />.
    /// </summary>
    private async ValueTask<string?> StepAsync(ISecurityContext context, ReadOnlyMemory<byte> incomingToken, ITransferEvents events, CancellationToken cancellationToken)
    {
        SecurityContextStep step;
        try
        {
            step = await context.NextTokenAsync(incomingToken, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            context.Dispose();
            throw;
        }

        ReportFailure(step, events);
        string? header = step.Status is SecurityContextStatus.ContinueNeeded or SecurityContextStatus.Completed && step.Token.Length != 0
            ? SchemePrefix + Convert.ToBase64String(step.Token)
            : null;
        if (header is not null && step.Status == SecurityContextStatus.ContinueNeeded)
        {
            KeepForTheNextLeg(header, context);
        }
        else
        {
            context.Dispose();
        }

        return header;
    }

    /// <summary>
    /// Reports the platform curl's failure line to <paramref name="events" /> when
    /// <paramref name="step" /> failed; does nothing for a step that went on or completed.
    /// </summary>
    private void ReportFailure(SecurityContextStep step, ITransferEvents events)
    {
        if (step.Status is not (SecurityContextStatus.ContinueNeeded or SecurityContextStatus.Completed))
        {
            events.ReportInfo(NegotiateFailureLines.For(step.Status, wordsFailuresAsSspi));
        }
    }

    private void KeepForTheNextLeg(string header, ISecurityContext context)
    {
        if (contextsAwaitingALeg.TryRemove(header, out ISecurityContext? replaced))
        {
            replaced.Dispose();
        }

        contextsAwaitingALeg[header] = context;
    }
}
