using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Looks up the credentials one <see cref="RedirectFollower" /> hop after the first sends from that
/// hop's own URL, as curl 8.21.0 looks the netrc file up again for each hop's host under <c>-n</c>
/// with <c>-L</c>, <c>--location-trusted</c> or not (BL-790 Notes).
/// </summary>
/// <param name="url">The hop's URL.</param>
/// <returns>The credentials the hop sends, or <see langword="null" /> to send none.</returns>
public delegate NetworkCredential? HopCredentialSelector(CurlUrl url);
