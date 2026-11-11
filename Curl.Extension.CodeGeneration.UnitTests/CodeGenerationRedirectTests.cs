namespace Curl.Extension.CodeGeneration.UnitTests;

[TestClass]
public sealed class CodeGenerationRedirectTests
{
    private string directory = null!;

    [TestInitialize]
    public void CreateDirectory() => directory = Directory.CreateTempSubdirectory("codegen").FullName;

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(directory, true);

    [TestMethod]
    public async Task ExecuteAsync_ARedirectFollowUp_AddsNoRequestAndStillRunsTheTransfer()
    {
        var path = Path.Combine(directory, "l.ps1");
        var hook = new CodeGenerationTransferHook([new CodeFile(path, PowerShellScript.For)]);
        var expected = new TransferResult(CurlExitCode.Ok, 0);

        await hook.ExecuteAsync(Context("http://h/redir", 0), _ => ValueTask.FromResult(expected));
        var result = await hook.ExecuteAsync(Context("http://h/plain", 1), _ => ValueTask.FromResult(expected));

        Assert.AreSame(expected, result);
        var script = await File.ReadAllTextAsync(path);
        Assert.Contains("http://h/redir", script);
        Assert.DoesNotContain("http://h/plain", script);
    }

    private static TransferContext Context(string url, int redirectsFollowed) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = Stream.Null,
        Http = new HttpRequestOptions { FollowRedirects = true, RedirectsFollowed = redirectsFollowed },
    };
}
