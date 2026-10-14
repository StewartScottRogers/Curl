namespace Curl.Conformance;

[TestClass]
public sealed class UpstreamCaseDebugSettingsTests
{
    private static Func<string, string?> Environment(string? caseNumber, string? keepLogs) =>
        name => name switch
        {
            UpstreamCaseDebugSettings.CaseVariableName => caseNumber,
            UpstreamCaseDebugSettings.KeepLogsVariableName => keepLogs,
            _ => null,
        };

    [TestMethod]
    public void Read_NothingSet_NamesNoCaseAndDeletesLogs()
    {
        UpstreamCaseDebugSettings settings = UpstreamCaseDebugSettings.Read(Environment(null, null));

        Assert.AreEqual(new UpstreamCaseDebugSettings(null, false), settings);
    }

    [TestMethod]
    public void Read_CaseNumberAndKeepLogsSet_NamesTheCaseAndKeepsLogs()
    {
        UpstreamCaseDebugSettings settings = UpstreamCaseDebugSettings.Read(Environment(" 798 ", "1"));

        Assert.AreEqual(new UpstreamCaseDebugSettings(798, true), settings);
    }

    [TestMethod]
    [DataRow("test798")]
    [DataRow("-798")]
    [DataRow("")]
    public void Read_CaseNotANumber_NamesNoCase(string value)
    {
        UpstreamCaseDebugSettings settings = UpstreamCaseDebugSettings.Read(Environment(value, null));

        Assert.IsNull(settings.CaseNumber);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("true")]
    [DataRow("")]
    public void Read_KeepLogsNotOne_DeletesLogs(string value)
    {
        UpstreamCaseDebugSettings settings = UpstreamCaseDebugSettings.Read(Environment(null, value));

        Assert.IsFalse(settings.KeepsLogs);
    }

    [TestMethod]
    public void Read_NullReader_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseDebugSettings.Read(null!));
    }

    [TestMethod]
    public void KeptLogFolderLine_Path_NamesTheFolder()
    {
        string line = UpstreamCaseDebugSettings.KeptLogFolderLine("/tmp/log/test798-1");

        Assert.AreEqual("Kept the case's log folder: /tmp/log/test798-1", line);
    }
}
