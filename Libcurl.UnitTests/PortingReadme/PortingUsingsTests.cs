namespace Libcurl.UnitTests.PortingReadme;

[TestClass]
public sealed class PortingUsingsTests
{
    private static string[] NamespacesIn(string text, string prefix) =>
        text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => line[prefix.Length..].TrimEnd('`', '"', ' ', '/', '>'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    [TestMethod]
    public void ReadmePortingList_MatchesTheImplicitUsingsOfThePackage()
    {
        string baseDirectory = Path.Combine(AppContext.BaseDirectory, "PortingReadme");
        string readme = File.ReadAllText(Path.Combine(baseDirectory, "README.md"));
        string props = File.ReadAllText(Path.Combine(baseDirectory, "Libcurl.props"));

        string section = readme[readme.IndexOf("## Porting from C", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("\n## Folder map", StringComparison.Ordinal)];
        string[] listed = NamespacesIn(section.Replace("- `", "- ").Replace("`\r", "\r"), "- ");
        string[] declared = props.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("<Using Include=\"", StringComparison.Ordinal))
            .Select(line => line["<Using Include=\"".Length..line.IndexOf('"', "<Using Include=\"".Length)])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(declared, listed.Select(name => name.TrimEnd('`')).ToArray());
        Assert.AreEqual(4, declared.Length);
    }

    [TestMethod]
    public void PortedTypes_ResolveWithOnlyTheImplicitUsings()
    {
        // No using lines: the props import supplies them, as the package does for a consumer.
        Assert.AreEqual(0, (int)CurlCode.Ok);
        Assert.AreEqual(1, (int)CurlWebSocketFlags.Text);
        Assert.IsTrue(Enum.GetValues<RtspRequest>().Length > 0);
        ChunkEndCallback callback = () => default;
        Assert.AreEqual(default, callback());
    }
}
