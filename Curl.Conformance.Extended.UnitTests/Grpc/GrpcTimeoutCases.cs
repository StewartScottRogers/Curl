using System.Text.RegularExpressions;
using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>
/// The request's <c>grpc-timeout</c> (PROTOCOL-HTTP2.md "Requests": Timeout is a TimeoutValue of at most 8 ASCII digits
/// and a TimeoutUnit) as curl derives it from <c>-m</c>, and its absence without <c>-m</c>.
/// </summary>
[TestClass]
public sealed partial class GrpcTimeoutCases
{
    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Timeout-1")]
    [DataRow("10", "10000000u", DisplayName = "-m 10 is 10000000 microseconds, the finest unit within 8 digits")]
    [DataRow("0.5", "500000u", DisplayName = "-m 0.5 is 500000 microseconds, 500000000 nanoseconds being 9 digits")]
    [DataRow("0.001", "1000000n", DisplayName = "-m 0.001 is 1000000 nanoseconds, which fits 8 digits")]
    [DataRow("2000000", "2000000S", DisplayName = "-m 2000000 is 2000000 seconds, 2000000000 milliseconds being 10 digits")]
    public Task Timeout_MaxTime_SendsGrpcTimeoutInTheFinestUnitWithinEightDigits(string maxTime, string expected) =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Timeout-1"],
            async () =>
            {
                GrpcReferenceCall call = await RunUnaryAsync(["-m", maxTime]);

                string? timeout = call.Header("grpc-timeout");
                Assert.IsNotNull(timeout, "Grpc:Http2-Timeout-1");
                Assert.IsTrue(TimeoutSyntax().IsMatch(timeout), $"Grpc:Http2-Timeout-1: {timeout}");
                Assert.AreEqual(expected, timeout, "Grpc:Http2-Timeout-1");
            });

    [TestMethod]
    [SpecRequirement("Grpc:Http2-Timeout-2")]
    public Task Timeout_NoMaxTime_SendsNoGrpcTimeout() =>
        Known.RunCaseAsync(
            ["Grpc:Http2-Timeout-2"],
            async () =>
            {
                GrpcReferenceCall call = await RunUnaryAsync([]);

                Assert.IsNull(call.Header("grpc-timeout"), "Grpc:Http2-Timeout-2");
            });

    // PROTOCOL-HTTP2.md: TimeoutValue is a positive integer of at most 8 ASCII digits; TimeoutUnit is H, M, S, m, u or n.
    [GeneratedRegex("^[0-9]{1,8}[HMSmun]$")]
    private static partial Regex TimeoutSyntax();

    private static async Task<GrpcReferenceCall> RunUnaryAsync(IReadOnlyList<string> timeoutArguments)
    {
        string directory = Directory.CreateTempSubdirectory("grpc-timeout-").FullName;
        try
        {
            string proto = Path.Combine(directory, "greeter.proto");
            await File.WriteAllTextAsync(proto, GrpcUnaryCases.Proto);
            GrpcReferenceServer server = new(new GrpcScriptedService(GrpcUnaryCases.MethodPath, GrpcReferenceReply.Ok([0x0A, 0x04, .. "hi x"u8])));
            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                CurlRunner,
                [ReferenceRoute.Tcp(GrpcUnaryCases.Port, server)],
                ["-sS", .. timeoutArguments, "--grpc-proto", proto, "-d", """{"name":"x"}""", $"grpc://localhost:{GrpcUnaryCases.Port}{GrpcUnaryCases.MethodPath}"],
                timeProvider: new FrozenClock());
            Assert.AreEqual(0, result.ExitCode, "the call succeeds");
            return server.Calls.Single();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // A clock that never moves and whose timers never fire, so even -m 0.001 lets the call finish: the case reads only the
    // grpc-timeout curl derives from -m, never the deadline expiring.
    private sealed class FrozenClock : TimeProvider
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public override long GetTimestamp() => 0;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NeverFiringTimer();

        private sealed class NeverFiringTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
