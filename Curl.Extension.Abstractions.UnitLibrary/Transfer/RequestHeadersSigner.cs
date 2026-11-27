namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// Signs one request a handler makes for a transfer besides the one the transfer's URL names, such as a gRPC server
/// reflection request: replaces what a hook signed for the transfer's own request, such as a DPoP proof (RFC 9449 §4.2),
/// with the same signed for this request.
/// </summary>
/// <param name="method">The request's HTTP method.</param>
/// <param name="url">The request's URL.</param>
/// <param name="headers">The headers the request would carry, as <c>Name: value</c> lines.</param>
/// <returns>The headers the request is to carry; those the signer did not itself add are unchanged.</returns>
public delegate IReadOnlyList<string> RequestHeadersSigner(string method, CurlUrl url, IReadOnlyList<string> headers);
