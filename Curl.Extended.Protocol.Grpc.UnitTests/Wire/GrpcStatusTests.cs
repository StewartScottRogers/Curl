namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>Checks the status code names of statuscodes.md and the <c>grpc-message</c> decoding of PROTOCOL-HTTP2.md "Responses".</summary>
[TestClass]
public sealed class GrpcStatusTests
{
    [TestMethod]
    [DataRow((int)GrpcStatusCode.Ok, "OK")]
    [DataRow((int)GrpcStatusCode.Cancelled, "CANCELLED")]
    [DataRow((int)GrpcStatusCode.Unknown, "UNKNOWN")]
    [DataRow((int)GrpcStatusCode.InvalidArgument, "INVALID_ARGUMENT")]
    [DataRow((int)GrpcStatusCode.DeadlineExceeded, "DEADLINE_EXCEEDED")]
    [DataRow((int)GrpcStatusCode.NotFound, "NOT_FOUND")]
    [DataRow((int)GrpcStatusCode.AlreadyExists, "ALREADY_EXISTS")]
    [DataRow((int)GrpcStatusCode.PermissionDenied, "PERMISSION_DENIED")]
    [DataRow((int)GrpcStatusCode.ResourceExhausted, "RESOURCE_EXHAUSTED")]
    [DataRow((int)GrpcStatusCode.FailedPrecondition, "FAILED_PRECONDITION")]
    [DataRow((int)GrpcStatusCode.Aborted, "ABORTED")]
    [DataRow((int)GrpcStatusCode.OutOfRange, "OUT_OF_RANGE")]
    [DataRow((int)GrpcStatusCode.Unimplemented, "UNIMPLEMENTED")]
    [DataRow((int)GrpcStatusCode.Internal, "INTERNAL")]
    [DataRow((int)GrpcStatusCode.Unavailable, "UNAVAILABLE")]
    [DataRow((int)GrpcStatusCode.DataLoss, "DATA_LOSS")]
    [DataRow((int)GrpcStatusCode.Unauthenticated, "UNAUTHENTICATED")]
    [DataRow(17, "17")]
    [DataRow(-1, "-1")]
    public void NameOf_Responses_GivesTheStatusCodeName(int code, string expected) =>
        Assert.AreEqual(expected, GrpcStatus.NameOf((GrpcStatusCode)code));

    [TestMethod]
    [DataRow("", "")]
    [DataRow("plain", "plain")]
    [DataRow("%E2%82%AC", "€")]
    [DataRow("100%", "100%")]
    [DataRow("%4", "%4")]
    [DataRow("%zz%41", "%zzA")]
    [DataRow("%FF", "�")]
    public void Decode_Responses_PercentDecodesWithoutEverFailing(string encoded, string expected) =>
        Assert.AreEqual(expected, GrpcStatusMessage.Decode(encoded));
}
