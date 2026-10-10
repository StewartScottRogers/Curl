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
    public void Rewrite_Line_ComposesOnlyWhatRuntestsWorkingDirectoryNames(string written, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("written", written);

        string rewritten = Encoding.Latin1.GetString(UpstreamTestDirectoryComposition.Rewrite(Encoding.Latin1.GetBytes(written)));

        diagnostics.Assert("rewritten", expected, rewritten);
        Assert.AreEqual(expected, rewritten);
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
