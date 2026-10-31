using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Ssh.HostKeys;

/// <summary>
/// Pins the known-hosts file curl writes back for <c>CURLKHSTAT_FINE_ADD_TO_FILE</c> and
/// <c>CURLKHSTAT_FINE_REPLACE</c> to libssh2 1.11.1's <c>libssh2_knownhost_writefile</c>
/// (ADR-0626): every entry read, one line per name, then the URL's plain host name.
/// </summary>
public sealed partial class KnownHostsFileTests
{
    private const string KnownHostsPath = "known_hosts";

    [TestMethod]
    public void ToText_PlainNamesHashedNamesAndComments_AreWrittenOneEntryPerLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string text =
            "a,b ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIGkey  my comment\n" +
            $"|1|{Salt}|{HashOfPlainHost} ecdsa-sha2-nistp256 {HostKeyBase64}\n" +
            "c 1024 35 1234567890123456789012345 rsa1\n" +
            "# skipped\n" +
            $"d ssh-dss {OtherKeyBase64}\n";
        diagnostics.Arrange("file", text);

        string written = KnownHostsFile.Parse(text).ToText("\n");

        string expected =
            "b ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIGkey my comment\n" +
            "a ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIGkey my comment\n" +
            $"|1|{Salt}|{HashOfPlainHost} ecdsa-sha2-nistp256 {HostKeyBase64}\n" +
            "c 1024 35 1234567890123456789012345 rsa1\n" +
            $"d ssh-dss {OtherKeyBase64}\n";
        diagnostics.Act("written", written);
        diagnostics.Assert("written", expected, written);
        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public void WithHostKey_NoEntryReplaced_AddsThePlainHostNameLast()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        KnownHostsFile file = KnownHostsFile.Parse($"other ssh-ed25519 {OtherKeyBase64}\n");
        diagnostics.Arrange("host", Host);

        KnownHostsFile added = file.WithHostKey(Host, HostKey, replaced: null);

        string expected = $"other ssh-ed25519 {OtherKeyBase64}\n{Host} ecdsa-sha2-nistp256 {HostKeyBase64}\n";
        diagnostics.Act("written", added.ToText("\n"));
        diagnostics.Assert("written", expected, added.ToText("\n"));
        Assert.AreEqual(expected, added.ToText("\n"));
        Assert.AreEqual(KnownHostsCheck.Match, added.Check(Host, 22, HostKey));
    }

    [TestMethod]
    public void WithHostKey_EntryReplaced_DeletesThatEntryAlone()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        KnownHostsFile file = KnownHostsFile.Parse($"[{Host}]:{Port},{Host} ecdsa-sha2-nistp256 {OtherKeyBase64}\n");
        KnownHostsEntry replaced = file.Lookup(Host, Port, HostKey).Entry!;
        diagnostics.Arrange("replaced", replaced.PlainName);

        string written = file.WithHostKey(Host, HostKey, replaced).ToText("\n");

        string expected = $"{Host} ecdsa-sha2-nistp256 {OtherKeyBase64}\n{Host} ecdsa-sha2-nistp256 {HostKeyBase64}\n";
        diagnostics.Act("written", written);
        diagnostics.Assert("written", expected, written);
        Assert.AreEqual($"[{Host}]:{Port}", replaced.PlainName);
        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public async Task WriteAsync_Opened_TruncatesWithThePlatformsLineEndingAndNoByteOrderMark()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WritableInMemoryFileSystem fileSystem = new(new Dictionary<string, string>());
        TranscriptTransferEvents events = new();
        KnownHostsFile file = KnownHostsFile.Parse(string.Empty).WithHostKey(Host, HostKey, replaced: null);
        string expected = $"{Host} ecdsa-sha2-nistp256 {HostKeyBase64}{Environment.NewLine}";

        await file.WriteAsync(fileSystem, KnownHostsPath, events, CancellationToken.None);

        string written = Encoding.UTF8.GetString(fileSystem.Written[KnownHostsPath]);
        diagnostics.Act("written", written);
        diagnostics.Assert("written", expected, written);
        Assert.AreEqual(expected, written);
        Assert.AreEqual((byte)'1', fileSystem.Written[KnownHostsPath][0], "no byte order mark");
        Assert.AreEqual((KnownHostsPath, FileWriteMode.Truncate, (UnixFileMode)0b110_110_110), fileSystem.WriteOpens.Single());
        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    [DataRow(true, false, DisplayName = "the open is refused")]
    [DataRow(false, true, DisplayName = "the write fails")]
    public async Task WriteAsync_Fails_ReportsLibcurlsWarning(bool refusesWrites, bool failsWrites)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WritableInMemoryFileSystem fileSystem = new(new Dictionary<string, string>()) { RefusesWrites = refusesWrites, FailsWrites = failsWrites };
        TranscriptTransferEvents events = new();
        diagnostics.Arrange("refuses", refusesWrites);
        diagnostics.Arrange("fails", failsWrites);
        KnownHostsFile file = KnownHostsFile.Parse(string.Empty).WithHostKey(Host, HostKey, replaced: null);

        await file.WriteAsync(fileSystem, KnownHostsPath, events, CancellationToken.None);

        diagnostics.ActLines(events.Transcript);
        diagnostics.Assert("line", "* WARNING: writing known_hosts failed", events.Transcript.Single());
        Assert.AreEqual("* WARNING: writing known_hosts failed", events.Transcript.Single());
    }
}
