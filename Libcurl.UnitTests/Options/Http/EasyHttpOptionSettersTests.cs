using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options.Http;

[TestClass]
public sealed class EasyHttpOptionSettersTests
{
    [TestMethod]
    public void SetOption_Httpheader_ReplacesTheHeadersWithACopyAndNullEmptiesThem()
    {
        var headers = new List<string> { "X-A: 1", "Accept:" };
        var options = SetOk(new EasyOptions(), CurlOption.Httpheader, headers);
        headers.Add("X-B: 2");

        CollectionAssert.AreEqual(new[] { "X-A: 1", "Accept:" }, options.Http.HttpHeaders.ToArray());
        Assert.AreEqual(0, SetOk(options, CurlOption.Httpheader, null).Http.HttpHeaders.Count);
    }

    [TestMethod]
    public void SetOption_Useragent_SetsTheUserAgentAndNullClearsIt()
    {
        var options = SetOk(new EasyOptions(), CurlOption.Useragent, "agent/1");

        Assert.AreEqual("agent/1", options.Http.UserAgent);
        Assert.IsNull(SetOk(options, CurlOption.Useragent, null).Http.UserAgent);
    }

    [TestMethod]
    public void SetOption_Referer_SetsTheReferer()
    {
        Assert.AreEqual("http://a/", SetOk(new EasyOptions(), CurlOption.Referer, "http://a/").Http.Referer);
    }

