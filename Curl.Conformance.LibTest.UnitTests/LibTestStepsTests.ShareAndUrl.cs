using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Global;
using Libcurl.Share;
using Libcurl.Url;

namespace Curl.Conformance.LibTest.UnitTests;

public sealed partial class LibTestStepsTests
{
    [TestMethod]
    public async Task ShareInit_OnALiveLibrary_GoesOnWithAHandle()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.ShareInit(library, out CurlShare? share);

        Assert.IsTrue(goesOn);
        Assert.IsNotNull(share);
        Assert.AreEqual(CurlShareCode.Ok, share.Cleanup());
        Assert.AreEqual(CurlCode.Ok, steps.Result);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task ShareInit_OnACleanedUpLibrary_FailsWithTestErrMajorBad()
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await library.DisposeAsync();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.ShareInit(library, out CurlShare? share);

        Assert.IsFalse(goesOn);
        Assert.IsNull(share);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, steps.Result);
        Assert.AreEqual("curl_share_init() failed\n", error.ToString());
    }

    [TestMethod]
    public void ShareInit_RefusesANullLibrary()
    {
        var steps = new LibTestSteps(new StringWriter());

        Assert.ThrowsExactly<ArgumentNullException>(() => steps.ShareInit(null!, out _));
    }

    [TestMethod]
    public async Task UrlInit_OnALiveLibrary_GoesOnWithAHandle()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        var steps = new LibTestSteps(new StringWriter());

        bool goesOn = steps.UrlInit(library, out CurlUrlHandle? url);

        Assert.IsTrue(goesOn);
        Assert.IsNotNull(url);
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Host, "example.com", CurlUrlFlags.None));
        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.Host, CurlUrlFlags.None, out string? host));
        Assert.AreEqual("example.com", host);
        url.Dispose();
    }

    [TestMethod]
    public async Task UrlInit_OnACleanedUpLibrary_FailsWithTestErrMajorBad()
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await library.DisposeAsync();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.UrlInit(library, out CurlUrlHandle? url);

        Assert.IsFalse(goesOn);
        Assert.IsNull(url);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, steps.Result);
        Assert.AreEqual("curl_url() failed\n", error.ToString());
    }

    [TestMethod]
    public void UrlInit_RefusesANullLibrary()
    {
        var steps = new LibTestSteps(new StringWriter());

        Assert.ThrowsExactly<ArgumentNullException>(() => steps.UrlInit(null!, out _));
    }
}
