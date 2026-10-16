namespace Curl.Mcp;

/// <summary>
/// Tests for <see cref="McpArgumentChecker" />. Each row's arguments are joined with <c>|</c>;
/// <c>{in}</c> and <c>{out}</c> stand for a folder inside and outside the root, <c>{urlin}</c> and
/// <c>{urlout}</c> for the same folders as the path of a <c>file://</c> URL.
/// </summary>
[TestClass]
public sealed class McpArgumentCheckerTests
{
    private string workspace = string.Empty;

    private string root = string.Empty;

    private string outside = string.Empty;

    private McpArgumentChecker checker = null!;

    [TestInitialize]
    public void CreateFolders()
    {
        workspace = Path.Combine(Path.GetTempPath(), "curl-mcp-args-" + Guid.NewGuid().ToString("N"));
        root = Path.Combine(workspace, "root");
        outside = Path.Combine(workspace, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        checker = new McpArgumentChecker(new McpConfinementRoot(root));
    }

    [TestCleanup]
    public void DeleteFolders() => Directory.Delete(workspace, recursive: true);

    [TestMethod]
    // None: flags and values that name no path.
    [DataRow("-s|-H|X: y|http://h/", "")]
    [DataRow("--user-agent|{out}/x|http://h/", "")]
    [DataRow("--silent=x|http://h/", "")]
    [DataRow("-N|http://h/", "")]
    // ReadsFile.
    [DataRow("--cacert|{out}/ca.pem|https://h/", "--cacert")]
    [DataRow("--cacert|{in}/ca.pem|https://h/", "")]
    [DataRow("-n|http://h/", "-n")]
    [DataRow("--netrc-optional|http://h/", "--netrc-optional")]
    [DataRow("-T|{out}/u|http://h/", "-T")]
    [DataRow("-T|{in}/u|http://h/", "")]
    [DataRow("-T|-|http://h/", "")]
    [DataRow("-T|.|http://h/", "")]
    [DataRow("-T|{in}/{a,b}|http://h/", "-T")]
    // WritesFile.
    [DataRow("-o|{out}/x|http://h/", "-o")]
    [DataRow("-o|-|http://h/", "")]
    [DataRow("-o|{in}/#1.txt|http://h/{a,b}", "-o")]
    [DataRow("-o|{in}/a#b|http://h/", "")]
    [DataRow("-o|{in}/a#|http://h/", "")]
    [DataRow("-O|http://h/x", "")]
    [DataRow("-D|{out}/h|http://h/", "-D")]
    // WritesDirectory.
    [DataRow("--output-dir|{out}|-O|http://h/x", "--output-dir")]
    [DataRow("--output-dir|{in}|-O|http://h/x", "")]
    // ReadsOrWritesFile.
    [DataRow("--hsts|{out}/h.txt|http://h/", "--hsts")]
    // ReadsFileOrLiteral.
    [DataRow("-b|{out}/c.txt|http://h/", "-b")]
    [DataRow("-b|a=b|http://h/", "")]
    [DataRow("-b|-|http://h/", "")]
    [DataRow("--pinnedpubkey|sha256//abc=|https://h/", "")]
    [DataRow("--pinnedpubkey|{out}/k.pem|https://h/", "--pinnedpubkey")]
    [DataRow("-z|{out}/f|http://h/", "-z")]
    [DataRow("-z|-{out}/f|http://h/", "-z")]
    [DataRow("-z|20200101|http://h/", "")]
    [DataRow("-z||http://h/", "")]
    // UnixSocketPath.
    [DataRow("--unix-socket|{out}/s.sock|http://h/", "--unix-socket")]
    [DataRow("--unix-socket|{in}/s.sock|http://h/", "")]
    [DataRow("--abstract-unix-socket|name|http://h/", "--abstract-unix-socket")]
    public void Check_EveryClassificationKind_RefusesTheOptionNamingAnOutsidePath(string arguments, string refusedOption) =>
        AssertChecks(arguments, refusedOption);

    [TestMethod]
    [DataRow("-sSo|{out}/x|http://h/", "-o")]
    [DataRow("-sSo{out}/x|http://h/", "-o")]
    [DataRow("-sSo{in}/x|http://h/", "")]
    [DataRow("--output={out}/x|http://h/", "--output")]
    [DataRow("-:o|{out}/x", "")]
    [DataRow("http://h/|--next|-o|{out}/x|http://h/", "-o")]
    [DataRow("http://h/|-:|-o|{out}/x", "-o")]
    [DataRow("--|-o|{out}/x", "")]
    [DataRow("--|file://{urlout}/x", "URL")]
    [DataRow("-o|{in}/a|-o|{out}/b|http://h/", "-o")]
    [DataRow("--bogus|x", "--bogus")]
    [DataRow("-5|x", "-5")]
    [DataRow("-s5|x", "-5")]
    [DataRow("--no-progress-meter|http://h/", "")]
    [DataRow("--no-output|{out}/x", "")]
    [DataRow("--no-bogus", "--no-bogus")]
    [DataRow("--expand-output|{{v}}|http://h/", "--expand-output")]
    [DataRow("--expand-output|{in}/x|http://h/", "")]
    [DataRow("--expand-output={out}/x|http://h/", "--expand-output")]
    [DataRow("--expand-user-agent|{{v}}|http://h/", "")]
    [DataRow("--expand-silent|http://h/", "")]
    [DataRow("--expand-proto-default|{{v}}|x", "--expand-proto-default")]
    [DataRow("--expand-bogus|x", "--expand-bogus")]
    [DataRow("--proto-default|FILE|x", "--proto-default")]
    [DataRow("--proto-default|https|x", "")]
    [DataRow("-h|-o|{out}/x", "")]
    [DataRow("--help|-o", "")]
    [DataRow("--help=all|-o|{out}/x", "-o")]
    [DataRow("-hv|-o|{out}/x", "-o")]
    [DataRow("-sh|all", "")]
    [DataRow("http://h/|-o", "")]
    [DataRow("http://h/|--output", "")]
    [DataRow("-", "")]
    public void Check_CommandLineShapes_ReadTheArgumentsAsCurlDoes(string arguments, string refusedOption) =>
        AssertChecks(arguments, refusedOption);

    [TestMethod]
    [DataRow("-d|@{out}/f|http://h/", "-d")]
    [DataRow("-d|@{in}/f|http://h/", "")]
    [DataRow("-d|@-|http://h/", "")]
    [DataRow("-d|plain|http://h/", "")]
    [DataRow("--data-binary|@{out}/f|http://h/", "--data-binary")]
    [DataRow("--json|@{out}/f|http://h/", "--json")]
    [DataRow("-F|name=@{out}/f|http://h/", "-F")]
    [DataRow("-F|name=<{out}/f|http://h/", "-F")]
    [DataRow("-F|@{out}/f|http://h/", "-F")]
    [DataRow("-F|name=@{in}/f;type=text/plain|http://h/", "")]
    [DataRow("-F|name=@{out}/f;type=text/plain|http://h/", "-F")]
    [DataRow("-F|name=@\"{in}/f;x\";type=a|http://h/", "")]
    [DataRow("-F|name=@\"{in}/a\\xb\"|http://h/", "")]
    [DataRow("-F|name=@\"{out}/a\\|http://h/", "-F")]
    [DataRow("-F|name=@{in}/a,{out}/b|http://h/", "-F")]
    [DataRow("-F|name=value;headers=@{out}/h|http://h/", "-F")]
    [DataRow("-F|name=value;headers=<{in}/h;headers=<{out}/h|http://h/", "-F")]
    [DataRow("-F|name=value|http://h/", "")]
    [DataRow("-F|name=@-|http://h/", "")]
    [DataRow("--data-urlencode|name@{out}/f|http://h/", "--data-urlencode")]
    [DataRow("--data-urlencode|@{out}/f|http://h/", "--data-urlencode")]
    [DataRow("--data-urlencode|name=@{out}/f|http://h/", "")]
    [DataRow("--data-urlencode|plain|http://h/", "")]
    [DataRow("--url-query|+name@{out}/f|http://h/", "--url-query")]
    [DataRow("--url-query|a=b|http://h/", "")]
    [DataRow("--variable|name@{out}/f|http://h/", "--variable")]
    [DataRow("--variable|%HOME|http://h/", "")]
    [DataRow("--variable|%HOME@{out}/f|http://h/", "--variable")]
    [DataRow("--variable|n=v|http://h/", "")]
    [DataRow("-H|@{out}/h|http://h/", "-H")]
    [DataRow("-H|X: @{out}|http://h/", "")]
    [DataRow("--proxy-header|@{out}/h|http://h/", "--proxy-header")]
    [DataRow("-w|@{out}/t|http://h/", "-w")]
    [DataRow("-w|@{in}/t|http://h/", "")]
    [DataRow("-w|@-|http://h/", "")]
    [DataRow("-w|%output{{out}/w}%{http_code}|http://h/", "-w")]
    [DataRow("-w|%output{>>{out}/w}|http://h/", "-w")]
    [DataRow("-w|%output{{in}/w}x|http://h/", "")]
    [DataRow("-w|%%output{{out}/w}|http://h/", "")]
    [DataRow("-w|%output{{out}/w|http://h/", "-w")]
    [DataRow("-w|100%|http://h/", "")]
    [DataRow("-w|%{http_code}|http://h/", "")]
    [DataRow("-x|socks5h://localhost{urlout}/s.sock|http://h/", "-x")]
    [DataRow("-x|socks5h://localhost{urlin}/s.sock|http://h/", "")]
    [DataRow("-x|socks5h://user:pw@LOCALHOST:1080{urlout}/s|http://h/", "-x")]
    [DataRow("-x|http://proxy:8080|http://h/", "")]
    [DataRow("-x|http://proxy:8080/|http://h/", "")]
    [DataRow("--socks5|localhost{urlout}/s|http://h/", "--socks5")]
    [DataRow("--preproxy|socks5://other{urlout}/s|http://h/", "")]
    public void Check_PathsEmbeddedInValues_AreFoundAsCurlReadsThem(string arguments, string refusedOption) =>
        AssertChecks(arguments, refusedOption);

    [TestMethod]
    [DataRow("file://{urlin}/x", "")]
    [DataRow("file://{urlout}/x", "URL")]
    [DataRow("file://localhost{urlin}/x", "")]
    [DataRow("file://localhost{urlout}/x", "URL")]
    [DataRow("FILE://127.0.0.1{urlout}/x", "URL")]
    [DataRow("file://otherhost/share/x", "URL")]
    [DataRow("file://otherhost", "URL")]
    [DataRow("file:{urlout}/x", "URL")]
    [DataRow("file:x", "")]
    [DataRow("file:/a", "URL")]
    [DataRow("file://{urlin}/%2e%2e/%2e%2e/x", "URL")]
    [DataRow("file://{urlin}/x?q=1#frag", "")]
    [DataRow("file://{urlin}/{a,b}", "URL")]
    [DataRow("--url|file://{urlout}/x", "--url")]
    [DataRow("--url|file://{urlin}/x", "")]
    [DataRow("-T|{in}/u|file://{urlout}/x", "URL")]
    [DataRow("-T|{in}/u|file://{urlin}/", "")]
    [DataRow("http://h/|--next|file://{urlout}/x", "URL")]
    public void Check_FileUrls_AreCheckedAsPaths(string arguments, string refusedOption) =>
        AssertChecks(arguments, refusedOption);

    [TestMethod]
    public void Check_RepeatedOption_NamesTheFirstOutsidePath()
    {
        McpArgumentCheck check = checker.Check(Arguments("-o|{in}/a|-o|{out}/b|--cacert|{out}/c|http://h/"));

        Assert.AreEqual("-o", check.Option);
        Assert.AreEqual(Expand("{out}/b"), check.Path);
        StringAssert.StartsWith(check.Reason, "-o '");
        StringAssert.Contains(check.Reason, "outside CURL_MCP_ROOT");
    }

    [TestMethod]
    public void Check_AllowedArguments_AnswerAllowed()
    {
        McpArgumentCheck check = checker.Check(Arguments("-sS|-o|{in}/a|http://h/"));

        Assert.AreSame(McpArgumentCheck.Allowed, check);
        Assert.IsTrue(check.IsAllowed);
        Assert.IsNull(check.Option);
        Assert.IsNull(check.Path);
    }

    [TestMethod]
    public void Check_UncheckableValue_SaysWhy()
    {
        McpArgumentCheck check = checker.Check(Arguments("-T|{in}/{a,b}|http://h/"));

        Assert.AreEqual(Expand("{in}/{a,b}"), check.Path);
        StringAssert.Contains(check.Reason, "glob characters");
    }

    [TestMethod]
    public void Check_NullElement_ReadsAsAnEmptyArgument()
    {
        Assert.IsTrue(checker.Check([null!, "http://h/"]).IsAllowed);
        Assert.AreEqual("-K", checker.Check(["-K", null!]).Option);
    }

    [TestMethod]
    public void Check_NoRootConfigured_RefusesEveryPath()
    {
        McpArgumentChecker unconfigured = new(McpConfinementRoot.NotConfigured);

        McpArgumentCheck refused = unconfigured.Check(["-o", "x", "http://h/"]);

        Assert.AreEqual("-o", refused.Option);
        StringAssert.Contains(refused.Reason, "CURL_MCP_ROOT is not set");
        Assert.IsTrue(unconfigured.Check(["-s", "http://h/"]).IsAllowed);
    }

    [TestMethod]
    public void ConstructorAndCheck_Null_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpArgumentChecker(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => checker.Check(null!));
    }

