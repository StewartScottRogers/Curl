using System.Net;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;

namespace Curl.Api;

/// <summary>
/// The <see cref="SocketsHttpHandler"/> properties <see cref="CurlHttpMessageHandler"/> honours for
/// decompression, redirects, authentication and cookies (BL-2512, ADR-0650), each with
/// <see cref="SocketsHttpHandler"/>'s default and mapped onto the request's easy options. Where curl's
/// engine answers differently the engine's answer stands and the property says so.
/// </summary>
public sealed partial class CurlHttpMessageHandler
{
    /// <summary>The authentication schemes asked of <see cref="Credentials"/>, strongest first, with the <c>CURLAUTH_*</c> bit each enables.</summary>
    private static readonly (string Scheme, HttpAuthenticationMethods Method)[] AuthenticationSchemes =
    [
        ("Negotiate", HttpAuthenticationMethods.Negotiate),
        ("NTLM", HttpAuthenticationMethods.Ntlm),
        ("Digest", HttpAuthenticationMethods.Digest),
        ("Basic", HttpAuthenticationMethods.Basic),
    ];

    /// <summary>
    /// The content codings asked for and decoded: <c>CURLOPT_ACCEPT_ENCODING</c> set to the codings named
    /// (<c>gzip, deflate, br</c> in that order, as <see cref="SocketsHttpHandler"/> sends them), and a
    /// decoded response's <c>Content-Encoding</c> and <c>Content-Length</c> removed. Curl's engine decodes
    /// every coding it knows in a response that asked for any, so every <c>Content-Encoding</c> value goes.
    /// Default <see cref="DecompressionMethods.None"/>: the client's <see cref="CurlHttpOptions.AcceptEncoding"/> is kept.
    /// </summary>
    public DecompressionMethods AutomaticDecompression { get; set; }

    /// <summary>
    /// Whether redirects are followed: <see langword="true"/> sets <c>CURLOPT_FOLLOWLOCATION</c> to
    /// <c>CURLFOLLOW_OBEYCODE</c>, so a <c>POST</c> becomes a <c>GET</c> after 301, 302 and 303 and any other
    /// method after 303, and the response's <see cref="HttpRequestMessage.RequestUri"/> becomes the
    /// redirected URL; <see langword="false"/> sets it to 0. Curl's engine follows an HTTPS to HTTP redirect,
    /// which <see cref="SocketsHttpHandler"/> does not. Default <see langword="true"/>.
    /// </summary>
    public bool AllowAutoRedirect { get; set; } = true;

    /// <summary>
    /// The most redirects followed (<c>CURLOPT_MAXREDIRS</c>). One more fails the transfer with
    /// <c>CURLE_TOO_MANY_REDIRECTS</c>, thrown as <see cref="HttpRequestException"/>, where
    /// <see cref="SocketsHttpHandler"/> returns the last redirect response. Default 50.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxAutomaticRedirections
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 50;

    /// <summary>
    /// The credentials for the origin. The strongest scheme of Negotiate, NTLM, Digest and Basic they give
    /// a credential for sets <c>CURLOPT_USERNAME</c> (<c>domain\user</c> when it has a domain) and
    /// <c>CURLOPT_PASSWORD</c>, and every scheme giving that same credential sets its <c>CURLOPT_HTTPAUTH</c>
    /// bit; a credential with no user name (<see cref="CredentialCache.DefaultCredentials"/>) is the logged-on
    /// user's, for Negotiate and NTLM only. Default <see langword="null"/>: the client's
    /// <see cref="CurlClientOptions.Authentication"/> is kept.
    /// </summary>
    public ICredentials? Credentials { get; set; }

    /// <summary>
    /// Whether the credentials are sent before the server asks: <see langword="false"/> adds
    /// <c>CURLAUTH_ONLY</c>, so the engine first sends the request without them and answers the
    /// <c>401</c> challenge, as <see cref="SocketsHttpHandler"/> does. Default <see langword="false"/>.
    /// </summary>
    public bool PreAuthenticate { get; set; }

