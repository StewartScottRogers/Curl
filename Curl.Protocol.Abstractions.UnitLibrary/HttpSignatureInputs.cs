namespace Curl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="IHttpAuthenticator" /> needs beyond an <see cref="HttpAuthRequest" />'s
/// method and URL to sign the request with the <c>--httpsig-*</c> options (BL-2182): the
/// settings, the <c>@authority</c> value and the <c>-H</c> headers, as the HTTP handler sends them.
/// </summary>
/// <param name="Settings">The <c>--httpsig-*</c> settings (<see cref="HttpRequestOptions.HttpSignature" />).</param>
/// <param name="Authority">
/// The <c>@authority</c> value: an <c>-H</c> <c>Host</c> header's value when one is sent, else the
/// <c>Host</c> value curl's own head carries, such as <c>example.com:8000</c>.
/// </param>
/// <param name="CustomHeaders">The <c>-H</c> headers, each exactly as given (<see cref="HttpRequestOptions.Headers" />).</param>
public sealed record HttpSignatureInputs(HttpSignatureSettings Settings, string Authority, IReadOnlyList<string> CustomHeaders);
