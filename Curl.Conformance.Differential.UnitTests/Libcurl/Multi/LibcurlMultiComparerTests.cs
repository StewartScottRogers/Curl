using Curl.Conformance.Differential.Libcurl.Multi;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Multi;

[TestClass]
public sealed class LibcurlMultiComparerTests
{
    [TestMethod]
    public void Compare_SameLines_HasNoDifferences() =>
        Assert.IsEmpty(LibcurlMultiComparer.Compare("m2", ["0 add=0"], ["0 add=0"]).Differences);

    [TestMethod]
    public void Compare_DifferentAndMissingLines_WritesRealAndOursPairs()
    {
        var outcome = LibcurlMultiComparer.Compare("m_in", ["0 a", "1 b"], ["0 c"]);
        Assert.AreEqual("m_in", outcome.Name);
        CollectionAssert.AreEqual(new[] { "real: 0 a", "ours: 0 c", "real: 1 b", "ours: (none)" }, outcome.Differences.ToArray());
        CollectionAssert.AreEqual(new[] { "real: (none)", "ours: 1 b" }, LibcurlMultiComparer.Compare("x", [], ["1 b"]).Differences.ToArray());
    }

    [TestMethod]
    public void Compare_NullLines_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMultiComparer.Compare("x", null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMultiComparer.Compare("x", [], null!));
    }
}