    [TestMethod]
    [DataRow("output = \"{out}/x\"", "--output")]
    [DataRow("output:\"{out}/x\"", "--output")]
    [DataRow("--output \"{out}/x\"", "--output")]
    [DataRow("-o \"{out}/x\"", "-o")]
    [DataRow("-so \"{in}/x\"", "")]
    [DataRow("user-agent \"a\\tb\\nc\\rd\\ve\\qf\"", "")]
    [DataRow("output \"{out}/a\\", "--output")]
    [DataRow("output \"{in}/x\" # comment", "")]
    [DataRow("user-agent agent # comment\nuser-agent agent", "")]
    [DataRow("# comment\r\n\r\n   \t\r\nsilent\r\nurl = \"file://{urlin}/x\"", "")]
    [DataRow("silent\nurl = \"file://{urlout}/x\"", "--url")]
    [DataRow("output \"\"", "")]
    [DataRow("output \"{in}/x", "")]
    [DataRow("bogus", "--bogus")]
    [DataRow("-5", "-5")]
    public void Check_ConfigFileInsideTheRoot_ChecksItsLines(string content, string refusedOption)
    {
        string config = Path.Combine(root, "c.txt");
        File.WriteAllText(config, Expand(content));

        McpArgumentCheck check = checker.Check(["-K", config, "http://h/"]);

        Assert.AreEqual(refusedOption, check.Option ?? string.Empty);
        if (!check.IsAllowed)
        {
            StringAssert.StartsWith(check.Reason, $"In config file '{config}', line ");
        }
    }

