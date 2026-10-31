namespace Curl.Extension.JsonRpc;

/// <summary>One call a <c>--jsonrpc</c> value names: a Request object of JSON-RPC 2.0 section 4.</summary>
/// <param name="Method">The method to invoke.</param>
/// <param name="Params">The params as JSON text, an array or an object (section 4.2), or <see langword="null" /> when omitted.</param>
/// <param name="Id">The call's id, or <see langword="null" /> for a notification, which has none and gets no reply (section 4.1).</param>
internal sealed record JsonRpcCall(string Method, string? Params, int? Id);
