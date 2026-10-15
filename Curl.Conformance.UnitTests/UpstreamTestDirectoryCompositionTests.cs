using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTestDirectoryComposition"/>: which directory compositions of a test
/// file are rewritten before expansion, and that every other byte is left as it was.
/// </summary>
[TestClass]
public sealed class UpstreamTestDirectoryCompositionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("%PERL %SRCDIR/libtest/test613.pl prepare %PWD/%LOGDIR/test%TESTNUMBER.dir", "%PERL ./libtest/test613.pl prepare %LOGDIR/test%TESTNUMBER.dir")]
    [DataRow("%PERL %SRCDIR/libtest/test610.pl mkdir %PWD/%LOGDIR/a", "%PERL ./libtest/test610.pl mkdir %LOGDIR/a")]
    [DataRow("--output-dir %PWD/not-there", "--output-dir %PWD/not-there")]
    [DataRow("%SRCDIR/data/x %SRCDIR/libtest/test1013.pl", "%SRCDIR/data/x %SRCDIR/libtest/test1013.pl")]
    [DataRow("%PWD%LOGDIR", "%PWD%LOGDIR")]
    [DataRow("file:%FILE_PWD/%LOGDIR/test%TESTNUMBER.txt", "file:%FILE_PWD%LOGDIR/test%TESTNUMBER.txt")]
    [DataRow("file://localhost/%FILE_PWD/%LOGDIR/x", "file://localhost/%FILE_PWD%LOGDIR/x")]
    [DataRow("file://%FILE_PWD/", "file://%FILE_PWD/")]
    public void Rewrite_Line_ComposesOnlyWhatRuntestsWorkingDirectoryNames(string written, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("written", written);

        string rewritten = Encoding.Latin1.GetString(UpstreamTestDirectoryComposition.Rewrite(Encoding.Latin1.GetBytes(written)));

        diagnostics.Assert("rewritten", expected, rewritten);
        Assert.AreEqual(expected, rewritten);
    }

    [TestMethod]
    [DataRow("<file name=\"log/upload5018\">\nlog/x\n</file>\n<command>\nhttp://h/ -T log/upload5018 --x \"log/y\"\n</command>\nlog/z\n", "<file name=\"%LOGDIR/upload5018\">\nlog/x\n</file>\n<command>\nhttp://h/ -T %LOGDIR/upload5018 --x \"%LOGDIR/y\"\n</command>\nlog/z\n")]
    [DataRow("<command>\n-T blog/x\n</command>\n<command>\n a log/b\n</command>", "<command>\n-T blog/x\n</command>\n<command>\n a %LOGDIR/b\n</command>")]
    [DataRow("<command>\n log/unterminated", "<command>\n log/unterminated")]
    public void Rewrite_LogFolderOfRuntests_BecomesTheCaseLogDirectoryInFileNamesAndCommandsOnly(string written, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        string rewritten = Encoding.Latin1.GetString(UpstreamTestDirectoryComposition.Rewrite(Encoding.Latin1.GetBytes(written)));

        diagnostics.Assert("rewritten", expected, rewritten);
        Assert.AreEqual(expected, rewritten);
    }

    [TestMethod]
    [DataRow("/tmp/log/test203-1", "", "file:/tmp/log/test203-1/test203.txt")]
    [DataRow("C:/work/log/test203-1", "/", "file:/C:/work/log/test203-1/test203.txt")]
    public void FilePwdBefore_LogDirectory_MakesTheSingleSlashFileUrlOfTest203NameTheFile(string logDirectory, string expected, string expectedUrl)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("logDirectory", logDirectory);

        string filePwd = UpstreamTestDirectoryComposition.FilePwdBefore(logDirectory);
        string url = $"file:{filePwd}{logDirectory}/test203.txt";

        diagnostics.Assert("filePwd", expected, filePwd);
        Assert.AreEqual(expected, filePwd);
        Assert.AreEqual(expectedUrl, url);
    }

    [TestMethod]
    public void Rewrite_EveryByteValue_IsKept()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] everyByte = [.. Enumerable.Range(0, 256).Select(value => (byte)value)];

        byte[] rewritten = UpstreamTestDirectoryComposition.Rewrite(everyByte);

        diagnostics.Assert("length", 256, rewritten.Length);
        CollectionAssert.AreEqual(everyByte, rewritten);
    }
}