    /// <summary>
    /// Whether <see cref="CookieContainer"/> is used: its cookies for the URL go out as <c>CURLOPT_COOKIE</c>,
    /// and the final response's <c>Set-Cookie</c> fields are stored for its URL, an invalid one skipped. A
    /// followed redirect's own <c>Set-Cookie</c> fields never reach the handler, so they are not stored.
    /// Default <see langword="true"/>.
    /// </summary>
    public bool UseCookies { get; set; } = true;

    /// <summary>The cookies <see cref="UseCookies"/> sends and stores. Default an empty container.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public CookieContainer CookieContainer
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new();

    /// <summary><paramref name="curlRequest"/> with the decompression, redirect, cookie and credential properties applied for <paramref name="uri"/>.</summary>
    /// <param name="curlRequest">The mapped request; its <see cref="CurlRequest.Http"/> is set.</param>
    /// <param name="uri">The request's absolute URI.</param>
    /// <returns>The request to send.</returns>
    private CurlRequest WithBehaviour(CurlRequest curlRequest, Uri uri)
    {
        CurlHttpOptions http = curlRequest.Http! with
        {
            FollowLocation = AllowAutoRedirect ? FollowLocation.ObeyCode : FollowLocation.None,
            MaxRedirects = MaxAutomaticRedirections,
        };
        http = AutomaticDecompression == DecompressionMethods.None ? http : http with { AcceptEncoding = AcceptEncodingOf(AutomaticDecompression), HttpContentDecoding = true };
        string cookies = UseCookies ? CookieContainer.GetCookieHeader(uri) : "";
        http = cookies.Length == 0 ? http : http with { Cookie = cookies };
        return curlRequest with { Http = http, Authentication = AuthenticationFor(uri) };
    }

    /// <summary>
    /// Updates <paramref name="message"/> after the transfer: the request URI to the redirected URL, a decoded
    /// content's encoding and length headers removed, and the response's cookies stored.
    /// </summary>
    /// <param name="message">The response message.</param>
    /// <param name="response">The response it was made from.</param>
    private void ApplyBehaviour(HttpResponseMessage message, CurlResponse response)
    {
        HttpRequestMessage request = message.RequestMessage!;
        if (AllowAutoRedirect && Uri.TryCreate(response.Info.EffectiveUrl, UriKind.Absolute, out Uri? effective))
        {
            request.RequestUri = effective;
        }

        if (AutomaticDecompression != DecompressionMethods.None)
        {
            message.Content.Headers.ContentEncoding.Clear();
            message.Content.Headers.ContentLength = null;
        }

        if (UseCookies)
        {
            StoreCookies(response.Headers, request.RequestUri!);
        }
    }

