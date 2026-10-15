namespace Curl.Api.Options.Http;

[TestClass]
public sealed class EasyHttpOptionsTests
{
    private static readonly EasyHttpOptions Defaults = new();

    [TestMethod]
    public void New_EasyHttpOptions_LeavesHttpStringsUnset()
    {
        Assert.IsNull(Defaults.UserAgent);
        Assert.IsNull(Defaults.Referer);
        Assert.IsNull(Defaults.AcceptEncoding);
        Assert.IsNull(Defaults.RequestTarget);
        Assert.IsNull(Defaults.Cookie);
        Assert.IsNull(Defaults.CookieJar);
        Assert.IsNull(Defaults.AltSvc);
        Assert.IsNull(Defaults.Hsts);
    }

    [TestMethod]
    public void New_EasyHttpOptions_LeavesEveryListEmpty()
    {
        Assert.IsEmpty(Defaults.HttpHeaders);
        Assert.IsEmpty(Defaults.Http200Aliases);
        Assert.IsEmpty(Defaults.CookieFiles);
        Assert.IsEmpty(Defaults.CookieList);
    }

    [TestMethod]
    public void New_EasyHttpOptions_LeavesEveryOffSwitchOff()
    {
        Assert.IsFalse(Defaults.AutoReferer);
        Assert.IsFalse(Defaults.TransferEncoding);
        Assert.IsFalse(Defaults.Post);
        Assert.IsFalse(Defaults.HttpGet);
        Assert.IsFalse(Defaults.Http09Allowed);
        Assert.IsFalse(Defaults.IgnoreContentLength);
        Assert.IsFalse(Defaults.CookieSession);
        Assert.IsFalse(Defaults.PipeWait);
    }

    [TestMethod]
    public void New_EasyHttpOptions_DecodesContentAndTransferEncodings()
    {
        Assert.IsTrue(Defaults.HttpContentDecoding);
        Assert.IsTrue(Defaults.HttpTransferDecoding);
    }

    [TestMethod]
    public void New_EasyHttpOptions_OffersAlpn()
    {
        Assert.IsTrue(Defaults.SslEnableAlpn);
    }

    [TestMethod]
    public void New_EasyHttpOptions_FollowsNoRedirectButAllowsThirty()
    {
        Assert.AreEqual(FollowLocation.None, Defaults.FollowLocation);
        Assert.AreEqual(30L, Defaults.MaxRedirects);
        Assert.AreEqual(PostRedirect.None, Defaults.PostRedirect);
    }

    [TestMethod]
    public void New_EasyHttpOptions_HasNoBodyAndMeasuresPostFieldsItself()
    {
        Assert.IsNull(Defaults.PostFields);
        Assert.AreEqual(-1L, Defaults.PostFieldSize);
        Assert.IsNull(Defaults.MimePost);
        Assert.IsNull(Defaults.HttpPost);
        Assert.AreEqual(MimeOptions.None, Defaults.MimeOptions);
    }

