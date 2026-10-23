using System.Text;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Core;

[TestClass]
public sealed class DifferentialControlResponderTests
{
    [TestMethod]
    public async Task RespondAsync_AnyOtherPath_AnswersTheJoinedLogAndClearsItButKeepsCounters()
    {
        var state = new DifferentialResponderState();
        state.LogRequest("one"u8.ToArray());
        state.LogRequest("two"u8.ToArray());
        state.IncrementCounter("retry");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 11\r\n\r\none\0SEP\0two", await RespondAsync(state, "GET /log HTTP/1.1\r\nHost: x\r\n\r\n"));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", await RespondAsync(state, "GET /log HTTP/1.1\r\n"));
        Assert.AreEqual(2, state.IncrementCounter("retry"));
    }

    [TestMethod]
    public async Task RespondAsync_Reset_ClearsLogAndCountersAndAnswersOk()
    {
        var state = new DifferentialResponderState();
        state.LogRequest("one"u8.ToArray());
        state.IncrementCounter("retry");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", await RespondAsync(state, "GET /reset HTTP/1.1\r\n\r\n"));
        Assert.AreEqual(0, state.TakeRequestLog().Length);
        Assert.AreEqual(1, state.IncrementCounter("retry"));
    }

    [TestMethod]
    public async Task RespondAsync_RequestLineWithoutTarget_AnswersNothing()
    {
        Assert.AreEqual(string.Empty, await RespondAsync(new DifferentialResponderState(), "GET\r\n\r\n"));
    }

    private static async Task<string> RespondAsync(DifferentialResponderState state, string request)
    {
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(request));
        await new DifferentialControlResponder(state).RespondAsync(connection, CancellationToken.None);
        return Encoding.Latin1.GetString(connection.Written);
    }
}
