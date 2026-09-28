using Curl.Cli;
using Curl.Core;

namespace Curl.Console;

/// <summary>
/// Pins how <see cref="RetryPolicyMapping" /> copies the parsed retry options onto the
/// <see cref="RetryPolicy" /> the retrier applies.
/// </summary>
[TestClass]
public sealed class RetryPolicyMappingTests
{
    private const string Url = "http://127.0.0.1/a";

    [TestMethod]
    public void FromCommandLine_RetryConnectionRefused_SetsRetryConnectionRefused()
    {
        RetryPolicy policy = RetryPolicyMapping.FromCommandLine(Parse("--retry", "2", "--retry-connrefused", Url));

        Assert.IsTrue(policy.RetryConnectionRefused);
        Assert.AreEqual(2, policy.Retries);
    }

    [TestMethod]
    public void FromCommandLine_WithoutRetryConnectionRefused_LeavesRetryConnectionRefusedFalse()
    {
        RetryPolicy policy = RetryPolicyMapping.FromCommandLine(Parse("--retry", "2", Url));

        Assert.IsFalse(policy.RetryConnectionRefused);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
