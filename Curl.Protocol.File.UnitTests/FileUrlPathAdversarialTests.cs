using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.File;

/// <summary>
/// Adversarial black-box tests of <see cref="FileUrlPath" /> (BL-1507), by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c>: escapes at and past the end of the
/// path, malformed and invalid UTF-8 escapes, schemes that are almost <c>file</c>, very
/// long and non-ASCII names, and repeated and concurrent parses. The oracle is
/// <see cref="FileUrlPath" />'s documented contract and, where the path reaches the exit 37
/// message, curl 8.21.0 (Schannel build), which quoted <c>d/%%41</c>, <c>d/a%4</c> and
/// <c>d/%FF%FE</c> exactly as written. Every URL is drive-less, so every test runs on every
/// platform.
/// </summary>
[TestClass]
public sealed class FileUrlPathAdversarialTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // Boundaries.

    [TestMethod]
    [DataRow("file:///d/a%", "/d/a%", "/d/a%")]
    [DataRow("file:///d/a%4", "/d/a%4", "/d/a%4")]
    [DataRow("file:///d/a%41", "/d/a%41", "/d/aA")]
    [DataRow("file:///%41", "/%41", "/A")]
    public void TryParse_EscapeCutShortAtTheEndOfThePath_KeepsOnlyACompleteEscapeAsAnEscape(
        string url,
        string expectedUrlPath,
        string expectedOsPath)
    {
        FileUrlPath path = Parsed(url);

        Diagnostics.Assert("url path", expectedUrlPath, path.UrlPath);
        Diagnostics.Assert("os path", expectedOsPath, Slashed(path.OsPath));
        Assert.AreEqual(expectedUrlPath, path.UrlPath);
        Assert.AreEqual(NativePath(expectedOsPath), path.OsPath);
    }

    [TestMethod]
    public void TryParse_RootOnly_IsTheRootDirectory()
    {
        FileUrlPath path = Parsed("file:///");

        Diagnostics.Assert("url path", "/", path.UrlPath);
        Assert.AreEqual("/", path.UrlPath);
        Assert.AreEqual(NativePath("/"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_PathOfSixtyFourThousandCharacters_KeepsEveryCharacter()
    {
        string name = new('a', 65536);

        FileUrlPath path = Parsed("file:///d/" + name);

        Diagnostics.Act("url path length", path.UrlPath.Length);
        Assert.AreEqual("/d/" + name, path.UrlPath);
        Assert.AreEqual(NativePath("/d/" + name), path.OsPath);
    }

    [TestMethod]
    public void TryParse_PathOfTenThousandEscapes_DecodesEveryOne()
    {
        string encoded = string.Concat(Enumerable.Repeat("%41", 10000));

        FileUrlPath path = Parsed("file:///d/" + encoded);

        Diagnostics.Act("os path length", path.OsPath.Length);
        Assert.AreEqual("/d/" + encoded, path.UrlPath);
        Assert.AreEqual(NativePath("/d/" + new string('A', 10000)), path.OsPath);
    }

    [TestMethod]
    [DataRow("file:///d/%7f", "/d/%7F", "/d/\u007F")]
    [DataRow("file:///d/%80", "/d/%80", "/d/�")]
    [DataRow("file:///d/%01", "/d/%01", "/d/\u0001")]
    public void TryParse_EscapeAtTheEdgeOfAscii_DecodesAsUtf8(
        string url,
        string expectedUrlPath,
        string expectedOsPath)
    {
        FileUrlPath path = Parsed(url);

        Diagnostics.Assert("url path", expectedUrlPath, path.UrlPath);
        Assert.AreEqual(expectedUrlPath, path.UrlPath);
        Assert.AreEqual(NativePath(expectedOsPath), path.OsPath);
    }

    // Malformed input.

    [TestMethod]
    public void TryParse_PercentBeforeAnEscape_KeepsTheFirstPercentAndDecodesTheEscape()
    {
        FileUrlPath path = Parsed("file:///d/%%41");

        Diagnostics.Assert("url path", "/d/%%41", path.UrlPath);
        Assert.AreEqual("/d/%%41", path.UrlPath);
        Assert.AreEqual(NativePath("/d/%A"), path.OsPath);
    }

    [TestMethod]
    [DataRow("file:///d/%GG", "/d/%GG")]
    [DataRow("file:///d/%g1", "/d/%g1")]
    [DataRow("file:///d/%1g", "/d/%1g")]
    [DataRow("file:///d/%-1", "/d/%-1")]
    [DataRow("file:///d/%+1", "/d/%+1")]
    public void TryParse_EscapeWithANonHexDigit_KeepsItExactlyAsWritten(string url, string expected)
    {
        FileUrlPath path = Parsed(url);

        Diagnostics.Assert("url path", expected, path.UrlPath);
        Assert.AreEqual(expected, path.UrlPath);
        Assert.AreEqual(NativePath(expected), path.OsPath);
    }

    [TestMethod]
    public void TryParse_EscapesThatAreNotUtf8_DecodeToReplacementCharacters()
    {
        FileUrlPath path = Parsed("file:///d/%FF%FE");

        Diagnostics.Assert("url path", "/d/%FF%FE", path.UrlPath);
        Assert.AreEqual("/d/%FF%FE", path.UrlPath);
        Assert.AreEqual(NativePath("/d/��"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_Utf8SequenceCutByALiteralCharacter_DecodesEachHalfOnItsOwn()
    {
        FileUrlPath path = Parsed("file:///d/%C3x%A9");

        Diagnostics.Act("os path", Slashed(path.OsPath));
        Assert.AreEqual("/d/%C3x%A9", path.UrlPath);
        Assert.AreEqual(NativePath("/d/�x�"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_Utf8SequenceSplitAcrossTwoEscapeRuns_IsNotJoined()
    {
        FileUrlPath joined = Parsed("file:///d/%C3%A9");
        FileUrlPath split = Parsed("file:///d/%C3/%A9");

        Assert.AreEqual(NativePath("/d/é"), joined.OsPath);
        Assert.AreEqual(NativePath("/d/�/�"), split.OsPath);
    }

    [TestMethod]
    public void TryParse_EncodedSlash_StaysAnEscapeInTheUrlPathAndBecomesASeparatorInTheOsPath()
    {
        // curl 8.21.0 opened d/a b.txt for file:///.../d%2Fa%20b.txt and exited 0.
        FileUrlPath path = Parsed("file:///d%2Fa%20b.txt");

        Diagnostics.Assert("url path", "/d%2Fa%20b.txt", path.UrlPath);
        Assert.AreEqual("/d%2Fa%20b.txt", path.UrlPath);
        Assert.AreEqual(NativePath("/d/a b.txt"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_EncodedDotDotAroundAnEncodedSlash_IsNotRemovedAsADotSegment()
    {
        FileUrlPath path = Parsed("file:///d/x%2F%2E%2E%2Fy");

        Diagnostics.Assert("url path", "/d/x%2F%2E%2E%2Fy", path.UrlPath);
        Assert.AreEqual("/d/x%2F%2E%2E%2Fy", path.UrlPath);
        Assert.AreEqual(NativePath("/d/x/../y"), path.OsPath);
    }

    [TestMethod]
    [DataRow("file:///d/a%2fb", "/d/a%2Fb")]
    [DataRow("file:///d/%aF%Af", "/d/%AF%AF")]
    [DataRow("file:///d/%e2%82%ac", "/d/%E2%82%AC")]
    public void TryParse_LowercaseOrMixedCaseHexDigits_AreQuotedUppercase(string url, string expected)
    {
        FileUrlPath path = Parsed(url);

        Diagnostics.Assert("url path", expected, path.UrlPath);
        Assert.AreEqual(expected, path.UrlPath);
    }

    [TestMethod]
    public void TryParse_EncodedEuroSign_DecodesToTheCharacter()
    {
        FileUrlPath path = Parsed("file:///d/%E2%82%AC");

        Assert.AreEqual(NativePath("/d/€"), path.OsPath);
    }

    // Invalid partitions.

    [TestMethod]
    [DataRow("http://localhost/d/x")]
    [DataRow("ftp://localhost/d/x")]
    [DataRow("files://localhost/d/x")]
    [DataRow("fil://localhost/d/x")]
    public void TryParse_SchemeThatIsNotFile_ReturnsFalse(string url)
    {
        bool parsed = FileUrlPath.TryParse(CurlUrl.Parse(url), out _);

        Diagnostics.Arrange("url", url);
        Diagnostics.Assert("parsed", false, parsed);
        Assert.IsFalse(parsed);
    }

    [TestMethod]
    [DataRow("File:///d/x")]
    [DataRow("fILE:///d/x")]
    public void TryParse_FileSchemeInMixedCase_IsAccepted(string url)
    {
        FileUrlPath path = Parsed(url);

        Assert.AreEqual("/d/x", path.UrlPath);
    }

    [TestMethod]
    [DataRow("file:///d/é", "/d/%C3%A9", "/d/é")]
    [DataRow("file:///d/日本", "/d/%E6%97%A5%E6%9C%AC", "/d/日本")]
    [DataRow("file:///d/\U0001F600", "/d/%F0%9F%98%80", "/d/\U0001F600")]
    public void TryParse_UnescapedNonAsciiName_QuotesItsUtf8BytesAndOpensTheCharacters(
        string url,
        string expectedUrlPath,
        string expectedOsPath)
    {
        FileUrlPath path = Parsed(url);

        Diagnostics.Assert("url path", expectedUrlPath, path.UrlPath);
        Assert.AreEqual(expectedUrlPath, path.UrlPath);
        Assert.AreEqual(NativePath(expectedOsPath), path.OsPath);
    }

    [TestMethod]
    public void TryParse_UrlPathOfANonAsciiName_IsAlwaysPureAscii()
    {
        FileUrlPath path = Parsed("file:///d/aé日\U0001F600b");

        Diagnostics.Act("url path", path.UrlPath);
        Assert.IsTrue(path.UrlPath.All(char.IsAscii), path.UrlPath);
    }

    // State and concurrency.

    [TestMethod]
    public void TryParse_SameUrlTwice_ReturnsEqualPaths()
    {
        CurlUrl url = CurlUrl.Parse("file:///d/a%20b/%C3%A9");

        Assert.IsTrue(FileUrlPath.TryParse(url, out var first));
        Assert.IsTrue(FileUrlPath.TryParse(url, out var second));

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void TryParse_ManyConcurrentCallers_AllGetTheSamePath()
    {
        CurlUrl url = CurlUrl.Parse("file:///d/%e2%82%ac/x%2Fy/%%41");
        Assert.IsTrue(FileUrlPath.TryParse(url, out var expected));

        FileUrlPath[] results = new FileUrlPath[64];
        Parallel.For(0, results.Length, index =>
        {
            Assert.IsTrue(FileUrlPath.TryParse(url, out var path));
            results[index] = path;
        });

        Diagnostics.Act("distinct results", results.Distinct().Count());
        Assert.IsTrue(results.All(result => result == expected));
    }

    [TestMethod]
    public void TryParse_SeededRandomPaths_NeverThrowAndKeepTheUrlPathAscii()
    {
        const int seed = 1507;
        var random = new Random(seed);
        // No ':' - a leading "/X:" is a drive letter, which only Windows strips.
        const string alphabet = "aZ09%/._-~!$&'()*+,;=@é日";
        Diagnostics.Arrange("seed", seed);

        for (int round = 0; round < 2000; round++)
        {
            var text = new StringBuilder("file:///");
            int length = random.Next(1, 40);

            for (int index = 0; index < length; index++)
            {
                text.Append(alphabet[random.Next(alphabet.Length)]);
            }

            if (!CurlUrl.TryParse(text.ToString(), false, out var url))
            {
                continue;
            }

            Assert.IsTrue(FileUrlPath.TryParse(url, out var path), text.ToString());
            Assert.IsTrue(path.UrlPath.All(char.IsAscii), text.ToString());
            Assert.StartsWith("/", path.UrlPath, text.ToString());
        }
    }

    private static FileUrlPath Parsed(string url)
    {
        Assert.IsTrue(FileUrlPath.TryParse(CurlUrl.Parse(url), out var path), url);
        return path;
    }

    private static string NativePath(string slashedPath) =>
        slashedPath.Replace('/', Path.DirectorySeparatorChar);

    private static string Slashed(string? osPath) =>
        (osPath ?? string.Empty).Replace(Path.DirectorySeparatorChar, '/');
}
