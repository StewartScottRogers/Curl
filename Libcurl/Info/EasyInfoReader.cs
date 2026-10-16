using Curl.Api.Info;
using Curl.Api.Options;

namespace Libcurl.Info;

/// <summary>
/// Reads one <see cref="CurlInfo"/> from a handle: the row an info group adds to
/// <see cref="EasyInfoReaders"/>.
/// </summary>
/// <param name="info">What the handle's last transfer reported.</param>
/// <param name="options">The handle's options (<see cref="CurlInfo.Private"/> reads <c>CURLOPT_PRIVATE</c> there).</param>
/// <returns>The value, of the .NET type <see cref="CurlInfo"/>'s remarks give for the info's base.</returns>
internal delegate object? EasyInfoReader(TransferInfo info, EasyOptions options);
