namespace Curl.Cryptography;

/// <summary>
/// Skips a long-running test - slow, but pure computation - unless the environment variable
/// <c>CURL_RUN_LONG_RUNNING_TESTS</c> is <c>1</c> (ADR-0421, decision 3). The fast run reports
/// it as skipped; the Integration tests workflow sets the variable and runs it.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RunsOnlyWhenLongRunningTestsAreEnabledAttribute : ConditionBaseAttribute
{
    /// <summary>The environment variable that, set to <c>1</c>, runs long-running tests.</summary>
    public const string EnvironmentVariableName = "CURL_RUN_LONG_RUNNING_TESTS";

    /// <summary>Initializes the attribute so the test runs only when the variable is <c>1</c>.</summary>
    public RunsOnlyWhenLongRunningTestsAreEnabledAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = $"Long-running test: set {EnvironmentVariableName}=1 and run dotnet test --filter \"TestCategory=LongRunning\" to run it.";
    }

    /// <summary>Gets whether <c>CURL_RUN_LONG_RUNNING_TESTS</c> is <c>1</c>.</summary>
    public override bool IsConditionMet => Environment.GetEnvironmentVariable(EnvironmentVariableName) == "1";

    /// <summary>Gets the group condition attributes of this kind are combined in.</summary>
    public override string GroupName => nameof(RunsOnlyWhenLongRunningTestsAreEnabledAttribute);
}