    [TestMethod]
    public void SetOption_Autoreferer_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.Autoreferer, h => h.AutoReferer);
    }

    [TestMethod]
    public void SetOption_AcceptEncodingAndItsAliasEncoding_SetTheAcceptEncoding()
    {
        Assert.AreEqual(string.Empty, SetOk(new EasyOptions(), CurlOption.AcceptEncoding, string.Empty).Http.AcceptEncoding);
        Assert.AreEqual("gzip", SetOk(new EasyOptions(), CurlOption.Encoding, "gzip").Http.AcceptEncoding);
    }

    [TestMethod]
    public void SetOption_TransferEncoding_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.TransferEncoding, h => h.TransferEncoding);
    }

    [TestMethod]
    public void SetOption_HttpContentDecoding_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.HttpContentDecoding, h => h.HttpContentDecoding);
    }

    [TestMethod]
    public void SetOption_HttpTransferDecoding_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.HttpTransferDecoding, h => h.HttpTransferDecoding);
    }

    [TestMethod]
    public void SetOption_Followlocation_TakesZeroToThreeAndRefusesTheRest()
    {
        Assert.AreEqual(FollowLocation.FirstOnly, SetOk(new EasyOptions(), CurlOption.Followlocation, 3L).Http.FollowLocation);
        Assert.AreEqual(FollowLocation.All, SetOk(new EasyOptions(), CurlOption.Followlocation, 1).Http.FollowLocation);
        AssertRefused(CurlOption.Followlocation, 4L, CurlCode.BadFunctionArgument);
        AssertRefused(CurlOption.Followlocation, -1L, CurlCode.BadFunctionArgument);
    }

    [TestMethod]
    public void SetOption_Maxredirs_TakesMinusOneUpAndRefusesLess()
    {
        Assert.AreEqual(-1L, SetOk(new EasyOptions(), CurlOption.Maxredirs, -1L).Http.MaxRedirects);
        Assert.AreEqual(5L, SetOk(new EasyOptions(), CurlOption.Maxredirs, 5).Http.MaxRedirects);
        AssertRefused(CurlOption.Maxredirs, -2L, CurlCode.BadFunctionArgument);
    }

    [TestMethod]
    public void SetOption_Postredir_KeepsOnlyThePostBitsAndRefusesANegative()
    {
        Assert.AreEqual(PostRedirect.Post302, SetOk(new EasyOptions(), CurlOption.Postredir, PostRedirect.Post302).Http.PostRedirect);
        Assert.AreEqual(PostRedirect.All, SetOk(new EasyOptions(), CurlOption.Postredir, 0xFFL).Http.PostRedirect);
        AssertRefused(CurlOption.Postredir, -1L, CurlCode.BadFunctionArgument);
    }

    [TestMethod]
    public void SetOption_PostOne_MakesAPostAndClearsHttpgetAndNobody()
    {
        var options = new EasyOptions { Transfer = new EasyTransferOptions { NoBody = true }, Http = new EasyHttpOptions { HttpGet = true } };

        options = SetOk(options, CurlOption.Post, 1L);

        Assert.IsTrue(options.Http.Post);
        Assert.IsFalse(options.Http.HttpGet);
        Assert.IsFalse(options.Transfer.NoBody);
    }

    [TestMethod]
    public void SetOption_PostZero_ClearsThePostOnly()
    {
        var options = new EasyOptions { Transfer = new EasyTransferOptions { NoBody = true }, Http = new EasyHttpOptions { Post = true } };

        options = SetOk(options, CurlOption.Post, 0L);

        Assert.IsFalse(options.Http.Post);
        Assert.IsTrue(options.Transfer.NoBody);
    }

    [TestMethod]
    public void SetOption_PostfieldsGivenAByteArray_KeepsTheCallersBufferByReference()
    {
        var body = new byte[] { 1, 2, 3 };
        var options = SetOk(new EasyOptions { Http = new EasyHttpOptions { HttpGet = true } }, CurlOption.Postfields, body);
        body[0] = 9;

        CollectionAssert.AreEqual(new byte[] { 9, 2, 3 }, options.Http.PostFields!.Value.ToArray());
        Assert.IsTrue(options.Http.Post);
        Assert.IsFalse(options.Http.HttpGet);
    }

    [TestMethod]
    public void SetOption_PostfieldsGivenMemoryAStringOrNull_SetsTheBody()
    {
        var memory = new ReadOnlyMemory<byte>([4, 5]);

        Assert.IsTrue(memory.Equals(SetOk(new EasyOptions(), CurlOption.Postfields, memory).Http.PostFields!.Value));
        CollectionAssert.AreEqual("a=é"u8.ToArray(), SetOk(new EasyOptions(), CurlOption.Postfields, "a=é").Http.PostFields!.Value.ToArray());
        var cleared = SetOk(new EasyOptions { Http = new EasyHttpOptions { PostFields = memory } }, CurlOption.Postfields, null);
        Assert.IsNull(cleared.Http.PostFields);
        Assert.IsTrue(cleared.Http.Post);
    }

    [TestMethod]
    public void SetOption_PostfieldsGivenAnObjectThatIsNoBody_ReturnsBadFunctionArgument()
    {
        AssertRefused(CurlOption.Postfields, new object(), CurlCode.BadFunctionArgument);
        AssertRefused(CurlOption.Copypostfields, 5L, CurlCode.BadFunctionArgument);
    }

    [TestMethod]
    public void SetOption_Copypostfields_CopiesTheCallersBuffer()
    {
        var body = new byte[] { 1, 2, 3 };
        var options = SetOk(new EasyOptions(), CurlOption.Copypostfields, body);
        body[0] = 9;

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, options.Http.PostFields!.Value.ToArray());
        Assert.IsTrue(options.Http.Post);
    }

    [TestMethod]
    public void SetOption_CopypostfieldsAfterPostfieldsize_CopiesOnlyThatManyBytes()
    {
        var sized = SetOk(new EasyOptions(), CurlOption.Postfieldsize, 2L);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, SetOk(sized, CurlOption.Copypostfields, new byte[] { 1, 2, 3 }).Http.PostFields!.Value.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1 }, SetOk(sized, CurlOption.Copypostfields, new byte[] { 1 }).Http.PostFields!.Value.ToArray());
        Assert.IsNull(SetOk(sized, CurlOption.Copypostfields, null).Http.PostFields);
    }

    [TestMethod]
    public void SetOption_PostfieldsizeAndItsLargeForm_TakeMinusOneUpAndRefuseLess()
    {
        Assert.AreEqual(7L, SetOk(new EasyOptions(), CurlOption.Postfieldsize, 7).Http.PostFieldSize);
        Assert.AreEqual(1L << 40, SetOk(new EasyOptions(), CurlOption.PostfieldsizeLarge, 1L << 40).Http.PostFieldSize);
        AssertRefused(CurlOption.Postfieldsize, -2L, CurlCode.BadFunctionArgument);
        AssertRefused(CurlOption.PostfieldsizeLarge, -2L, CurlCode.BadFunctionArgument);
    }

    [TestMethod]
    public void SetOption_HttpgetOne_MakesAGetAndClearsPostNobodyAndUpload()
    {
        var options = new EasyOptions
        {
            Transfer = new EasyTransferOptions { NoBody = true, Upload = true },
            Http = new EasyHttpOptions { Post = true },
        };

        options = SetOk(options, CurlOption.Httpget, 1L);

        Assert.IsTrue(options.Http.HttpGet);
        Assert.IsFalse(options.Http.Post);
        Assert.IsFalse(options.Transfer.NoBody);
        Assert.IsFalse(options.Transfer.Upload);
    }

    [TestMethod]
    public void SetOption_HttpgetZero_ChangesNothing()
    {
        var options = new EasyOptions { Http = new EasyHttpOptions { Post = true } };

        Assert.AreSame(options, SetOk(options, CurlOption.Httpget, 0L));
    }

    [TestMethod]
    public void SetOption_MimeOptions_KeepsOnlyTheFormEscapeBit()
    {
        Assert.AreEqual(MimeOptions.FormEscape, SetOk(new EasyOptions(), CurlOption.MimeOptions, 3L).Http.MimeOptions);
        Assert.AreEqual(MimeOptions.None, SetOk(new EasyOptions(), CurlOption.MimeOptions, 2L).Http.MimeOptions);
    }

    [TestMethod]
    public void SetOption_HttpVersion_TakesEachCurlHttpVersionValue()
    {
        foreach (var version in Enum.GetValues<HttpVersion>())
        {
            Assert.AreEqual(version, SetOk(new EasyOptions(), CurlOption.HttpVersion, version).Http.HttpVersion);
        }

        Assert.AreEqual(HttpVersion.Http3, SetOk(new EasyOptions(), CurlOption.HttpVersion, 30L).Http.HttpVersion);
    }

    [TestMethod]
    public void SetOption_HttpVersionOutOfRange_RefusesANegativeAsBadArgumentAndAnUnknownAsUnsupported()
    {
        AssertRefused(CurlOption.HttpVersion, -1L, CurlCode.BadFunctionArgument);
        AssertRefused(CurlOption.HttpVersion, 6L, CurlCode.UnsupportedProtocol);
        AssertRefused(CurlOption.HttpVersion, 32L, CurlCode.UnsupportedProtocol);
    }

    [TestMethod]
    public void SetOption_Http09Allowed_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.Http09Allowed, h => h.Http09Allowed);
    }

    [TestMethod]
    public void SetOption_IgnoreContentLength_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.IgnoreContentLength, h => h.IgnoreContentLength);
    }

    [TestMethod]
    public void SetOption_Expect100TimeoutMs_SetsTheTimeoutCutAtIntMaxAndRefusesANegative()
    {
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), SetOk(new EasyOptions(), CurlOption.Expect100TimeoutMs, 250L).Http.Expect100Timeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(int.MaxValue), SetOk(new EasyOptions(), CurlOption.Expect100TimeoutMs, long.MaxValue).Http.Expect100Timeout);
        AssertRefused(CurlOption.Expect100TimeoutMs, -1L, CurlCode.BadFunctionArgument);
    }

    [TestMethod]
    public void SetOption_Http200aliases_SetsTheAliases()
    {
        CollectionAssert.AreEqual(new[] { "ICY 200 OK" }, SetOk(new EasyOptions(), CurlOption.Http200aliases, new[] { "ICY 200 OK" }).Http.Http200Aliases.ToArray());
    }

    [TestMethod]
    public void SetOption_RequestTarget_SetsTheRequestTarget()
    {
        Assert.AreEqual("*", SetOk(new EasyOptions(), CurlOption.RequestTarget, "*").Http.RequestTarget);
    }

    [TestMethod]
    public void SetOption_Cookie_SetsTheCookieHeader()
    {
        Assert.AreEqual("a=1; b=2", SetOk(new EasyOptions(), CurlOption.Cookie, "a=1; b=2").Http.Cookie);
    }

    [TestMethod]
    public void SetOption_Cookiefile_AppendsEachFileAndNullClearsTheList()
    {
        var options = SetOk(SetOk(new EasyOptions(), CurlOption.Cookiefile, "one.txt"), CurlOption.Cookiefile, string.Empty);

        CollectionAssert.AreEqual(new[] { "one.txt", string.Empty }, options.Http.CookieFiles.ToArray());
        Assert.AreEqual(0, SetOk(options, CurlOption.Cookiefile, null).Http.CookieFiles.Count);
    }

    [TestMethod]
    public void SetOption_Cookiejar_SetsTheCookieJar()
    {
        Assert.AreEqual("-", SetOk(new EasyOptions(), CurlOption.Cookiejar, "-").Http.CookieJar);
    }

    [TestMethod]
    public void SetOption_Cookiesession_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.Cookiesession, h => h.CookieSession);
    }

    [TestMethod]
    public void SetOption_Cookielist_AppendsEachLineInOrderAndIgnoresNull()
    {
        var options = SetOk(SetOk(new EasyOptions(), CurlOption.Cookielist, "ALL"), CurlOption.Cookielist, "FLUSH");

        CollectionAssert.AreEqual(new[] { "ALL", "FLUSH" }, options.Http.CookieList.ToArray());
        Assert.AreSame(options.Http, SetOk(options, CurlOption.Cookielist, null).Http);
    }

    [TestMethod]
    public void SetOption_Altsvc_SetsTheCacheFile()
    {
        Assert.AreEqual("altsvc.txt", SetOk(new EasyOptions(), CurlOption.Altsvc, "altsvc.txt").Http.AltSvc);
    }

    [TestMethod]
    public void SetOption_AltsvcCtrl_SetsTheBits()
    {
        Assert.AreEqual(AltSvcControl.H1 | AltSvcControl.H2, SetOk(new EasyOptions(), CurlOption.AltsvcCtrl, 24L).Http.AltSvcControl);
    }

    [TestMethod]
    public void SetOption_Hsts_SetsTheCacheFile()
    {
        Assert.AreEqual("hsts.txt", SetOk(new EasyOptions(), CurlOption.Hsts, "hsts.txt").Http.Hsts);
    }

    [TestMethod]
    public void SetOption_HstsCtrl_KeepsTheBitsOnlyWhenEnabled()
    {
        Assert.AreEqual(HstsControl.Enable | HstsControl.ReadOnlyFile, SetOk(new EasyOptions(), CurlOption.HstsCtrl, 7L).Http.HstsControl);
        Assert.AreEqual(HstsControl.None, SetOk(new EasyOptions(), CurlOption.HstsCtrl, 2L).Http.HstsControl);
    }

    [TestMethod]
    public void SetOption_Pipewait_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.Pipewait, h => h.PipeWait);
    }

    [TestMethod]
    public void SetOption_StreamDependsAndItsExclusiveForm_SetTheParentAndNullClearsIt()
    {
        var parent = new object();

        Assert.AreEqual(new EasyStreamDependency(parent, false), SetOk(new EasyOptions(), CurlOption.StreamDepends, parent).Http.StreamDependency);
        var exclusive = SetOk(new EasyOptions(), CurlOption.StreamDependsE, parent);
        Assert.AreEqual(new EasyStreamDependency(parent, true), exclusive.Http.StreamDependency);
        Assert.IsNull(SetOk(exclusive, CurlOption.StreamDepends, null).Http.StreamDependency);
    }

    [TestMethod]
    public void SetOption_StreamWeight_KeepsOneTo256AndIgnoresTheRest()
    {
        Assert.AreEqual(1, SetOk(new EasyOptions(), CurlOption.StreamWeight, 1L).Http.StreamWeight);
        Assert.AreEqual(256, SetOk(new EasyOptions(), CurlOption.StreamWeight, 256L).Http.StreamWeight);
        Assert.AreEqual(16, SetOk(new EasyOptions(), CurlOption.StreamWeight, 0L).Http.StreamWeight);
        Assert.AreEqual(16, SetOk(new EasyOptions(), CurlOption.StreamWeight, 257L).Http.StreamWeight);
    }

    [TestMethod]
    public void SetOption_SslEnableAlpn_IsOnForNonZeroAndOffForZero()
    {
        AssertSwitch(CurlOption.SslEnableAlpn, h => h.SslEnableAlpn);
    }

    [TestMethod]
    public void SetOption_Httppost_IsSetByTheFormArea()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Httppost, null));
    }

    private static EasyOptions SetOk(EasyOptions options, CurlOption option, object? value)
    {
        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        return options;
    }

    private static void AssertRefused(CurlOption option, object? value, CurlCode expected)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(expected, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        Assert.AreSame(before, options);
    }

    private static void AssertSwitch(CurlOption option, Func<EasyHttpOptions, bool> read)
    {
        Assert.IsTrue(read(SetOk(new EasyOptions(), option, 2L).Http), $"{option} 2");
        Assert.IsFalse(read(SetOk(new EasyOptions(), option, 0).Http), $"{option} 0");
    }
}
