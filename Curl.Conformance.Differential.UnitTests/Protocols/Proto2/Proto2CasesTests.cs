using System.Text;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Proto2;

[TestClass]
public sealed class Proto2CasesTests
{
    private const string CaseFolder = "CliProtocols/Proto2/";

    private const string ResourcePrefix = "Curl.Conformance.Differential.Cases.CliProtocols.Proto2.";

    [TestMethod]
    public void ReadEmbedded_Proto2CaseFiles_HoldOneFilePerCaseTheTestersCasesProto2PyProduces()
    {
        Assert.HasCount(52, Proto2CaseFileNames());
    }

    [TestMethod]
    public void ReadEmbedded_EveryProto2CaseFile_ParsesToOneCaseNamedAfterTheFile()
    {
        foreach (var fileName in Proto2CaseFileNames())
        {
            var cases = DifferentialCaseFileReader.ReadEmbedded(CaseFolder + fileName);

            Assert.HasCount(1, cases, fileName);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(fileName), cases[0].Name);
        }
    }

    [TestMethod]
    public void ReadEmbedded_TelnetUploadFileCase_ReadsTheTestersArgumentsStdinFileAndTimeout()
    {
        var telnetCase = DifferentialCaseFileReader.ReadEmbedded(CaseFolder + "telnet-upload-file.json")[0];

        CollectionAssert.AreEqual(new[] { "-s", "-T", "up.txt", "telnet://127.0.0.1:18420" }, telnetCase.Arguments.ToArray());
        Assert.AreEqual("user\r\n", Encoding.UTF8.GetString(telnetCase.StandardInput!));
        Assert.AreEqual("abc\n", Encoding.UTF8.GetString(telnetCase.Files["up.txt"]));
        Assert.AreEqual(TimeSpan.FromSeconds(10), telnetCase.Timeout);
    }

    [TestMethod]
    public void ReadEmbedded_RtspSetupCase_KeepsTheQuotedTransportHeaderAsOneArgument()
    {
        var rtspCase = DifferentialCaseFileReader.ReadEmbedded(CaseFolder + "rtsp-setup.json")[0];

        CollectionAssert.AreEqual(
            new[] { "-s", "-X", "SETUP", "-H", "Transport: RTP/AVP;unicast;client_port=1-2", "rtsp://127.0.0.1:18430/x/track1" },
            rtspCase.Arguments.ToArray());
        Assert.AreEqual(TimeSpan.FromSeconds(20), rtspCase.Timeout);
    }

    private static string[] Proto2CaseFileNames() =>
        [.. typeof(DifferentialCaseFileReader).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(name => name[ResourcePrefix.Length..])
            .Order(StringComparer.Ordinal)];
}
