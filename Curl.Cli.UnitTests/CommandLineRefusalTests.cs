using Curl.Protocol.Abstractions;

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
    [TestMethod]
    public void TryHelpLine_Always_IsCurlsExactText()
    {
        string tryHelpLine = CommandLineRefusal.TryHelpLine;

        Assert.AreEqual("curl: try 'curl --help' or 'curl --manual' for more information", tryHelpLine);
    }

    [TestMethod]
    public void UnknownOption_Spelled_NamesOptionAsUnknown()
    {
        CommandLineRefusal refusal = CommandLineRefusal.UnknownOption("--bogus");

        AssertRefusal(refusal, "curl: option --bogus: is unknown");
    }

    [TestMethod]
    public void CannotBeReversed_Spelled_NamesOptionAsNotReversible()
    {
        CommandLineRefusal refusal = CommandLineRefusal.CannotBeReversed("--no-output");

        AssertRefusal(refusal, "curl: option --no-output: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void RequiresParameter_Spelled_NamesOptionAsRequiringParameter()
    {
        CommandLineRefusal refusal = CommandLineRefusal.RequiresParameter("-o");

        AssertRefusal(refusal, "curl: option -o: requires parameter");
    }

    [TestMethod]
    public void BlankArgument_Spelled_NamesOptionAsBlank()
    {
        CommandLineRefusal refusal = CommandLineRefusal.BlankArgument("--output=");

        AssertRefusal(refusal, "curl: option --output=: blank argument where content is expected");
    }

    [TestMethod]
    public void BlankArgument_EmptySpelling_LeavesNameEmpty()
    {
        CommandLineRefusal refusal = CommandLineRefusal.BlankArgument(string.Empty);

        AssertRefusal(refusal, "curl: option : blank argument where content is expected");
    }

    [TestMethod]
    public void TooLargeNumber_Spelled_NamesOptionAsTooLarge()
    {
        CommandLineRefusal refusal = CommandLineRefusal.TooLargeNumber("--create-file-mode");

        AssertRefusal(refusal, "curl: option --create-file-mode: too large number");
    }

    [TestMethod]
    public void ExpectedProperNumericalParameter_Spelled_NamesOptionAsNotNumerical()
    {
        CommandLineRefusal refusal = CommandLineRefusal.ExpectedProperNumericalParameter("--tftp-blksize");

        AssertRefusal(refusal, "curl: option --tftp-blksize: expected a proper numerical parameter");
    }

    [TestMethod]
    public void ExpectedPositiveNumericalParameter_Spelled_NamesOptionAsNotPositive()
    {
        CommandLineRefusal refusal = CommandLineRefusal.ExpectedPositiveNumericalParameter("--tftp-blksize");

        AssertRefusal(refusal, "curl: option --tftp-blksize: expected a positive numerical parameter");
    }

    [TestMethod]
    public void EmptyCommandLine_Always_IsTheTryHelpLineAloneWithExit2()
    {
        CommandLineRefusal refusal = CommandLineRefusal.EmptyCommandLine();

        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { CommandLineRefusal.TryHelpLine }, refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void NoUrlSpecified_Always_ReportsNoUrlWithCurlsExitCodePrefix()
    {
        CommandLineRefusal refusal = CommandLineRefusal.NoUrlSpecified();

        AssertRefusal(refusal, "curl: (2) no URL specified");
    }

    [TestMethod]
    public void FileDoesNotExist_Spelled_NamesFileAndOptionInThreeLines()
    {
        CommandLineRefusal refusal = CommandLineRefusal.FileDoesNotExist("--cacert=", "--cacert", "nonexist.pem", errorsHidden: false);

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
        CommandLineRefusal refusal = CommandLineRefusal.FileDoesNotExist("--knownhosts", "--knownhosts", "nope", errorsHidden: true);

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
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineRefusal.FileDoesNotExist(spelledOption!, longOption!, file!, errorsHidden: false));

        Assert.AreEqual(expectedParamName, exception.ParamName);
    }

    [TestMethod]
    public void DataFileUnreadable_ErrorsShown_ExitsWithReadErrorInThreeLines()
    {
        CommandLineRefusal refusal = CommandLineRefusal.DataFileUnreadable("-d", string.Empty, errorsHidden: false);

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
        CommandLineRefusal refusal = CommandLineRefusal.DataFileUnreadable("--data", "missing", errorsHidden: true);

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
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineRefusal.DataFileUnreadable(spelledOption!, file!, errorsHidden: false));

        Assert.AreEqual(expectedParamName, exception.ParamName);
    }

    private static void AssertRefusal(CommandLineRefusal refusal, string expectedFirstLine)
    {
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
    }
}