    [TestMethod]
    public void New_EasyHttpOptions_LetsLibcurlPickTheVersionAndWaitsOneSecondFor100()
    {
        Assert.AreEqual(HttpVersion.None, Defaults.HttpVersion);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1000), Defaults.Expect100Timeout);
    }

    [TestMethod]
    public void New_EasyHttpOptions_DisablesAltSvcAndHsts()
    {
        Assert.AreEqual(AltSvcControl.None, Defaults.AltSvcControl);
        Assert.AreEqual(HstsControl.None, Defaults.HstsControl);
    }

    [TestMethod]
    public void New_EasyHttpOptions_HasNoStreamDependencyAndWeightSixteen()
    {
        Assert.IsNull(Defaults.StreamDependency);
        Assert.AreEqual(16, Defaults.StreamWeight);
    }

    [TestMethod]
    public void HttpVersion_Members_HoldLibcurlsCurlHttpVersionValues()
    {
        Assert.AreEqual(0, (int)HttpVersion.None);
        Assert.AreEqual(1, (int)HttpVersion.Http10);
        Assert.AreEqual(2, (int)HttpVersion.Http11);
        Assert.AreEqual(3, (int)HttpVersion.Http2);
        Assert.AreEqual(4, (int)HttpVersion.Http2Tls);
        Assert.AreEqual(5, (int)HttpVersion.Http2PriorKnowledge);
        Assert.AreEqual(30, (int)HttpVersion.Http3);
        Assert.AreEqual(31, (int)HttpVersion.Http3Only);
    }

    [TestMethod]
    public void FollowLocation_Members_HoldLibcurlsCurlfollowValues()
    {
        Assert.AreEqual(0, (int)FollowLocation.None);
        Assert.AreEqual(1, (int)FollowLocation.All);
        Assert.AreEqual(2, (int)FollowLocation.ObeyCode);
        Assert.AreEqual(3, (int)FollowLocation.FirstOnly);
    }

    [TestMethod]
    public void PostRedirect_Members_HoldLibcurlsCurlRedirPostBits()
    {
        Assert.AreEqual(0, (int)PostRedirect.None);
        Assert.AreEqual(1, (int)PostRedirect.Post301);
        Assert.AreEqual(2, (int)PostRedirect.Post302);
        Assert.AreEqual(4, (int)PostRedirect.Post303);
        Assert.AreEqual(7, (int)PostRedirect.All);
    }

    [TestMethod]
    public void MimeOptions_Members_HoldLibcurlsCurlmimeoptBits()
    {
        Assert.AreEqual(0, (int)MimeOptions.None);
        Assert.AreEqual(1, (int)MimeOptions.FormEscape);
    }

    [TestMethod]
    public void AltSvcControl_Members_HoldLibcurlsCurlaltsvcBits()
    {
        Assert.AreEqual(0, (int)AltSvcControl.None);
        Assert.AreEqual(4, (int)AltSvcControl.ReadOnlyFile);
        Assert.AreEqual(8, (int)AltSvcControl.H1);
        Assert.AreEqual(16, (int)AltSvcControl.H2);
        Assert.AreEqual(32, (int)AltSvcControl.H3);
    }

    [TestMethod]
    public void HstsControl_Members_HoldLibcurlsCurlhstsBits()
    {
        Assert.AreEqual(0, (int)HstsControl.None);
        Assert.AreEqual(1, (int)HstsControl.Enable);
        Assert.AreEqual(2, (int)HstsControl.ReadOnlyFile);
    }

    [TestMethod]
    public void With_EachProperty_ChangesOnlyThatProperty()
    {
        AssertChangesOnly(Defaults with { HttpHeaders = ["X-A: 1"] }, o => o with { HttpHeaders = Defaults.HttpHeaders });
        AssertChangesOnly(Defaults with { UserAgent = "curl/8.22.0" }, o => o with { UserAgent = null });
        AssertChangesOnly(Defaults with { Referer = "http://r/" }, o => o with { Referer = null });
        AssertChangesOnly(Defaults with { AutoReferer = true }, o => o with { AutoReferer = false });
        AssertChangesOnly(Defaults with { AcceptEncoding = "" }, o => o with { AcceptEncoding = null });
        AssertChangesOnly(Defaults with { TransferEncoding = true }, o => o with { TransferEncoding = false });
        AssertChangesOnly(Defaults with { HttpContentDecoding = false }, o => o with { HttpContentDecoding = true });
        AssertChangesOnly(Defaults with { HttpTransferDecoding = false }, o => o with { HttpTransferDecoding = true });
        AssertChangesOnly(Defaults with { FollowLocation = FollowLocation.All }, o => o with { FollowLocation = FollowLocation.None });
        AssertChangesOnly(Defaults with { MaxRedirects = -1 }, o => o with { MaxRedirects = 30 });
        AssertChangesOnly(Defaults with { PostRedirect = PostRedirect.All }, o => o with { PostRedirect = PostRedirect.None });
        AssertChangesOnly(Defaults with { Post = true }, o => o with { Post = false });
        AssertChangesOnly(Defaults with { PostFields = new byte[] { 1 } }, o => o with { PostFields = null });
        AssertChangesOnly(Defaults with { PostFieldSize = 0 }, o => o with { PostFieldSize = -1 });
        AssertChangesOnly(Defaults with { HttpGet = true }, o => o with { HttpGet = false });
        AssertChangesOnly(Defaults with { MimePost = new EasyMimeBody() }, o => o with { MimePost = null });
        AssertChangesOnly(Defaults with { MimeOptions = MimeOptions.FormEscape }, o => o with { MimeOptions = MimeOptions.None });
        AssertChangesOnly(Defaults with { HttpPost = new EasyMimeBody() }, o => o with { HttpPost = null });
        AssertChangesOnly(Defaults with { HttpVersion = HttpVersion.Http3Only }, o => o with { HttpVersion = HttpVersion.None });
        AssertChangesOnly(Defaults with { Http09Allowed = true }, o => o with { Http09Allowed = false });
        AssertChangesOnly(Defaults with { IgnoreContentLength = true }, o => o with { IgnoreContentLength = false });
        AssertChangesOnly(Defaults with { Expect100Timeout = TimeSpan.Zero }, o => o with { Expect100Timeout = TimeSpan.FromSeconds(1) });
        AssertChangesOnly(Defaults with { Http200Aliases = ["ICY 200 OK"] }, o => o with { Http200Aliases = Defaults.Http200Aliases });
        AssertChangesOnly(Defaults with { RequestTarget = "*" }, o => o with { RequestTarget = null });
        AssertChangesOnly(Defaults with { Cookie = "a=b" }, o => o with { Cookie = null });
        AssertChangesOnly(Defaults with { CookieFiles = [""] }, o => o with { CookieFiles = Defaults.CookieFiles });
        AssertChangesOnly(Defaults with { CookieJar = "-" }, o => o with { CookieJar = null });
        AssertChangesOnly(Defaults with { CookieSession = true }, o => o with { CookieSession = false });
        AssertChangesOnly(Defaults with { CookieList = ["ALL"] }, o => o with { CookieList = Defaults.CookieList });
        AssertChangesOnly(Defaults with { AltSvc = "altsvc.txt" }, o => o with { AltSvc = null });
        AssertChangesOnly(Defaults with { AltSvcControl = AltSvcControl.H1 }, o => o with { AltSvcControl = AltSvcControl.None });
        AssertChangesOnly(Defaults with { Hsts = "hsts.txt" }, o => o with { Hsts = null });
        AssertChangesOnly(Defaults with { HstsControl = HstsControl.Enable }, o => o with { HstsControl = HstsControl.None });
        AssertChangesOnly(Defaults with { PipeWait = true }, o => o with { PipeWait = false });
        AssertChangesOnly(Defaults with { StreamDependency = new EasyStreamDependency(new object(), true) }, o => o with { StreamDependency = null });
        AssertChangesOnly(Defaults with { StreamWeight = 256 }, o => o with { StreamWeight = 16 });
        AssertChangesOnly(Defaults with { SslEnableAlpn = false }, o => o with { SslEnableAlpn = true });
    }

    private static void AssertChangesOnly(EasyHttpOptions changed, Func<EasyHttpOptions, EasyHttpOptions> restore)
    {
        Assert.AreNotEqual(Defaults, changed);
        Assert.AreEqual(Defaults, restore(changed));
    }
}
