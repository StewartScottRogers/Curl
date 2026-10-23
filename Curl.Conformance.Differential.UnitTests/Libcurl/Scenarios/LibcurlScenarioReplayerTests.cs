using System.Text;
using Curl.Conformance.Differential.Libcurl.Scenarios;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Scenarios;

[TestClass]
public sealed class LibcurlScenarioReplayerTests
{
    [TestMethod]
    public void Replay_Url_SetsThenGetsEveryPartWithEveryFlagSet()
    {
        var target = new RecordingTarget();

        var line = LibcurlScenarioReplayer.Replay(new LibcurlScenario(3, "U", "http://a/", 0, 4, null, []), target).Single();

        Assert.StartsWith("3 U set=0 |0/0=0:687474703A2F2F612F |0/1=0:687474703A2F2F612F", line);
        Assert.EndsWith(" |10/16386=0:687474703A2F2F612F", line);
        Assert.AreEqual(1 + (11 * LibcurlScenarioReplayer.GetFlags.Count), target.Calls.Count);
        Assert.AreEqual("set 0 http://a/ 4", target.Calls[0]);
        Assert.IsTrue(target.Disposed);
    }

    [TestMethod]
    public void Replay_PartOnBaseUrl_SetsBaseThenPartAndGetsUrlAndPart()
    {
        var target = new RecordingTarget { GetCode = 9 };

        var line = LibcurlScenarioReplayer.Replay(new LibcurlScenario(4, "P", "http://a/", 5, 128, null, []), target).Single();

        Assert.AreEqual("4 P set=0 url=9:null part=9:null", line);
        CollectionAssert.AreEqual(new[] { "set 0 http://a/ 0", "set 5 (null) 128", "get 0 0", "get 5 0" }, target.Calls);
    }

    [TestMethod]
    public void Replay_PartWithoutBaseUrl_SetsOnlyThePart()
    {
        var target = new RecordingTarget();

        LibcurlScenarioReplayer.Replay(new LibcurlScenario(0, "P", "", 7, 0, "/x", []), target);

        Assert.AreEqual("set 7 /x 0", target.Calls[0]);
    }

    [TestMethod]
    public void Replay_PartWithNullBaseUrl_SetsOnlyThePart()
    {
        var target = new RecordingTarget();

        LibcurlScenarioReplayer.Replay(new LibcurlScenario(0, "P", null, 8, 0, "q", []), target);

        Assert.AreEqual("set 8 q 0", target.Calls[0]);
    }

    [TestMethod]
    public void Replay_UnknownKind_WritesExc() =>
        Assert.AreEqual("6 X EXC KeyNotFoundException", LibcurlScenarioReplayer.Replay(new LibcurlScenario(6, "X", null, 0, 0, null, []), new RecordingTarget()).Single());

    [TestMethod]
    public void Replay_EscapeUnescapeDateVersion_WriteOneLineEach()
    {
        var target = new RecordingTarget();

        Assert.AreEqual("1 E esc:2", LibcurlScenarioReplayer.Replay(new LibcurlScenario(1, "E", null, 0, 0, null, [1, 2]), target).Single());
        Assert.AreEqual("2 N 0102", LibcurlScenarioReplayer.Replay(new LibcurlScenario(2, "N", null, 0, 0, null, [1, 2]), target).Single());
        Assert.AreEqual("3 D 10", LibcurlScenarioReplayer.Replay(new LibcurlScenario(3, "D", "now", 0, 0, null, []), target).Single());
        Assert.AreEqual("4 V libcurl/8.22.0", LibcurlScenarioReplayer.Replay(new LibcurlScenario(4, "V", null, 0, 0, null, []), target).Single());
    }

    [TestMethod]
    public void Replay_UnescapeReturningNull_WritesExc()
    {
        var target = new RecordingTarget { UnescapeReturnsNull = true };

        Assert.AreEqual("2 N EXC", LibcurlScenarioReplayer.Replay(new LibcurlScenario(2, "N", null, 0, 0, null, []), target).Single());
    }

    [TestMethod]
    public void Replay_ErrorTexts_ListsEveryCodeOfEveryKind()
    {
        var lines = LibcurlScenarioReplayer.Replay(new LibcurlScenario(1, "S", null, 0, 0, null, []), new RecordingTarget());

        Assert.HasCount(133 + 24 + 14 + 42, lines);
        Assert.AreEqual("1 easy -2 easy-2", lines[0]);
        Assert.AreEqual("1 multi -3 multi-3", lines[133]);
        Assert.AreEqual("1 share -1 share-1", lines[157]);
        Assert.AreEqual("1 url 40 url40", lines[^1]);
    }

    [TestMethod]
    public void Replay_TargetThrows_WritesExcWithTheExceptionType()
    {
        var target = new RecordingTarget { Throws = true };

        Assert.AreEqual("5 D EXC InvalidOperationException", LibcurlScenarioReplayer.Replay(new LibcurlScenario(5, "D", "x", 0, 0, null, []), target).Single());
    }

    private sealed class RecordingTarget : ILibcurlScenarioTarget, ILibcurlScenarioUrl
    {
        public List<string> Calls { get; } = [];

        public int GetCode { get; init; }

        public bool UnescapeReturnsNull { get; init; }

        public bool Throws { get; init; }

        public bool Disposed { get; private set; }

        public string Version() => "libcurl/8.22.0";

        public string EasyErrorText(int code) => $"easy{code}";

        public string MultiErrorText(int code) => $"multi{code}";

        public string ShareErrorText(int code) => $"share{code}";

        public string UrlErrorText(int code) => $"url{code}";

        public ILibcurlScenarioUrl CreateUrl() => this;

        public string Escape(byte[] bytes) => $"esc:{bytes.Length}";

        public byte[]? Unescape(byte[] bytes) => UnescapeReturnsNull ? null : bytes;

        public long ParseDate(string text) => Throws ? throw new InvalidOperationException() : 10;

        public int Set(int part, string? value, int flags)
        {
            Calls.Add($"set {part} {value ?? "(null)"} {flags}");
            return 0;
        }

        public int Get(int part, int flags, out byte[]? value)
        {
            Calls.Add($"get {part} {flags}");
            value = GetCode == 0 ? Encoding.ASCII.GetBytes("http://a/") : null;
            return GetCode;
        }

        public void Dispose() => Disposed = true;
    }
}
