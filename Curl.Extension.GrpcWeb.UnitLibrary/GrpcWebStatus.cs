namespace Curl.Extension.GrpcWeb;

/// <summary>
/// Checks a gRPC-Web call's <c>grpc-status</c>, from the trailers frame or, in a trailers-only response, the response
/// headers (PROTOCOL-WEB.md, "Message framing"). Status 0, OK, leaves the transfer as it is; any other fails it with
/// exit 22 and the status and its percent-decoded <c>grpc-message</c>; none at all fails it with exit 8.
/// </summary>
internal static class GrpcWebStatus
{
    /// <summary>Gives the transfer's result once the call's status is checked.</summary>
    /// <param name="fields">The trailers, or the response headers when the body had none.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns><paramref name="result" /> for status 0, or a failure with its report.</returns>
    internal static TransferResult Check(IReadOnlyList<KeyValuePair<string, string>> fields, TransferResult result)
    {
        string? status = ValueOf(fields, "grpc-status");
        if (status is null)
        {
            return Failure(CurlExitCode.WeirdServerReply, "--grpc-web: the reply has no grpc-status (PROTOCOL-WEB.md, Message framing)", result);
        }

        if (status == "0")
        {
            return result;
        }

        string? message = ValueOf(fields, "grpc-message");
        string text = message is null
            ? $"--grpc-web: the call failed with grpc-status {status}"
            : $"--grpc-web: the call failed with grpc-status {status}: {Uri.UnescapeDataString(message)}";
        return Failure(CurlExitCode.HttpReturnedError, text, result);
    }

    private static string? ValueOf(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        fields.Where(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(field => field.Value.Trim()).FirstOrDefault();

    private static TransferResult Failure(CurlExitCode code, string message, TransferResult result) =>
        TransferResult.Failure(code, message, result.BytesTransferred) with { Report = result.Report };
}