    [TestMethod]
    public void Check_ConfigThatIncludesItself_StopsAtTheDepthCap()
    {
        string config = Path.Combine(root, "self.txt");
        File.WriteAllText(config, $"config = \"{ForwardSlashes(config)}\"");

        McpArgumentCheck check = checker.Check(["--config", config]);

        Assert.AreEqual("--config", check.Option);
        StringAssert.Contains(check.Reason, $"more than {McpArgumentChecker.MaximumConfigDepth} deep");
    }

    [TestMethod]
    public void Check_ConfigOutsideUnreadableOrStandardInput_IsRefused()
    {
        string outsideConfig = Path.Combine(outside, "c.txt");
        File.WriteAllText(outsideConfig, "silent");

        McpArgumentCheck outsideCheck = checker.Check(["-K", outsideConfig]);
        McpArgumentCheck missing = checker.Check(["-K", Path.Combine(root, "missing.txt")]);
        McpArgumentCheck folder = checker.Check(["-K", root]);
        McpArgumentCheck standardInput = checker.Check(["-K", "-"]);

        Assert.AreEqual("-K", outsideCheck.Option);
        StringAssert.Contains(outsideCheck.Reason, "outside CURL_MCP_ROOT");
        StringAssert.Contains(missing.Reason, "cannot be read");
        StringAssert.Contains(folder.Reason, "cannot be read");
        StringAssert.Contains(standardInput.Reason, "standard input");
    }

