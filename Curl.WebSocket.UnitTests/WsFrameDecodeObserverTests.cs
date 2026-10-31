using Curl.Testing;

namespace Curl.WebSocket;

/// <summary>Pins what <see cref="WsFrameDecoder" /> tells its <see cref="IWsFrameDecodeObserver" />.</summary>
[TestClass]
public sealed class WsFrameDecodeObserverTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Decode_HeadEndingARead_ThenItsPayloadAndAClose_TellsTheObserverEachInOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var observer = new RecordingObserver();
        var decoder = new WsFrameDecoder(observer);
        diagnostics.Arrange("reads", "81 02 | 68 69 88 02 03 e8");

        decoder.Decode([0x81, 0x02]);
        decoder.Decode([0x68, 0x69, 0x88, 0x02, 0x03, 0xe8]);
        diagnostics.Act("told", string.Join(" | ", observer.Told));

        string[] expected =
        [
            "opened Text FIN 2",
            "head ended read Text FIN 2",
            "passed Text FIN 2 at 2 of 2",
            "opened Close FIN 2",
            "passed Close FIN 2 at 2 of 2",
            "close 03e8",
        ];
        diagnostics.Assert("told", string.Join(" | ", expected), string.Join(" | ", observer.Told));
        CollectionAssert.AreEqual(expected, observer.Told);
    }

    private sealed class RecordingObserver : IWsFrameDecodeObserver
    {
        public List<string> Told { get; } = [];

        public void FrameOpened(WsOpcode opcode, bool isFinal, long payloadLength) =>
            Told.Add($"opened {opcode} {Fin(isFinal)} {payloadLength}");

        public void HeadEndedRead(WsOpcode opcode, bool isFinal, long payloadLength) =>
            Told.Add($"head ended read {opcode} {Fin(isFinal)} {payloadLength}");

        public void PayloadPassed(WsOpcode opcode, bool isFinal, long passed, long payloadOffset, long payloadLength) =>
            Told.Add($"passed {opcode} {Fin(isFinal)} {passed} at {payloadOffset} of {payloadLength}");

        public void CloseReceived(ReadOnlySpan<byte> payload) =>
            Told.Add("close " + Convert.ToHexStringLower(payload));

        private static string Fin(bool isFinal) => isFinal ? "FIN" : "no FIN";
    }
}
