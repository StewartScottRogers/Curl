using System.Text;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>Every non-OK status code of statuscodes.md, ended in a Trailers-Only reply and reported by its name.</summary>
[TestClass]
public sealed class GrpcStatusCases
{
    private const int Port = 50053;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Grpc:StatusCodes-Names-1")]
    [DataRow(1, "CANCELLED")]
    [DataRow(2, "UNKNOWN")]
    [DataRow(3, "INVALID_ARGUMENT")]
    [DataRow(4, "DEADLINE_EXCEEDED")]
    [DataRow(5, "NOT_FOUND")]
    [DataRow(6, "ALREADY_EXISTS")]
    [DataRow(7, "PERMISSION_DENIED")]
    [DataRow(8, "RESOURCE_EXHAUSTED")]
    [DataRow(9, "FAILED_PRECONDITION")]
    [DataRow(10, "ABORTED")]
    [DataRow(11, "OUT_OF_RANGE")]
    [DataRow(12, "UNIMPLEMENTED")]
    [DataRow(13, "INTERNAL")]
    [DataRow(14, "UNAVAILABLE")]
    [DataRow(15, "DATA_LOSS")]
    [DataRow(16, "UNAUTHENTICATED")]
    public Task NonOkStatus_InTrailersOnly_ExitsTwentyTwoNamingTheCode(int code, string name) =>
        Known.RunCaseAsync(
            ["Grpc:StatusCodes-Names-1"],
            async () =>
            {
                (CurlRunResult result, GrpcReferenceCall call) = await GrpcResponseCases.CallAsync(GrpcUnaryCases.MethodPath, GrpcReferenceReply.TrailersOnly(code, "why"), Port);

                Assert.AreEqual(GrpcUnaryCases.MethodPath, call.Path, "Grpc:StatusCodes-Names-1");
                Assert.AreEqual(22, result.ExitCode, "Grpc:StatusCodes-Names-1");
                Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput), "Grpc:StatusCodes-Names-1");
                Assert.AreEqual($"curl: (22) grpc-status {code} ({name}): why{Environment.NewLine}", Encoding.UTF8.GetString(result.StandardError), "Grpc:StatusCodes-Names-1");
            });
}
