using System.Globalization;

namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>
/// The outcome of a gRPC call: its <c>grpc-status</c> code and its <c>grpc-message</c>, percent-decoded.
/// </summary>
/// <param name="Code">The status code; a code statuscodes.md does not define keeps its number.</param>
/// <param name="Message">The decoded status message, empty when the server sent none.</param>
internal sealed record GrpcStatus(GrpcStatusCode Code, string Message)
{
    private static readonly string[] CodeNames =
    [
        "OK", "CANCELLED", "UNKNOWN", "INVALID_ARGUMENT", "DEADLINE_EXCEEDED", "NOT_FOUND", "ALREADY_EXISTS",
        "PERMISSION_DENIED", "RESOURCE_EXHAUSTED", "FAILED_PRECONDITION", "ABORTED", "OUT_OF_RANGE",
        "UNIMPLEMENTED", "INTERNAL", "UNAVAILABLE", "DATA_LOSS", "UNAUTHENTICATED",
    ];

    /// <summary>
    /// Gets the code's name as statuscodes.md writes it (<c>NOT_FOUND</c>), or its number when it defines none.
    /// </summary>
    public string CodeName => NameOf(Code);

    /// <summary>
    /// Gives the name statuscodes.md writes for <paramref name="code" />, or its number when it defines none.
    /// </summary>
    /// <param name="code">The status code.</param>
    /// <returns>The name, for example <c>DEADLINE_EXCEEDED</c>.</returns>
    public static string NameOf(GrpcStatusCode code) =>
        (uint)code < (uint)CodeNames.Length ? CodeNames[(int)code] : ((int)code).ToString(CultureInfo.InvariantCulture);
}
