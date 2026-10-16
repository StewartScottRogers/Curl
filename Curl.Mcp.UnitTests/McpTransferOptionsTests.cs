using Curl.Api.Options;
using Curl.Api.Options.Http;

namespace Curl.Mcp;

[TestClass]
public sealed class McpTransferOptionsTests
{
    [TestMethod]
    public void Build_PlainGet_SetsOnlyTheUrl()
    {
        EasyOptions defaults = new();

        EasyOptions options = McpTransferOptions.Build(new McpTransferRequest("http://h/"), null);

        Assert.AreEqual(defaults with { Network = defaults.Network with { Url = "http://h/" }, Http = options.Http }, options);
        Assert.AreEqual(0, options.Http.HttpHeaders.Count);
        Assert.AreEqual(defaults.Http, options.Http with { HttpHeaders = defaults.Http.HttpHeaders });
    }

    [TestMethod]
    public void Build_Head_SetsNoBodyAndNoCustomRequest()
    {
        EasyOptions options = McpTransferOptions.Build(new McpTransferRequest("http://h/") { Method = "HEAD" }, null);

        Assert.IsTrue(options.Transfer.NoBody);
        Assert.IsNull(options.Transfer.CustomRequest);
        Assert.IsFalse(options.Http.Post);
    }

    [TestMethod]
    public void Build_PostWithoutABody_PostsAnEmptyBody()
    {
        EasyOptions options = McpTransferOptions.Build(new McpTransferRequest("http://h/") { Method = "POST" }, null);

        Assert.IsTrue(options.Http.Post);
        Assert.AreEqual(0, options.Http.PostFields!.Value.Length);
        Assert.IsNull(options.Transfer.CustomRequest);
    }

    [TestMethod]
    public void Build_PostWithABody_PostsItWithNoCustomRequest()
    {
        EasyOptions options = McpTransferOptions.Build(new McpTransferRequest("http://h/") { Method = "POST" }, [1, 2]);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, options.Http.PostFields!.Value.ToArray());
        Assert.IsNull(options.Transfer.CustomRequest);
    }

    [TestMethod]
    public void Build_GetWithABody_PostsItAsACustomGet()
    {
        EasyOptions options = McpTransferOptions.Build(new McpTransferRequest("http://h/"), [1]);

        Assert.IsTrue(options.Http.Post);
        Assert.AreEqual("GET", options.Transfer.CustomRequest);
    }

    [TestMethod]
    public void Build_DeleteWithoutABody_IsACustomRequestOnly()
    {
        EasyOptions options = McpTransferOptions.Build(new McpTransferRequest("http://h/") { Method = "DELETE" }, null);

        Assert.AreEqual("DELETE", options.Transfer.CustomRequest);
        Assert.IsFalse(options.Http.Post);
        Assert.IsNull(options.Http.PostFields);
    }

    [TestMethod]
    public void Build_FalseFlags_LeaveRedirectsAndVerificationAtTheirDefaults()
    {
        EasyOptions defaults = new();

        EasyOptions options = McpTransferOptions.Build(new McpTransferRequest("http://h/") { FollowRedirects = false, Insecure = false }, null);

        Assert.AreEqual(FollowLocation.None, options.Http.FollowLocation);
        Assert.AreEqual(defaults.Tls, options.Tls);
    }

    [TestMethod]
    public void Build_HeaderValues_AreTrimmedAndAnEmptyOneUsesTheSemicolonForm()
    {
        McpTransferRequest request = new("http://h/") { Headers = [new McpTransferHeader("A", "  x  "), new McpTransferHeader("B", "   ")] };

        EasyOptions options = McpTransferOptions.Build(request, null);

        CollectionAssert.AreEqual(new[] { "A: x", "B;" }, options.Http.HttpHeaders.ToArray());
    }

    [TestMethod]
    public void Build_NullRequest_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => McpTransferOptions.Build(null!, null));
    }
}
