using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a later <c>-:</c>/<c>--next</c> option group reuses a connection an earlier group
/// left in the run's <see cref="ConnectionCache" /> when their settings match, and opens its own
/// when a TLS setting differs, as curl 8.21.0 does (measured, BL-754 Notes; ADR-0285).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerOptionGroupConnectionReuseTests
{
    private const string WriteOut = "[%{num_connects}]";

    private const string ConnIdWriteOut = "[%{num_connects} %{conn_id}]";

    private readonly MemoryStream _standardOutput = new();

    private readonly MemoryStream _standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardOutputText => Encoding.Latin1.GetString(_standardOutput.ToArray());

    private string StandardErrorText => Encoding.UTF8.GetString(_standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_LaterGroupWithTheSameSettings_ReusesTheEarlierGroupsConnection()
    {
        ScriptedConnector connector = Serving(2);

        int exitCode = await RunAsync(connector, "-v", "-w", WriteOut, "http://h:18754/a", "--next", "-v", "-w", WriteOut, "http://h:18754/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "ok[1]ok[0]", StandardOutputText);
        Diagnostics.Assert("connections opened", 1, connector.Targets.Count);
        Diagnostics.Assert("stderr contains reuse line", true, StandardErrorText.Contains("* Reusing existing http: connection with host h", StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ok[1]ok[0]", Encoding.Latin1.GetString(_standardOutput.ToArray()));
        Assert.HasCount(1, connector.Targets);
        StringAssert.Contains(Encoding.UTF8.GetString(_standardError.ToArray()), "* Reusing existing http: connection with host h");
    }

    [TestMethod]
    public async Task RunAsync_LaterGroupWithADifferentTlsSetting_OpensAConnectionOfItsOwn()
    {
        ScriptedConnector connector = Serving(2);

        int exitCode = await RunAsync(connector, "-k", "-w", WriteOut, "https://h:18754/a", "--next", "-w", WriteOut, "https://h:18754/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "ok[1]ok[1]", StandardOutputText);
        Diagnostics.Assert("connections opened", 2, connector.Targets.Count);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ok[1]ok[1]", Encoding.Latin1.GetString(_standardOutput.ToArray()));
        Assert.HasCount(2, connector.Targets);
    }

    [TestMethod]
    public async Task RunAsync_TwoUrlsOnOneKeptAliveConnection_PrintTheReusedConnectionsNumberAsConnId()
    {
        // curl 8.21.0 printed [1 0][0 0] for two URLs on one kept-alive connection (BL-754 Notes, BL-1052).
        ScriptedConnector connector = Serving(2);

        int exitCode = await RunAsync(connector, "-s", "-w", ConnIdWriteOut, "http://h:18754/a", "http://h:18754/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "ok[1 0]ok[0 0]", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ok[1 0]ok[0 0]", Encoding.Latin1.GetString(_standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_LaterGroupReusingTheEarlierGroupsConnection_PrintsItsNumberAsConnId()
    {
        ScriptedConnector connector = Serving(2);

        int exitCode = await RunAsync(connector, "-s", "-w", ConnIdWriteOut, "http://h:18754/a", "--next", "-s", "-w", ConnIdWriteOut, "http://h:18754/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "ok[1 0]ok[0 0]", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ok[1 0]ok[0 0]", Encoding.Latin1.GetString(_standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ServerClosesBetweenTwoUrls_SecondTakesTheNextConnId()
    {
        byte[] closing = Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
        Diagnostics.Arrange("scripted responses", "two copies of: " + Lf(Encoding.Latin1.GetString(closing)));
        ScriptedConnector connector = new([closing, closing]);

        int exitCode = await RunAsync(connector, "-s", "-w", ConnIdWriteOut, "http://h:18754/a", "http://h:18754/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "ok[1 0]ok[1 1]", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ok[1 0]ok[1 1]", Encoding.Latin1.GetString(_standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_TraceIdsOnAReusedConnection_MarkTheSecondTransfersLinesWithTheReusedConnId()
    {
        ScriptedConnector connector = Serving(2);

        int exitCode = await RunAsync(connector, "-s", "-v", "--trace-ids", "http://h:18754/a", "http://h:18754/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string standardError = Encoding.UTF8.GetString(_standardError.ToArray());
        Diagnostics.Assert("stderr contains reuse line", true, standardError.Contains("[1-0] * Reusing existing http: connection with host h", StringComparison.Ordinal));
        Diagnostics.Assert("stderr contains [1-1]", false, standardError.Contains("[1-1]", StringComparison.Ordinal));
        StringAssert.Contains(standardError, "[1-0] * Reusing existing http: connection with host h");
        Assert.DoesNotContain("[1-1]", standardError);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static ScriptedConnector Serving(int responses) =>
        new(Enumerable.Repeat(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"), responses));

    private async Task<int> RunAsync(ScriptedConnector connector, params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await CurlComposition
                .CreateRunner(_standardOutput, _standardError, new MemoryStream(), connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), runConnections: new ConnectionCache(TimeProvider.System))
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("connections opened", connector.Targets.Count);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }
}
