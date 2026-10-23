using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest745Script"/>: upstream's <c>tests/test745.pl</c> over <c>typecheck-gcc.h</c>, <c>curl.h</c>
/// and <c>multi.h</c>, which option types it leaves out, and both kinds of difference it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest745ScriptTests
{
    private const string AddedOptionChecks =
        "(option) == CURLOPT_SOCKS5_GSSAPI_SERVICE\n(option) == CURLOPT_CONV_FROM_NETWORK_FUNCTION\n"
        + "(option) == CURLOPT_CONV_FROM_UTF8_FUNCTION\n(option) == CURLOPT_CONV_TO_NETWORK_FUNCTION\n";

    private string root = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test745-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "include", "curl"));
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_VendoredUpstreamHeaders_WritesOk()
    {
        Assert.AreEqual("OK\n", UpstreamTest745Script.Check(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData")));
    }

    [TestMethod]
    public void Check_HeadersInSync_WritesOkAndLeavesOutLongValuesBlobAndOffTOptions()
    {
        WriteHeaders(
            "(option) == CURLOPT_URL) || x\n(option) == CURLOPT_COOKIE\n",
            "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 2),\nCURLOPT(CURLOPT_COOKIE, CURLOPTTYPE_OBJECTPOINT, 22),\n"
            + "CURLOPT(CURLOPT_PORT, CURLOPTTYPE_LONG, 3),\nCURLOPT(CURLOPT_AUTH, CURLOPTTYPE_VALUES, 107),\n"
            + "CURLOPT(CURLOPT_BLOBBY, CURLOPTTYPE_BLOB, 1),\nCURLOPT(CURLOPT_SIZE, CURLOPTTYPE_OFF_T, 5),\nunrelated\n",
            "CURLMOPT(CURLMOPT_PIPE, CURLOPTTYPE_LONG, 3),\nCURLMOPT(CURLMOPT_SIZE, CURLOPTTYPE_OFF_T, 5),\n");

        Assert.AreEqual("OK\n", UpstreamTest745Script.Check(root));
    }

    [TestMethod]
    public void Check_BothKindsOfDifference_WritesEachInSortedOrderAndNoOk()
    {
        WriteHeaders(
            "(option) == CURLOPT_URL\n(option) == CURLOPT_ZETA\n(option) == CURLOPT_ALPHA\n",
            "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 2),\nCURLOPT(CURLOPT_UNCHECKED, CURLOPTTYPE_STRINGPOINT, 3),\n",
            "CURLMOPT(CURLMOPT_CALLBACK, CURLOPTTYPE_FUNCTIONPOINT, 3),\nCURLMOPT(CURLMOPT_BLOB, CURLOPTTYPE_BLOB, 4),\n");

        Assert.AreEqual(
            "CURLOPT_ALPHA is not in curl.h\nCURLOPT_ZETA is not in curl.h\n"
            + "CURLMOPT_BLOB is not checked in typecheck-gcc-h\nCURLMOPT_CALLBACK is not checked in typecheck-gcc-h\nCURLOPT_UNCHECKED is not checked in typecheck-gcc-h\n",
            UpstreamTest745Script.Check(root));
    }

    [TestMethod]
    public void Check_MissingHeader_Throws()
    {
        File.Delete(Path.Combine(root, "include", "curl", "typecheck-gcc.h"));

        Assert.Throws<FileNotFoundException>(() => UpstreamTest745Script.Check(root));
    }

    [TestMethod]
    public async Task RunAsync_TheSourceRoot_WritesTheCheckToStandardOutput()
    {
        WriteHeaders("(option) == CURLOPT_URL\n", "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 2),\n", string.Empty);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();

        int exitCode = await UpstreamTest745Script.RunAsync(Invoke([root], standardOutput, standardError), _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("OK\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public void RootOf_NoWords_IsTheCurrentFolder() => Assert.AreEqual(".", UpstreamTest745Script.RootOf([]));

    [TestMethod]
    public void RootOf_EmptyFirstWord_IsTheCurrentFolder() => Assert.AreEqual(".", UpstreamTest745Script.RootOf([string.Empty]));

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest745Script.RunAsync(null!, _ => Task.FromResult(0)));

    private static UpstreamCurlInvocation Invoke(string[] arguments, Stream standardOutput, Stream standardError) =>
        new(arguments, standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

    private void WriteHeaders(string typecheck, string curl, string multi)
    {
        string folder = Path.Combine(root, "include", "curl");
        File.WriteAllText(Path.Combine(folder, "typecheck-gcc.h"), typecheck + AddedOptionChecks);
        File.WriteAllText(Path.Combine(folder, "curl.h"), curl);
        File.WriteAllText(Path.Combine(folder, "multi.h"), multi);
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
