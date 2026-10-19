using Curl.Console;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Engine;

/// <summary>
/// Tests for <see cref="HttpRequestOptionsMapping.Of" /> driven by an <see cref="EasyHttpOptions" /> record built
/// directly, as a libcurl caller sets it, rather than through a command line (BL-2345).
/// </summary>
[TestClass]
public sealed class HttpRequestOptionsMappingTests
{
    private static readonly CommandLineOptions NoOptions = CommandLineParser.Parse(["http://example.com/"], _ => true).Options!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Of_RecordSettings_AreCopiedOntoTheRequestOptions()
    {
        EasyHttpOptions http = new()
        {
            HttpHeaders = ["X: 1"],
            UserAgent = "a/1",
            Referer = "http://r/",
            AutoReferer = true,
            RequestTarget = "*",
            AcceptEncoding = string.Empty,
            TransferEncoding = true,
            IgnoreContentLength = true,
            Http09Allowed = true,
            Http200Aliases = ["ICY 200 OK"],
            FollowLocation = FollowLocation.ObeyCode,
            MaxRedirects = 4,
            HttpVersion = HttpVersion.Http10,
            Expect100Timeout = TimeSpan.FromMilliseconds(250),
            PostFields = new byte[] { (byte)'a' },
        };
        Diagnostics.Arrange("record", http);

        HttpRequestOptions mapped = HttpRequestOptionsMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions), http, new EasyBehaviourOptions(), new EasyTransferOptions(), new EasyAuthenticationOptions(), ifNoneMatchHeaders: ["If-None-Match: \"e\""]);

        Diagnostics.Act("headers", string.Join(" | ", mapped.Headers));
        Diagnostics.Assert("headers", "X: 1 | If-None-Match: \"e\"", string.Join(" | ", mapped.Headers));
        CollectionAssert.AreEqual(new[] { "X: 1", "If-None-Match: \"e\"" }, mapped.Headers.ToArray());
        Assert.AreEqual("a/1", mapped.UserAgent);
        Assert.AreEqual("http://r/", mapped.Referer);
        Assert.IsTrue(mapped.AutoReferer);
        Assert.AreEqual("*", mapped.RequestTarget);
        Assert.IsTrue(mapped.Compressed);
        Assert.IsTrue(mapped.TransferEncoding);
        Assert.IsTrue(mapped.IgnoreContentLength);
        Assert.IsTrue(mapped.AllowHttp09Reply);
        CollectionAssert.AreEqual(new[] { "ICY 200 OK" }, mapped.Http200Aliases.ToArray());
        Assert.IsTrue(mapped.FollowRedirects);
        Assert.AreEqual(4, mapped.MaxRedirects);
        Assert.AreEqual(HttpVersionPreference.Http10, mapped.Version);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), mapped.ContinueWait);
        BytesBody body = (BytesBody)mapped.Body!;
        Assert.AreEqual(HttpRequestOptionsMapping.FormUrlEncoded, body.ContentType);
        CollectionAssert.AreEqual(new[] { (byte)'a' }, body.Content.ToArray());
        Assert.IsFalse(mapped.Raw);
    }

    [TestMethod]
    public void Of_DefaultRecord_SendsNoBodyAndFollowsNothing()
    {
        HttpRequestOptions mapped = HttpRequestOptionsMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions), new EasyHttpOptions(), new EasyBehaviourOptions(), new EasyTransferOptions(), new EasyAuthenticationOptions());

        Diagnostics.Act("follow / body / compressed", $"{mapped.FollowRedirects}/{mapped.Body is null}/{mapped.Compressed}");

        Diagnostics.Assert("follow / body / compressed", "False/True/False", $"{mapped.FollowRedirects}/{mapped.Body is null}/{mapped.Compressed}");
        Assert.IsFalse(mapped.FollowRedirects);
        Assert.IsNull(mapped.Body);
        Assert.IsFalse(mapped.Compressed);
        Assert.AreEqual(HttpVersionPreference.Http11, mapped.Version);
        Assert.AreEqual(HttpRequestOptions.DefaultContinueWait, mapped.ContinueWait);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Of_KeepSendingOnError_IsCopiedFromTheBehaviourOptions(bool keepSendingOnError)
    {
        Diagnostics.Arrange("keep sending on error", keepSendingOnError);

        HttpRequestOptions mapped = HttpRequestOptionsMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions), new EasyHttpOptions(), new EasyBehaviourOptions { KeepSendingOnError = keepSendingOnError }, new EasyTransferOptions(), new EasyAuthenticationOptions());

        Diagnostics.Assert("keep sending on error", keepSendingOnError, mapped.KeepSendingOnError);
        Assert.AreEqual(keepSendingOnError, mapped.KeepSendingOnError);
    }

    [TestMethod]
    public void Of_NoBodyWithPostFieldsAndFormBody_SendsNoBody()
    {
        EasyHttpOptions http = new() { PostFields = "moo"u8.ToArray(), Post = true };
        HttpRequestBody formBody = new BytesBody("x"u8.ToArray(), "text/plain");

        HttpRequestOptions mapped = HttpRequestOptionsMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions), http, new EasyBehaviourOptions(), new EasyTransferOptions { NoBody = true }, new EasyAuthenticationOptions(), formBody);

        Diagnostics.Act("body is null", mapped.Body is null);
        Diagnostics.Assert("body is null", true, mapped.Body is null);
        Assert.IsNull(mapped.Body);
    }

    [TestMethod]
    [DataRow(true, true, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(false, false, true)]
    public void Of_Decodings_AreRawOnlyWhenBothAreOff(bool contentDecoding, bool transferDecoding, bool expectedRaw)
    {
        Diagnostics.Arrange("content / transfer decoding", $"{contentDecoding}/{transferDecoding}");

        bool raw = HttpRequestOptionsMapping.Of(
            EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions),
            new EasyHttpOptions { HttpContentDecoding = contentDecoding, HttpTransferDecoding = transferDecoding }, new EasyBehaviourOptions(), new EasyTransferOptions(), new EasyAuthenticationOptions()).Raw;

        Diagnostics.Act("raw", raw);

        Diagnostics.Assert("raw", expectedRaw, raw);
        Assert.AreEqual(expectedRaw, raw);
    }
}