    [TestMethod]
    public void Check_WithoutConfigOption_ReadsNoFileAndWritesNone()
    {
        string unreadable = Path.Combine(root, "data.txt");

        McpArgumentCheck check = checker.Check(["-d", "@" + unreadable, "-o", Path.Combine(root, "out.txt"), "http://h/"]);

        Assert.IsTrue(check.IsAllowed);
        Assert.IsEmpty(Directory.GetFileSystemEntries(root));
    }

    private void AssertChecks(string arguments, string refusedOption)
    {
        McpArgumentCheck check = checker.Check(Arguments(arguments));

        Assert.AreEqual(refusedOption, check.Option ?? string.Empty, check.Reason);
        Assert.AreEqual(refusedOption.Length == 0, check.IsAllowed);
    }

    private string[] Arguments(string joined) => [.. joined.Split('|').Select(Expand)];

    /// <summary>Expands the placeholders with forward slashes, which every platform reads, as a quoted value unescapes backslashes.</summary>
    private string Expand(string text) => text
        .Replace("{urlin}", UrlPath(root), StringComparison.Ordinal)
        .Replace("{urlout}", UrlPath(outside), StringComparison.Ordinal)
        .Replace("{in}", ForwardSlashes(root), StringComparison.Ordinal)
        .Replace("{out}", ForwardSlashes(outside), StringComparison.Ordinal);

    private static string UrlPath(string folder) => "/" + ForwardSlashes(folder).TrimStart('/');

    private static string ForwardSlashes(string path) => path.Replace('\\', '/');
}
