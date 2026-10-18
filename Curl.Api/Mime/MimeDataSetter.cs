using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;

namespace Curl.Api;

/// <summary>
/// Sets a <see cref="CurlMimePart"/>'s content on the engine's part when the request is sent:
/// <c>curl_mime_data</c>, <c>curl_mime_filedata</c>, <c>curl_mime_data_cb</c> or <c>curl_mime_subparts</c>.
/// </summary>
/// <param name="part">The engine's part to fill.</param>
/// <param name="engine">The engine performing the request, for a nested body's MIME handle.</param>
/// <param name="owned">Takes every handle created, to dispose when the request ends.</param>
/// <returns>The setter's result.</returns>
internal delegate CurlCode MimeDataSetter(IMimePartBuilder part, ICurlEngine engine, ICollection<IDisposable> owned);
