using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the exact two standard-error lines and the exit code of every refusal the
/// command-line parser can raise: <c>curl: option &lt;spelled&gt;: &lt;reason&gt;</c>
/// (or <c>curl: (2) no URL specified</c>) followed by the try-help line, exiting with <see cref="CurlExitCode.FailedInit"/>,
/// byte for byte as curl 8.21.0 prints them.
/// </summary>
[TestClass]
public sealed class CommandLineRefusalTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TryHelpLine_Always_IsCurlsExactText()
    {
        Diagnostics.Arrange("member", "CommandLineRefusal.TryHelpLine");
        string tryHelpLine = CommandLineRefusal.TryHelpLine;
        Diagnostics.Act("try help line", tryHelpLine);

        Diagnostics.Assert("try help line", "curl: try 'curl --help' or 'curl --manual' for more information", tryHelpLine);
        Assert.AreEqual("curl: try 'curl --help' or 'curl --manual' for more information", tryHelpLine);
    }

    [TestMethod]
    public void UnknownOption_Spelled_NamesOptionAsUnknown()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.UnknownOption("--bogus")""");
        CommandLineRefusal refusal = CommandLineRefusal.UnknownOption("--bogus");
        ActRefusal(refusal);

        AssertRefusal(refusal, "curl: option --bogus: is unknown");
    }

    [TestMethod]
    public void CannotBeReversed_Spelled_NamesOptionAsNotReversible()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.CannotBeReversed("--no-output")""");
        CommandLineRefusal refusal = CommandLineRefusal.CannotBeReversed("--no-output");
        ActRefusal(refusal);

        AssertRefusal(refusal, "curl: option --no-output: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void RequiresParameter_Spelled_NamesOptionAsRequiringParameter()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.RequiresParameter("-o")""");
        CommandLineRefusal refusal = CommandLineRefusal.RequiresParameter("-o");
        ActRefusal(refusal);

        AssertRefusal(refusal, "curl: option -o: requires parameter");
    }

    [TestMethod]
    public void BlankArgument_Spelled_NamesOptionAsBlank()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.BlankArgument("--output=")""");
        CommandLineRefusal refusal = CommandLineRefusal.BlankArgument("--output=");
        ActRefusal(refusal);

        AssertRefusal(refusal, "curl: option --output=: blank argument where content is expected");
    }

    [TestMethod]
    public void BlankArgument_EmptySpelling_LeavesNameEmpty()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.BlankArgument(string.Empty)""");
        CommandLineRefusal refusal = CommandLineRefusal.BlankArgument(string.Empty);
        ActRefusal(refusal);

        AssertRefusal(refusal, "curl: option : blank argument where content is expected");
    }

    [TestMethod]
    public void TooLargeNumber_Spelled_NamesOptionAsTooLarge()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.TooLargeNumber("--create-file-mode")""");
        CommandLineRefusal refusal = CommandLineRefusal.TooLargeNumber("--create-file-mode");
        ActRefusal(refusal);

        AssertRefusal(refusal, "curl: option --create-file-mode: too large number");
    }

    [TestMethod]
    public void ExpectedProperNumericalParameter_Spelled_NamesOptionAsNotNumerical()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.ExpectedProperNumericalParameter("--tftp-blksize")""");
        CommandLineRefusal refusal = CommandLineRefusal.ExpectedProperNumericalParameter("--tftp-blksize");
        ActRefusal(refusal);

        AssertRefusal(refusal, "curl: option --tftp-blksize: expected a proper numerical parameter");
    }

    [TestMethod]
    public void ExpectedPositiveNumericalParameter_Spelled_NamesOptionAsNotPositive()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.ExpectedPositiveNumericalParameter("--tftp-blksize")""");
        CommandLineRefusal refusal = CommandLineRefusal.ExpectedPositiveNumericalParameter("--tftp-blksize");
        ActRefusal(refusal);

        AssertRefusal(refusal, "curl: option --tftp-blksize: expected a positive numerical parameter");
    }

    [TestMethod]
    public void EmptyCommandLine_Always_IsTheTryHelpLineAloneWithExit2()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.EmptyCommandLine()""");
        CommandLineRefusal refusal = CommandLineRefusal.EmptyCommandLine();
        ActRefusal(refusal);

        WriteAssertRefusal(CurlExitCode.FailedInit, refusal, CommandLineRefusal.TryHelpLine);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { CommandLineRefusal.TryHelpLine }, refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void NoUrlSpecified_Always_ReportsNoUrlWithCurlsExitCodePrefix()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.NoUrlSpecified()""");
        CommandLineRefusal refusal = CommandLineRefusal.NoUrlSpecified();
        ActRefusal(refusal);

        AssertRefusal(refusal, "curl: (2) no URL specified");
    }

    [TestMethod]
    public void FileDoesNotExist_Spelled_NamesFileAndOptionInThreeLines()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.FileDoesNotExist("--cacert=", "--cacert", "nonexist.pem", errorsHidden: false)""");
        CommandLineRefusal refusal = CommandLineRefusal.FileDoesNotExist("--cacert=", "--cacert", "nonexist.pem", errorsHidden: false);
        ActRefusal(refusal);

        WriteAssertRefusal(CurlExitCode.FailedInit, refusal, "curl: The file 'nonexist.pem' provided to --cacert does not exist", "curl: option --cacert=: is badly used here", CommandLineRefusal.TryHelpLine);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: The file 'nonexist.pem' provided to --cacert does not exist",
                "curl: option --cacert=: is badly used here",
                CommandLineRefusal.TryHelpLine,
            },
            refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void FileDoesNotExist_ErrorsHidden_LeavesOutTheFileLine()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.FileDoesNotExist("--knownhosts", "--knownhosts", "nope", errorsHidden: true)""");
        CommandLineRefusal refusal = CommandLineRefusal.FileDoesNotExist("--knownhosts", "--knownhosts", "nope", errorsHidden: true);
        ActRefusal(refusal);

        WriteAssertRefusal(CurlExitCode.FailedInit, refusal, "curl: option --knownhosts: is badly used here", CommandLineRefusal.TryHelpLine);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --knownhosts: is badly used here", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow(null, "--cacert", "f", "spelledOption")]
    [DataRow("--cacert", null, "f", "longOption")]
    [DataRow("--cacert", "--cacert", null, "file")]
    public void FileDoesNotExist_NullArgument_ThrowsArgumentNull(string? spelledOption, string? longOption, string? file, string expectedParamName)
    {
        ArrangeNullArgumentCall(
            $"CommandLineRefusal.FileDoesNotExist({CommandLineParseDiagnostics.QuoteEach([spelledOption, longOption, file])}, errorsHidden: false)",
            expectedParamName);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineRefusal.FileDoesNotExist(spelledOption!, longOption!, file!, errorsHidden: false));
        Diagnostics.Act("exception", exception.GetType().Name);

        Diagnostics.Assert("parameter name", expectedParamName, exception.ParamName);
        Assert.AreEqual(expectedParamName, exception.ParamName);
    }

    [TestMethod]
    public void DataFileUnreadable_ErrorsShown_ExitsWithReadErrorInThreeLines()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.DataFileUnreadable("-d", string.Empty, errorsHidden: false)""");
        CommandLineRefusal refusal = CommandLineRefusal.DataFileUnreadable("-d", string.Empty, errorsHidden: false);
        ActRefusal(refusal);

        WriteAssertRefusal(CurlExitCode.ReadError, refusal, "curl: Failed to open ", "curl: option -d: error encountered when reading a file", CommandLineRefusal.TryHelpLine);
        Assert.AreEqual(CurlExitCode.ReadError, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: Failed to open ",
                "curl: option -d: error encountered when reading a file",
                CommandLineRefusal.TryHelpLine,
            },
            refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void DataFileUnreadable_ErrorsHidden_ExitsWithReadErrorInTwoLines()
    {
        Diagnostics.Arrange("call", """CommandLineRefusal.DataFileUnreadable("--data", "missing", errorsHidden: true)""");
        CommandLineRefusal refusal = CommandLineRefusal.DataFileUnreadable("--data", "missing", errorsHidden: true);
        ActRefusal(refusal);

        WriteAssertRefusal(CurlExitCode.ReadError, refusal, "curl: option --data: error encountered when reading a file", CommandLineRefusal.TryHelpLine);
        Assert.AreEqual(CurlExitCode.ReadError, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --data: error encountered when reading a file", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow(null, "f", "spelledOption")]
    [DataRow("-d", null, "file")]
    public void DataFileUnreadable_NullArgument_ThrowsArgumentNull(string? spelledOption, string? file, string expectedParamName)
    {
        ArrangeNullArgumentCall(
            $"CommandLineRefusal.DataFileUnreadable({CommandLineParseDiagnostics.QuoteEach([spelledOption, file])}, errorsHidden: false)",
            expectedParamName);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineRefusal.DataFileUnreadable(spelledOption!, file!, errorsHidden: false));
        Diagnostics.Act("exception", exception.GetType().Name);

        Diagnostics.Assert("parameter name", expectedParamName, exception.ParamName);
        Assert.AreEqual(expectedParamName, exception.ParamName);
    }

    private void ArrangeNullArgumentCall(string call, string expectedParamName)
    {
        Diagnostics.Arrange("call", call);
        Diagnostics.Arrange("expected parameter name", expectedParamName);
    }

    private void ActRefusal(CommandLineRefusal refusal)
    {
        Diagnostics.Act("exit code", $"{(int)refusal.ExitCode} ({refusal.ExitCode})");
        foreach (string line in refusal.StandardErrorLines)
        {
            Diagnostics.Act("stderr", line);
        }
    }

    private void WriteAssertRefusal(CurlExitCode expectedExitCode, CommandLineRefusal refusal, params string[] expectedLines)
    {
        Diagnostics.Assert("exit code", expectedExitCode, refusal.ExitCode);
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(expectedLines),
            CommandLineParseDiagnostics.QuoteEach(refusal.StandardErrorLines));
    }

    private void AssertRefusal(CommandLineRefusal refusal, string expectedFirstLine)
    {
        WriteAssertRefusal(CurlExitCode.FailedInit, refusal, expectedFirstLine, CommandLineRefusal.TryHelpLine);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
    }
}
