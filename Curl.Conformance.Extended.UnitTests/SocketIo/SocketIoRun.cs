using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SocketIo;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.SocketIo;

/// <summary>Runs curl against a reference Engine.IO server and holds what the Socket.IO cases share.</summary>
internal static class SocketIoRun
{
    public const int Port = 5000;

    public const string Url = "socketio://localhost:5000/";

    public static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>A server that opens, takes curl's CONNECT to the main namespace, accepts it and then plays <paramref name="steps" />.</summary>
    public static SocketIoReferenceServer Connected(params SocketIoStep[] steps) =>
        new([SocketIoStep.Open(), SocketIoStep.Receive, SocketIoStep.Packet("""40{"sid":"edwuqZDxbKbA5RgbAAAD"}"""), .. steps]);

    public static Task<CurlRunResult> RunAsync(SocketIoReferenceServer server, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Tcp(Port, server)], ["-sS", "-m", "30", .. arguments]);

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>curl's standard error with each line ended by \n, as on every platform but Windows.</summary>
    public static string Error(CurlRunResult result) => Text(result.StandardError).ReplaceLineEndings("\n");
}
