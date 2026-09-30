using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Looks up the <c>--alt-svc</c> alternative for one <see cref="RedirectFollower" /> hop from that
/// hop's own URL, as curl 8.21.0 looks it up again for each connection a redirect needs.
/// </summary>
/// <param name="url">The hop's URL.</param>
/// <param name="http">
/// The hop's HTTP options, still carrying the first hop's route and version, which the selector replaces.
/// </param>
/// <returns>
/// <paramref name="http" /> with the hop's <see cref="HttpRequestOptions.AltSvcRoute" /> and the
/// <see cref="HttpRequestOptions.Version" /> the alternative leaves the hop to use.
/// </returns>
public delegate HttpRequestOptions HopAltSvcSelector(CurlUrl url, HttpRequestOptions http);
