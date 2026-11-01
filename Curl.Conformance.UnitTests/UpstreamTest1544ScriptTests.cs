using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1544Script"/>: upstream's <c>tests/test1544.pl</c> over the header's string options and the
/// OS/400 wrapper's translated ones, how the header's comments, continuations and folds are read, and every kind of
/// difference it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1544ScriptTests
{
    private string root = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1544-{Guid.NewGuid():N}")).FullName;
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_VendoredUpstreamFiles_FindsNothingToReport()
    {
        string vendored = Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original");

        Assert.AreEqual(("", 0), UpstreamTest1544Script.Check(vendored));
    }

    [TestMethod]
    public void Check_CommentsContinuationsAndFolds_ReadsTheStringOptionsAsPerlDoes()
    {
        WriteHeader(
            "#define X 1\n"
            + "\n"
            + "/* CURLOPT(CURLOPT_IN_COMMENT, CURLOPTTYPE_STRINGPOINT, 1), */\n"
            + "/* a multi-line\n"
            + "   comment CURLOPT(CURLOPT_STILL_COMMENT, CURLOPTTYPE_STRINGPOINT, 2), \n"
            + "   end */ CURLOPT(CURLOPT_AFTER_COMMENT, CURLOPTTYPE_STRINGPOINT, 3),\n"
            + "/* before */ CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 4), /* trailing */\n"
            + "CURLOPT(CURLOPT_PROXY, \\\n"
            + "        CURLOPTTYPE_STRINGPOINT, 5),\n"
            + "CURLOPTDEPRECATED(CURLOPT_OLD,\n"
            + "                  CURLOPTTYPE_STRINGPOINT, 6, 7.1, \"gone\"),\n"
            + "CURLOPT(CURLOPT_NUMBER, CURLOPTTYPE_LONG, 7),\n"
            + "CURLOPT(CURLOPT_UNFINISHED, CURLOPTTYPE_STRINGPOINT, 8) /* open\n"
            + "   comment */\n"
            + "CURLOPT(CURLOPT_AFTER_OPEN, CURLOPTTYPE_STRINGPOINT, 9),\n");
        WriteWrapper(
            "case CURLOPT_NOT_ARMORED:\n"
            + "/* BEGIN TRANSLATABLE STRING OPTIONS */\n"
            + "case CURLOPT_AFTER_COMMENT:\n"
            + "case CURLOPT_OLD:\n"
            + "  not a label\n"
            + "case CURLOPT_PROXY:\n"
            + "case CURLOPT_URL:\n"
            + "/* END TRANSLATABLE STRING OPTIONS */\n"
            + "case CURLOPT_AFTER_ARMOR:\n");

        (string output, int errors) = UpstreamTest1544Script.Check(root);

        Assert.AreEqual("CURLOPT_UNFINISHED is not translated\n", output);
        Assert.AreEqual(1, errors);
    }

    [TestMethod]
    public void Check_HashLineAndOptionLineWithNoFoldPending_DropsTheHashLineAndFindsTheOptionAsWritten()
    {
        WriteHeader(
            "# CURLOPT(CURLOPT_IN_HASH_LINE, CURLOPTTYPE_STRINGPOINT, 1),\n"
            + "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 2),\n");
        WriteWrapper("/* BEGIN TRANSLATABLE STRING OPTIONS */\ncase CURLOPT_URL:\n/* END TRANSLATABLE STRING OPTIONS */\n");

        (string output, int errors) = UpstreamTest1544Script.Check(root);

        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(0, errors);
    }

    [TestMethod]
    public void Check_HashLineRightAfterAContinuedLine_JoinsTheOptionAndStillDropsTheHashLine()
    {
        WriteHeader(
            "CURLOPT(CURLOPT_PROXY, \\\n"
            + "        CURLOPTTYPE_STRINGPOINT, 1),\n"
            + "# CURLOPT(CURLOPT_IN_HASH_LINE, CURLOPTTYPE_STRINGPOINT, 2),\n");
        WriteWrapper("/* BEGIN TRANSLATABLE STRING OPTIONS */\ncase CURLOPT_PROXY:\n/* END TRANSLATABLE STRING OPTIONS */\n");

        (string output, int errors) = UpstreamTest1544Script.Check(root);

        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(0, errors);
    }

    [TestMethod]
    public void Check_HeaderAndWrapperAgree_ReportsNothing()
    {
        WriteHeader("CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 4),\n");
        WriteWrapper("/* BEGIN TRANSLATABLE STRING OPTIONS */\ncase CURLOPT_URL:\n/* END TRANSLATABLE STRING OPTIONS */\n");

        Assert.AreEqual(("", 0), UpstreamTest1544Script.Check(root));
    }

    [TestMethod]
    public void Check_EveryKindOfDifference_WritesEachInSortedOrder()
    {
        WriteHeader(
            "CURLOPT(CURLOPT_B, CURLOPTTYPE_STRINGPOINT, 1),\n"
            + "CURLOPT(CURLOPT_A, CURLOPTTYPE_STRINGPOINT, 2),\n"
            + "CURLOPT(CURLOPT_KEPT, CURLOPTTYPE_STRINGPOINT, 3),\n");
        WriteWrapper(
            "/* BEGIN TRANSLATABLE STRING OPTIONS */\n"
            + "case CURLOPT_Z:\n"
            + "case CURLOPT_KEPT:\n"
            + "case CURLOPT_GHOST:\n"
            + "case CURLOPT_LAST_IS_NOT_CHECKED:\n"
            + "/* END TRANSLATABLE STRING OPTIONS */\n");

        (string output, int errors) = UpstreamTest1544Script.Check(root);

        Assert.AreEqual(
            "CURLOPT_A is not translated\nCURLOPT_B is not translated\n"
            + "translated option CURLOPT_GHOST does not exist\ntranslated option CURLOPT_LAST_IS_NOT_CHECKED does not exist\ntranslated option CURLOPT_Z does not exist\n"
            + "Translated string options are not sorted (CURLOPT_Z/CURLOPT_KEPT)\n",
            output);
        Assert.AreEqual(6, errors);
    }

    [TestMethod]
    public void Check_OnlyTheLastTranslatedOptionOutOfOrder_IsNotReported()
    {
        WriteHeader("CURLOPT(CURLOPT_B, CURLOPTTYPE_STRINGPOINT, 1),\nCURLOPT(CURLOPT_A, CURLOPTTYPE_STRINGPOINT, 2),\n");
        WriteWrapper("/* BEGIN TRANSLATABLE STRING OPTIONS */\ncase CURLOPT_B:\ncase CURLOPT_A:\n/* END TRANSLATABLE STRING OPTIONS */\n");

        Assert.AreEqual(("", 0), UpstreamTest1544Script.Check(root));
    }

    [TestMethod]
    public async Task RunAsync_ABrokenWrapper_WritesTheReportAndExitsOne()
    {
        WriteHeader("CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 4),\n");
        WriteWrapper(string.Empty);
        using MemoryStream standardOutput = new();

        int exitCode = await Run([root], standardOutput);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("CURLOPT_URL is not translated\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ConsistentFiles_WritesNothingAndExitsZero()
    {
        WriteHeader("CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 4),\n");
        WriteWrapper("/* BEGIN TRANSLATABLE STRING OPTIONS */\ncase CURLOPT_URL:\n");
        using MemoryStream standardOutput = new();

        int exitCode = await Run([root], standardOutput);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task RunAsync_NoRootWord_ReadsTheCurrentFolder()
    {
        string previous = Environment.CurrentDirectory;
        using MemoryStream standardOutput = new();
        WriteHeader("CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 4),\n");
        WriteWrapper(string.Empty);

        Environment.CurrentDirectory = root;
        try
        {
            Assert.AreEqual(1, await Run([], standardOutput));
        }
        finally
        {
            Environment.CurrentDirectory = previous;
        }

        Assert.AreEqual("CURLOPT_URL is not translated\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1544Script.RunAsync(null!, _ => Task.FromResult(0)));

    private static async Task<int> Run(IReadOnlyList<string> arguments, MemoryStream standardOutput)
    {
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new(arguments, standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        return await UpstreamTest1544Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));
    }

    private void WriteHeader(string text) => Write("include/curl/curl.h", text);

    private void WriteWrapper(string text) => Write("projects/OS400/ccsidcurl.c", text);

    private void Write(string relativePath, string text)
    {
        string path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
