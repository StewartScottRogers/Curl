namespace Curl.Extension.Protocol.Grpc.Wire;

/// <summary>Checks the Response-Headers, Trailers-Only and Trailers of PROTOCOL-HTTP2.md "Responses".</summary>
[TestClass]
public sealed class GrpcResponseHeadersTests
{
    [TestMethod]
    [DataRow("application/grpc")]
    [DataRow("application/grpc+proto")]
    [DataRow("Application/GRPC; charset=utf-8")]
    public void ReadHeaders_Responses_GrpcContentType_ExpectsMessagesAndTrailers(string contentType)
    {
        var head = GrpcResponseHeaders.ReadHeaders(Fields((":status", "200"), ("content-type", contentType)));

        Assert.AreEqual(new GrpcResponseHead(200, false, null), head);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("text/html")]
    [DataRow("application/grpcx")]
    public void ReadHeaders_Responses_OtherContentType_FailsWithAWireError(string? contentType)
    {
        var fields = contentType is null ? Fields((":status", "200")) : Fields((":status", "200"), ("content-type", contentType));

        Assert.ThrowsExactly<GrpcWireException>(() => GrpcResponseHeaders.ReadHeaders(fields));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("abc")]
    public void ReadHeaders_Responses_NoValidStatus_FailsWithAWireError(string? status)
    {
        var fields = status is null ? Fields(("content-type", "application/grpc")) : Fields((":status", status));

        Assert.ThrowsExactly<GrpcWireException>(() => GrpcResponseHeaders.ReadHeaders(fields));
    }

    [TestMethod]
    public void ReadHeaders_Responses_TrailersOnly_EndsTheCallWithItsStatus()
    {
        var head = GrpcResponseHeaders.ReadHeaders(Fields(
            (":status", "200"),
            ("content-type", "application/grpc"),
            ("grpc-status", "12"),
            ("grpc-message", "unknown%20method")));

        Assert.IsTrue(head.IsTrailersOnly);
        Assert.AreEqual(new GrpcStatus(GrpcStatusCode.Unimplemented, "unknown method"), head.FinalStatus);
        Assert.AreEqual("UNIMPLEMENTED", head.FinalStatus!.CodeName);
    }

    [TestMethod]
    [DataRow(400, (int)GrpcStatusCode.Internal)]
    [DataRow(401, (int)GrpcStatusCode.Unauthenticated)]
    [DataRow(403, (int)GrpcStatusCode.PermissionDenied)]
    [DataRow(404, (int)GrpcStatusCode.Unimplemented)]
    [DataRow(429, (int)GrpcStatusCode.Unavailable)]
    [DataRow(502, (int)GrpcStatusCode.Unavailable)]
    [DataRow(503, (int)GrpcStatusCode.Unavailable)]
    [DataRow(504, (int)GrpcStatusCode.Unavailable)]
    [DataRow(500, (int)GrpcStatusCode.Unknown)]
    public void ReadHeaders_Responses_HttpStatusOtherThan200_MapsToAGrpcStatus(int httpStatus, int expected)
    {
        var head = GrpcResponseHeaders.ReadHeaders(Fields((":status", httpStatus.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        Assert.IsFalse(head.IsTrailersOnly);
        Assert.AreEqual(new GrpcStatus((GrpcStatusCode)expected, $"HTTP status {httpStatus}"), head.FinalStatus);
    }

    [TestMethod]
    public void ReadTrailers_Responses_GivesTheStatusAndDecodedMessage()
    {
        var status = GrpcResponseHeaders.ReadTrailers(Fields(("grpc-status", "5"), ("grpc-message", "caf%C3%A9 %25 not found")));

        Assert.AreEqual(new GrpcStatus(GrpcStatusCode.NotFound, "café % not found"), status);
        Assert.AreEqual("NOT_FOUND", status.CodeName);
    }

    [TestMethod]
    public void ReadTrailers_Responses_NoMessage_IsEmpty() =>
        Assert.AreEqual(new GrpcStatus(GrpcStatusCode.Ok, string.Empty), GrpcResponseHeaders.ReadTrailers(Fields(("Grpc-Status", "0"))));

    [TestMethod]
    public void ReadTrailers_Responses_MalformedStatus_IsInternal() =>
        Assert.AreEqual(
            new GrpcStatus(GrpcStatusCode.Internal, "malformed grpc-status 'x'"),
            GrpcResponseHeaders.ReadTrailers(Fields(("grpc-status", "x"))));

    [TestMethod]
    public void ReadTrailers_Responses_NoStatus_FailsWithAWireError() =>
        Assert.ThrowsExactly<GrpcWireException>(() => GrpcResponseHeaders.ReadTrailers(Fields(("grpc-message", "x"))));

    private static KeyValuePair<string, string>[] Fields(params (string Name, string Value)[] fields) =>
        [.. fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))];
}