    /// <summary>Stores each <c>Set-Cookie</c> field of <paramref name="headers"/> for <paramref name="uri"/>, skipping one the container refuses.</summary>
    /// <param name="headers">The response's header fields.</param>
    /// <param name="uri">The URL the response came from.</param>
    private void StoreCookies(IReadOnlyList<KeyValuePair<string, string>> headers, Uri uri)
    {
        foreach ((string name, string value) in headers)
        {
            if (name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    CookieContainer.SetCookies(uri, value);
                }
                catch (CookieException)
                {
                    // SocketsHttpHandler skips a cookie it cannot parse too.
                }
            }
        }
    }

    /// <summary>The <c>CURLOPT_ACCEPT_ENCODING</c> list for <paramref name="methods"/>.</summary>
    /// <param name="methods">The decompression methods; not <see cref="DecompressionMethods.None"/>.</param>
    /// <returns>The codings named, comma separated.</returns>
    internal static string AcceptEncodingOf(DecompressionMethods methods)
    {
        IEnumerable<string> codings =
            from coding in new[] { (DecompressionMethods.GZip, "gzip"), (DecompressionMethods.Deflate, "deflate"), (DecompressionMethods.Brotli, "br") }
            where methods.HasFlag(coding.Item1)
            select coding.Item2;
        return string.Join(", ", codings);
    }

    /// <summary>
    /// The client's authentication options with <see cref="Credentials"/>' user, password and schemes for
    /// <paramref name="uri"/>; the client's own (<see langword="null"/> for libcurl's) when there are no credentials.
    /// </summary>
    /// <param name="uri">The request's URI.</param>
    /// <returns>The authentication options for the request.</returns>
    private CurlAuthenticationOptions? AuthenticationFor(Uri uri)
    {
        CurlAuthenticationOptions? clients = Client.Options.Authentication;
        if (Credentials is null)
        {
            return clients;
        }

        (NetworkCredential Credential, HttpAuthenticationMethods Methods)? chosen = CredentialFor(Credentials, uri);
        if (chosen is not { } found)
        {
            return clients;
        }

        HttpAuthenticationMethods only = PreAuthenticate ? HttpAuthenticationMethods.None : HttpAuthenticationMethods.Only;
        return (clients ?? new CurlAuthenticationOptions()) with
        {
            UserPassword = null,
            UserName = UserNameOf(found.Credential),
            Password = found.Credential.Password,
            HttpAuthentication = found.Methods | only,
        };
    }

    /// <summary>
    /// The strongest credential <paramref name="credentials"/> gives for <paramref name="uri"/> and the schemes
    /// giving that same credential; a credential with no user name serves Negotiate and NTLM only.
    /// </summary>
    /// <param name="credentials">The credentials.</param>
    /// <param name="uri">The request's URI.</param>
    /// <returns>The credential and its schemes, or <see langword="null"/> when no scheme has one.</returns>
    internal static (NetworkCredential Credential, HttpAuthenticationMethods Methods)? CredentialFor(ICredentials credentials, Uri uri)
    {
        List<(NetworkCredential Credential, HttpAuthenticationMethods Method)> usable =
        [
            .. from scheme in AuthenticationSchemes
               let credential = UsableCredential(credentials.GetCredential(uri, scheme.Scheme), scheme.Method)
               where credential is not null
               select (credential!, scheme.Method),
        ];
        if (usable.Count == 0)
        {
            return null;
        }

        NetworkCredential chosen = usable[0].Credential;
        HttpAuthenticationMethods methods = usable
            .Where(candidate => SameCredential(chosen, candidate.Credential))
            .Aggregate(HttpAuthenticationMethods.None, (all, candidate) => all | candidate.Method);
        return (chosen, methods);
    }

    /// <summary><paramref name="credential"/> when it can serve <paramref name="method"/>: one with no user name serves Negotiate and NTLM only.</summary>
    /// <param name="credential">The credential the scheme was given, or <see langword="null"/>.</param>
    /// <param name="method">The scheme's <c>CURLAUTH_*</c> bit.</param>
    /// <returns>The credential, or <see langword="null"/> when it cannot serve the scheme.</returns>
    private static NetworkCredential? UsableCredential(NetworkCredential? credential, HttpAuthenticationMethods method) =>
        credential is not null && (credential.UserName.Length > 0 || method is HttpAuthenticationMethods.Negotiate or HttpAuthenticationMethods.Ntlm)
            ? credential
            : null;

    /// <summary>Whether two credentials hold the same user, domain and password.</summary>
    /// <param name="first">The first credential.</param>
    /// <param name="second">The second credential.</param>
    /// <returns><see langword="true"/> when all three match.</returns>
    private static bool SameCredential(NetworkCredential first, NetworkCredential second) =>
        first.UserName == second.UserName && first.Domain == second.Domain && first.Password == second.Password;

    /// <summary><c>domain\user</c> for a credential with a domain, else its user name.</summary>
    /// <param name="credential">The credential.</param>
    /// <returns>The <c>CURLOPT_USERNAME</c> value.</returns>
    private static string UserNameOf(NetworkCredential credential) =>
        credential.Domain.Length == 0 ? credential.UserName : $"{credential.Domain}\\{credential.UserName}";
}
