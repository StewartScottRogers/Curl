using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1135Script"/>: upstream's <c>tests/test1135.pl</c> over the headers' <c>CURL_EXTERN</c>
/// declarations, one-line and two-line, in declaration order and sorted, with its heading and root arguments.
/// </summary>
[TestClass]
public sealed class UpstreamTest1135ScriptTests
{
    private string root = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1135-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "include", "curl"));
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void List_Headers_ReadsEachDeclarationInHeaderThenLineOrder()
    {
        Write("z.h", "CURL_EXTERN int   curl_z(void);\n");
        Write("a.h", "CURL_EXTERN CURL_DEPRECATED(7.1, \"use x\") int curl_old(int);\nCURL_EXTERN struct x *\n   curl_two_line(int a,\n  int b);\nCURL_EXTERN char *\n(not_a_name)\nCURL_EXTERN 1(x)\nCURL_EXTERN X\n1(y)\nCURL_EXTERN int\nno paren here\nCURL_EXTERN int\n");
        Write("notes.txt", "CURL_EXTERN int curl_not_a_header(void);\n");

        CollectionAssert.AreEqual(new[] { "curl_old", "curl_two_line", "curl_z" }, UpstreamTest1135Script.List(root, sort: false));
    }

    [TestMethod]
    public void List_Sorted_OrdersByByte()
    {
        Write("a.h", "CURL_EXTERN int curl_b(void);\nCURL_EXTERN int curl_a(void);\n");

        CollectionAssert.AreEqual(new[] { "curl_a", "curl_b" }, UpstreamTest1135Script.List(root, sort: true));
    }

    [TestMethod]
    public void List_HeaderFolderMissing_Throws() =>
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => UpstreamTest1135Script.List(Path.Combine(root, "none"), sort: false));

    [TestMethod]
    public async Task RunAsync_HeadingAndSortAndRoot_WritesHeadingThenSortedNames()
    {
        Write("a.h", "CURL_EXTERN int curl_b(void);\nCURL_EXTERN int curl_a(void);\n");

        Assert.AreEqual("EXPORTS\ncurl_a\ncurl_b\n", await Run("--heading=EXPORTS", "--sort", root));
    }

    [TestMethod]
    public async Task RunAsync_OnlyRoot_WritesNamesInOrder()
    {
        Write("a.h", "CURL_EXTERN int curl_b(void);\nCURL_EXTERN int curl_a(void);\n");

        Assert.AreEqual("curl_b\ncurl_a\n", await Run(root));
    }

    [TestMethod]
    public async Task RunAsync_NoRoot_ReadsTheCurrentFolder()
    {
        string previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(root);
        try
        {
            Write("a.h", "CURL_EXTERN int curl_c(void);\n");

            Assert.AreEqual("curl_c\n", await Run());
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1135Script.RunAsync(null!, _ => Task.FromResult(0)));

    private static async Task<string> Run(params string[] arguments)
    {
        using MemoryStream standardOutput = new();
        UpstreamCurlInvocation invocation = new(arguments, standardOutput, new MemoryStream(), Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1135Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        return Encoding.UTF8.GetString(standardOutput.ToArray());
    }

    private void Write(string name, string text) => File.WriteAllText(Path.Combine(root, "include", "curl", name), text);

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
