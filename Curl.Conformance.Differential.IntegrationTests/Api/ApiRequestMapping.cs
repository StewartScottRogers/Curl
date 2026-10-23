using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Conformance.Differential.Api;

namespace Curl.Conformance.Differential.IntegrationTests.Api;

/// <summary>Maps an <see cref="ApiDifferentialRequest"/> onto the <see cref="CurlRequest"/> that makes it.</summary>
internal static class ApiRequestMapping
{
    /// <summary>Gets the request for <paramref name="request"/> against the loopback server at <paramref name="baseUrl"/>.</summary>
    public static CurlRequest ToCurlRequest(ApiDifferentialRequest request, string baseUrl) =>
        new(new HttpMethod(request.Method), baseUrl + request.Path)
        {
            Headers = request.Headers,
            Body = BodyOf(request),
            Http = new CurlHttpOptions
            {
                UserAgent = request.UserAgent,
                FollowLocation = request.FollowsRedirects ? FollowLocation.All : FollowLocation.None,
                Cookie = request.Cookie,
                CookieFiles = request.StartsCookieEngine ? [""] : [],
            },
            Authentication = request.UserName is null ? null : new CurlAuthenticationOptions
            {
                UserName = request.UserName,
                Password = request.Password,
                HttpAuthentication = request.UsesDigest ? HttpAuthenticationMethods.Digest : HttpAuthenticationMethods.Basic,
            },
        };

    private static CurlContent? BodyOf(ApiDifferentialRequest request) =>
        request.FormFields.Count > 0
            ? new CurlMultipartContent(request.FormFields.Select(field => CurlMimePart.Text(field.Key, field.Value)))
            : request.Body is null ? null : CurlContent.FromBytes(Encoding.UTF8.GetBytes(request.Body));
}
