using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins the exact two standard-error lines and the exit code of every refusal the
/// command-line parser can raise: <c>curl: option &lt;spelled&gt;: &lt;reason&gt;</c>
/// followed by the try-help line, exiting with <see cref="CurlExitCode.FailedInit"/>,
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

    private static void AssertRefusal(CommandLineRefusal refusal, string expectedFirstLine)
    {
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
    }
}
