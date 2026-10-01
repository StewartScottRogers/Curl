using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;

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

    private readonly MemoryStream _standardOutput = new();

    private readonly MemoryStream _standardError = new();

    [TestMethod]
    public async Task RunAsync_LaterGroupWithTheSameSettings_ReusesTheEarlierGroupsConnection()
    {
        ScriptedConnector connector = Serving(2);

        int exitCode = await RunAsync(connector, "-v", "-w", WriteOut, "http://h:18754/a", "--next", "-v", "-w", WriteOut, "http://h:18754/b");

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

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ok[1]ok[1]", Encoding.Latin1.GetString(_standardOutput.ToArray()));
        Assert.HasCount(2, connector.Targets);
    }

    private static ScriptedConnector Serving(int responses) =>
        new(Enumerable.Repeat(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"), responses));

    private Task<int> RunAsync(ScriptedConnector connector, params string[] arguments) =>
        CurlComposition
            .CreateRunner(_standardOutput, _standardError, new MemoryStream(), connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), runConnections: new ConnectionCache(TimeProvider.System))
            .RunAsync(arguments);
}
