using System.Text;
using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.HttpFile;

/// <summary>Checks <see cref="HttpFileEnvironment" /> against the Visual Studio .http file format's environment files.</summary>
[TestClass]
public sealed class HttpFileEnvironmentTests
{
    [TestMethod]
    [DataRow("dir/sub/a.http", "dir/sub/http-client.env.json")]
    [DataRow(@"dir\a.http", @"dir\http-client.env.json")]
    [DataRow("a.http", "http-client.env.json")]
    public void PathBeside_HttpFilePath_KeepsItsDirectory(string httpFile, string expected) =>
        Assert.AreEqual(expected, HttpFileEnvironment.PathBeside(httpFile));

    [TestMethod]
    public void Load_EnvironmentWithoutShared_SetsItsVariables()
    {
        HttpFileVariables variables = new(_ => null);

        HttpFileEnvironment.Load("e.json", "prod", ContextOf("""{ "prod": { "host": "p", "secure": true } }"""), variables);

        Assert.AreEqual("p true", variables.Substitute("{{host}} {{secure}}"));
    }

    [TestMethod]
    public void Load_UnreadableFile_FailsWithReadError()
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => HttpFileEnvironment.Load("e.json", "dev", ContextOf(null), new HttpFileVariables(_ => null)));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual("http-file: cannot read environment file e.json", failure.Message);
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("""{ "$shared": 1, "dev": {} }""")]
    [DataRow("""{ "dev": "x" }""")]
    public void Load_FileThatIsNotAnObjectOfEnvironments_FailsWithExit2(string json)
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => HttpFileEnvironment.Load("e.json", "dev", ContextOf(json), new HttpFileVariables(_ => null)));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("http-file: e.json is not a JSON object of environments", failure.Message);
    }

    [TestMethod]
    public void Load_EnvironmentNotInTheFile_FailsWithExit2()
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => HttpFileEnvironment.Load("e.json", "dev", ContextOf("""{ "prod": {} }"""), new HttpFileVariables(_ => null)));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("http-file: environment 'dev' is not in e.json", failure.Message);
    }

    private static ArgumentExpansionContext ContextOf(string? json) =>
        new(_ => null, _ => json is null ? null : Encoding.UTF8.GetBytes(json));
}
