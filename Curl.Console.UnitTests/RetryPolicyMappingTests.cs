using Curl.Cli;
using Curl.Core;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how <see cref="RetryPolicyMapping" /> copies the parsed retry options onto the
/// <see cref="RetryPolicy" /> the retrier applies.
/// </summary>
[TestClass]
public sealed class RetryPolicyMappingTests
{
    private const string Url = "http://127.0.0.1/a";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromCommandLine_RetryConnectionRefused_SetsRetryConnectionRefused()
    {
        RetryPolicy policy = RetryPolicyMapping.FromCommandLine(Parse("--retry", "2", "--retry-connrefused", Url));
        Diagnostics.Act("retry connection refused / retries", $"{policy.RetryConnectionRefused} / {policy.Retries}");

        Diagnostics.Assert("retry connection refused / retries", "True / 2", $"{policy.RetryConnectionRefused} / {policy.Retries}");
        Assert.IsTrue(policy.RetryConnectionRefused);
        Assert.AreEqual(2, policy.Retries);
    }

    [TestMethod]
    public void FromCommandLine_WithoutRetryConnectionRefused_LeavesRetryConnectionRefusedFalse()
    {
        RetryPolicy policy = RetryPolicyMapping.FromCommandLine(Parse("--retry", "2", Url));
        Diagnostics.Act("retry connection refused", policy.RetryConnectionRefused);

        Diagnostics.Assert("retry connection refused", false, policy.RetryConnectionRefused);
        Assert.IsFalse(policy.RetryConnectionRefused);
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.ArrangeCommandLine(arguments);
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
