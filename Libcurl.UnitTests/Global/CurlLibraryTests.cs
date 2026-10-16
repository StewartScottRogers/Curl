using Libcurl.Global;

namespace Libcurl.UnitTests.Global;

[TestClass]
public sealed class CurlLibraryTests
{
    [TestMethod]
    public async Task CreatingMethods_EachDelegateToTheEngineAndReturnItsHandle()
    {
        var engine = new FakeCurlEngine();
        await using var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());

        object[] handles =
        [
            library.CreateEasyTransfer(),
            library.CreateMultiTransfer(),
            library.CreateShareHandle(),
            library.CreateUrlHandle(),
            library.CreateMimeBuilder(),
            library.CreateFormBuilder(),
        ];

        CollectionAssert.AreEqual(engine.Created, handles);
    }

    [TestMethod]
    public async Task CreatingMethods_AfterCleanup_ThrowObjectDisposed()
    {
        var engine = new FakeCurlEngine();
        var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());
        await library.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => library.CreateEasyTransfer());
        Assert.ThrowsExactly<ObjectDisposedException>(() => library.CreateMultiTransfer());
        Assert.ThrowsExactly<ObjectDisposedException>(() => library.CreateShareHandle());
        Assert.ThrowsExactly<ObjectDisposedException>(() => library.CreateUrlHandle());
        Assert.ThrowsExactly<ObjectDisposedException>(() => library.CreateMimeBuilder());
        Assert.ThrowsExactly<ObjectDisposedException>(() => library.CreateFormBuilder());
        Assert.IsEmpty(engine.Created);
    }
}
