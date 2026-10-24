using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Text;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Text;

[TestClass]
public sealed class TelnetResponderTests
{
    private static readonly byte[] Greeting = [0xff, 0xfd, 0x18, 0xff, 0xfb, 0x01, .. "login: "u8];

    [TestMethod]
    public async Task RespondAsync_ClientAnswers_SendsOptionsLogsTheReprAndWelcomes()
    {
        var log = new ProtocolWireLog();
        var time = new ImmediateTimeProvider();
        using var connection = new DuplexMemoryStream([0xff, 0xfc, 0x18, .. "bob\r\n"u8]);

        await new TelnetResponder(log, time).RespondAsync(connection, CancellationToken.None);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. "welcome\r\n"u8], connection.Written);
        Assert.AreEqual("b'\\xff\\xfc\\x18bob\\r\\n'", Encoding.UTF8.GetString(log.Take()));
        Assert.AreEqual(TimeSpan.FromMilliseconds(300), time.LastDelay);
    }

    [TestMethod]
    public async Task RespondAsync_ClientSendsNothing_LogsAnEmptyReprAndStillWelcomes()
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream([]);

        await new TelnetResponder(log, new ImmediateTimeProvider()).RespondAsync(connection, CancellationToken.None);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. "welcome\r\n"u8], connection.Written);
        Assert.AreEqual("b''", Encoding.UTF8.GetString(log.Take()));
    }

    private sealed class ImmediateTimeProvider : TimeProvider
    {
        public TimeSpan LastDelay { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            LastDelay = dueTime;
            callback(state);
            return new NoTimer();
        }
    }

    private sealed class NoTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
