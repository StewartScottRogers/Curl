using System.Text;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Arguments;
using Curl.Extension.Composition;

namespace Curl.Extension.HttpFile;

/// <summary>
/// Checks <c>--http-file</c> and <c>--http-env</c> end to end against the Visual Studio .http file format's sections,
/// from recorded file text to curl arguments, and the extension's registration in <c>Curl.Extension.Composition</c>.
/// </summary>
[TestClass]
public sealed class HttpFileArgumentExpanderTests
{
    private const string RequestsFile = """
        @hostname = localhost
        @port = 44320
        @host = {{hostname}}:{{port}}

        # Get the list of items
        GET https://{{host}}/api/items HTTP/1.1
        Accept: application/json

        ###

        POST https://{{host}}/api/items
        Content-Type: application/json

        {
          "name": "{{$processEnv ITEM_NAME}}"
        }

        """;

    private const string EnvironmentFile = """
        {
          "$shared": { "token": "shared-token", "hostname": "shared.example" },
          "dev": { "hostname": "dev.example", "port": 8080 }
        }
        """;

    [TestMethod]
    public void ArgumentsFor_RequestsSeparatedByThreeHashes_GivesOneNextGroupEach()
    {
        IReadOnlyList<string> arguments = new HttpFileArgumentExpander().ArgumentsFor(Claimed("dir/items.http"), ContextOf(Files(("dir/items.http", RequestsFile)), ("ITEM_NAME", "widget")));

        CollectionAssert.AreEqual(
            new[]
            {
                "https://localhost:44320/api/items", "-H", "Accept: application/json",
                "--next",
                "https://localhost:44320/api/items", "-H", "Content-Type: application/json", "--data-raw", "{\n  \"name\": \"widget\"\n}",
            },
            arguments.ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_NamedEnvironment_ItsVariablesAndSharedOnesAreUsed_AndFileVariablesWin()
    {
        const string Text = """
            @path = items
            GET https://{{hostname}}:{{port}}/{{path}}
            Authorization: Bearer {{token}}
            """;

        IReadOnlyList<string> arguments = new HttpFileArgumentExpander().ArgumentsFor(
            Claimed("dir/items.http", "dev"),
            ContextOf(Files(("dir/items.http", Text), ("dir/http-client.env.json", EnvironmentFile))));

        CollectionAssert.AreEqual(new[] { "https://dev.example:8080/items", "-H", "Authorization: Bearer shared-token" }, arguments.ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_FileVariableNamedAsAnEnvironmentVariable_ReplacesIt()
    {
        IReadOnlyList<string> arguments = new HttpFileArgumentExpander().ArgumentsFor(
            Claimed("items.http", "dev"),
            ContextOf(Files(("items.http", "@hostname = file.example\nGET http://{{hostname}}/"), ("http-client.env.json", EnvironmentFile))));

        CollectionAssert.AreEqual(new[] { "http://file.example/" }, arguments.ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_FileWithAByteOrderMark_ReadsTheRequestLine()
    {
        IReadOnlyList<string> arguments = new HttpFileArgumentExpander().ArgumentsFor(Claimed("a.http"), ContextOf(new Dictionary<string, byte[]> { ["a.http"] = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("GET http://h/")] }));

        CollectionAssert.AreEqual(new[] { "http://h/" }, arguments.ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_UnreadableFile_FailsWithReadError()
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => new HttpFileArgumentExpander().ArgumentsFor(Claimed("missing.http"), ContextOf(Files())));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual("http-file: cannot read missing.http", failure.Message);
    }

    [TestMethod]
    public void ArgumentsFor_FileWithOnlyCommentsAndVariables_FailsWithExit2()
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => new HttpFileArgumentExpander().ArgumentsFor(Claimed("a.http"), ContextOf(Files(("a.http", "# nothing\n@a = 1\n")))));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("http-file: no request in a.http", failure.Message);
    }

    [TestMethod]
    public void ArgumentsForValue_WithoutCompanions_ExpandsTheFileAlone()
    {
        IArgumentExpander expander = new HttpFileArgumentExpander();

        IReadOnlyList<string> arguments = expander.ArgumentsFor("a.http", ContextOf(Files(("a.http", "DELETE http://h/1"))));

        CollectionAssert.AreEqual(new[] { "-X", "DELETE", "http://h/1" }, arguments.ToArray());
    }

    [TestMethod]
    public void Expand_CommandLineWithHttpFileAndHttpEnv_ReplacesBothWithTheRequests()
    {
        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(
            ["-s", "--http-file", "dir/a.http", "--http-env", "dev"],
            ExtensionArgumentExpanderCatalogue.Entries,
            ContextOf(Files(("dir/a.http", "GET http://{{hostname}}/"), ("dir/http-client.env.json", EnvironmentFile))));

        CollectionAssert.AreEqual(new[] { "-s", "http://dev.example/" }, expanded.ToArray());
    }

    [TestMethod]
    public void Expand_CommandLineWithoutTheOption_IsUnchanged()
    {
        string[] arguments = ["-s", "-H", "X: 1", "http://h/"];

        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(arguments, ExtensionArgumentExpanderCatalogue.Entries, ContextOf(Files()));

        CollectionAssert.AreEqual(arguments, expanded.ToArray());
    }

    [TestMethod]
    public void ClaimsPositionalArgument_AnyArgument_LeavesItAUrl() =>
        Assert.IsFalse(new HttpFileArgumentExpander().ClaimsPositionalArgument("http://h/"));

    [TestMethod]
    public void Catalogues_ListTheExpanderAndBothOptions()
    {
        Assert.IsTrue(ExtensionArgumentExpanderCatalogue.Entries.Any(expander => expander is HttpFileArgumentExpander));
        Assert.AreSame(HttpFileOptions.File, new HttpFileArgumentExpander().Option);
        CollectionAssert.AreEqual(new[] { HttpFileOptions.Environment }, new HttpFileArgumentExpander().CompanionOptions.ToArray());
    }

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOff_LeaveBothOptionsOut_SoTheParserRefusesThemAsUnknown() =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name is "http-file" or "http-env"));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOn_HoldBothOptionsWithAValue()
    {
        IReadOnlyList<ExtensionOption> options = ExtensionCommandLineOptions.For(_ => null, _ => false);

        Assert.AreEqual(ExtensionOptionArgument.Value, options.Single(option => option.Name == "http-file").Argument);
        Assert.AreEqual(ExtensionOptionArgument.Value, options.Single(option => option.Name == "http-env").Argument);
    }

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOptions()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("http-file", StringComparison.Ordinal)));
        Assert.Contains("--http-file", ExtensionListing.Markdown(_ => null));
        Assert.Contains("--http-env", ExtensionListing.Markdown(_ => null));
        Assert.AreEqual(new Uri("https://learn.microsoft.com/aspnet/core/test/http-files"), HttpFileOptions.Description.Specification);
    }

    private static ClaimedArguments Claimed(string file, params string[] environments) =>
        new(file, [.. environments.Select(environment => new CompanionOptionValue(HttpFileOptions.Environment, environment))], []);

    private static Dictionary<string, byte[]> Files(params (string Path, string Text)[] files) =>
        files.ToDictionary(file => file.Path, file => Encoding.UTF8.GetBytes(file.Text.ReplaceLineEndings("\n")), StringComparer.Ordinal);

    private static ArgumentExpansionContext ContextOf(Dictionary<string, byte[]> files, params (string Name, string Value)[] environment) =>
        new(name => environment.FirstOrDefault(variable => variable.Name == name).Value, path => files.GetValueOrDefault(path));
}
