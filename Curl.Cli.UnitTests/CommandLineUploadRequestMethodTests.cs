using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the request method a <c>-T</c> upload finds selected and the warning curl 8.21.0 prints when it
/// is another one, before it ends with exit 2 (measured with the local curl on 2026-10-07, BL-1683).
/// </summary>
[TestClass]
public sealed class CommandLineUploadRequestMethodTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string WarningStart = "Warning: You can only select one HTTP request method! You asked for both PUT (-T, --upload-file) and ";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(new[] { "-T", "f", Url }, SelectedHttpMethod.None, DisplayName = "-T alone")]
    [DataRow(new[] { "-d", "a=1", "-T", "f", Url }, SelectedHttpMethod.Post, DisplayName = "-d before -T")]
    [DataRow(new[] { "-T", "f", "-d", "a=1", Url }, SelectedHttpMethod.Post, DisplayName = "-T before -d")]
    [DataRow(new[] { "--json", "{}", "-T", "f", Url }, SelectedHttpMethod.Post, DisplayName = "--json")]
    [DataRow(new[] { "-G", "-d", "a=1", "-T", "f", Url }, SelectedHttpMethod.Get, DisplayName = "-G -d")]
    [DataRow(new[] { "--no-head", "-T", "f", Url }, SelectedHttpMethod.Get, DisplayName = "--no-head")]
    [DataRow(new[] { "-I", "-T", "f", Url }, SelectedHttpMethod.Head, DisplayName = "-I")]
    [DataRow(new[] { "-I", "-G", "-d", "a=1", "-T", "f", Url }, SelectedHttpMethod.Head, DisplayName = "-I -G -d")]
    [DataRow(new[] { "-F", "a=1", "-T", "f", Url }, SelectedHttpMethod.MultipartFormPost, DisplayName = "-F")]
    public void HttpMethodBeforeUpload_CommandLine_IsTheMethodCurlFindsSelected(string[] arguments, SelectedHttpMethod expected)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("HttpMethodBeforeUpload", expected, result.Options.HttpMethodBeforeUpload);
        Assert.AreEqual(expected, result.Options.HttpMethodBeforeUpload);
    }

    [TestMethod]
    [DataRow(SelectedHttpMethod.None, DisplayName = "None")]
    [DataRow(SelectedHttpMethod.Put, DisplayName = "Put")]
    public void PutRequestedWith_NoOtherMethod_IsNull(SelectedHttpMethod selected)
    {
        Diagnostics.Arrange("selected", selected);
        IReadOnlyList<string>? lines = CommandLineWarning.PutRequestedWith(selected);
        Diagnostics.Act("lines", lines);

        Diagnostics.Assert("lines", null, lines);
        Assert.IsNull(lines);
    }

    [TestMethod]
    [DataRow(SelectedHttpMethod.Post, "POST (-d, --data).", DisplayName = "-d / --json")]
    [DataRow(SelectedHttpMethod.Get, "GET (-G, --get).", DisplayName = "-G / --no-head")]
    [DataRow(SelectedHttpMethod.Head, "HEAD (-I, --head).", DisplayName = "-I")]
    [DataRow(SelectedHttpMethod.MultipartFormPost, "multipart formpost (-F, --form).", DisplayName = "-F")]
    public void PutRequestedWith_OtherMethod_IsCurlsExactUnwrappedLine(SelectedHttpMethod selected, string expectedEnd)
    {
        Diagnostics.Arrange("selected", selected);
        IReadOnlyList<string>? lines = CommandLineWarning.PutRequestedWith(selected);
        Diagnostics.Act("lines", lines);

        string[] expected = [WarningStart + expectedEnd];
        Diagnostics.Assert("lines", expected, lines);
        Assert.IsNotNull(lines);
        CollectionAssert.AreEqual(expected, lines.ToArray());
    }
}
