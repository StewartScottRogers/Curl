using System.Text;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Proto;

[TestClass]
public sealed class ProtoCasesTests
{
    private const string ResourcePrefix = "Curl.Conformance.Differential.Cases.";
    private const string ProtoFolder = "CliProtocols.Proto.";

    [TestMethod]
    public void ReadEmbedded_ProtoCaseFiles_HoldOneCaseFileForEachCaseTheTestersCasesProtoPyProduces()
    {
        var casePaths = ProtoCasePaths();

        Assert.AreEqual(127, casePaths.Length);
        Assert.AreEqual(63, casePaths.Count(path => path.StartsWith(ProtoFolder + "Ftp.", StringComparison.Ordinal)));
        Assert.AreEqual(21, casePaths.Count(path => path.StartsWith(ProtoFolder + "Smtp.", StringComparison.Ordinal)));
        Assert.AreEqual(19, casePaths.Count(path => path.StartsWith(ProtoFolder + "Pop3.", StringComparison.Ordinal)));
        Assert.AreEqual(24, casePaths.Count(path => path.StartsWith(ProtoFolder + "Imap.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ReadEmbedded_EachProtoCaseFile_ParsesToOneCaseNamedAfterItsFile()
    {
        foreach (var casePath in ProtoCasePaths())
        {
            var differentialCase = DifferentialCaseFileReader.ReadEmbedded(casePath).Single();

            Assert.AreEqual(casePath.Split('.')[^2], differentialCase.Name, casePath);
            Assert.AreNotEqual(0, differentialCase.Arguments.Count, casePath);
            Assert.AreEqual(TimeSpan.FromSeconds(20), differentialCase.Timeout, casePath);
            Assert.IsTrue(differentialCase.ComparesStandardError, casePath);
            Assert.IsTrue(differentialCase.ComparesWire, casePath);
        }
    }

    [TestMethod]
    public void ReadEmbedded_ImapStoreCase_KeepsTheBackslashInsideItsQuotedCommand()
    {
        var differentialCase = DifferentialCaseFileReader.ReadEmbedded(ProtoFolder + "Imap.imap-store.json").Single();

        CollectionAssert.AreEqual(
            new[] { "-s", "imap://127.0.0.1:18230/INBOX", "-X", "STORE 1 +Flags \\Deleted" },
            differentialCase.Arguments.ToArray());
        Assert.AreEqual("Subject: hi\r\n\r\nhello\r\n", Encoding.UTF8.GetString(differentialCase.Files["mail.txt"]));
    }

    [TestMethod]
    public void ReadEmbedded_FtpUploadStdinCase_ReadsItsStandardInputAndFile()
    {
        var differentialCase = DifferentialCaseFileReader.ReadEmbedded(ProtoFolder + "Ftp.ftp-upload-stdin.json").Single();

        CollectionAssert.AreEqual(new[] { "-s", "-T", "-", "ftp://127.0.0.1:18200/up.txt" }, differentialCase.Arguments.ToArray());
        Assert.AreEqual("stdin up\n", Encoding.UTF8.GetString(differentialCase.StandardInput!));
        Assert.AreEqual("uploaded text\nline2\n", Encoding.UTF8.GetString(differentialCase.Files["up.txt"]));
    }

    private static string[] ProtoCasePaths() =>
        [.. typeof(DifferentialCaseFileReader).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix + ProtoFolder, StringComparison.Ordinal))
            .Select(name => name[ResourcePrefix.Length..])
            .Order(StringComparer.Ordinal)];
}
