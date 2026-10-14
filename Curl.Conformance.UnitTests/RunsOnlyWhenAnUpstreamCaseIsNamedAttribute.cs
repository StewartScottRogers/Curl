namespace Curl.Conformance;

/// <summary>
/// Skips the manual one-case test unless <c>CURL_UPSTREAM_CASE</c> names a case number
/// (BL-2155), so the fast run reports it as skipped and runs no case twice.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RunsOnlyWhenAnUpstreamCaseIsNamedAttribute : ConditionBaseAttribute
{
    /// <summary>Initializes the attribute so the test runs only when a case is named.</summary>
    public RunsOnlyWhenAnUpstreamCaseIsNamedAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = $"Manual test: set {UpstreamCaseDebugSettings.CaseVariableName}=<case number> and run dotnet test --filter \"TestCategory=Manual\" to run that case.";
    }

    /// <summary>Gets whether <c>CURL_UPSTREAM_CASE</c> names a case number.</summary>
    public override bool IsConditionMet => UpstreamCaseDebugSettings.Read(Environment.GetEnvironmentVariable).CaseNumber is not null;

    /// <summary>Gets the group condition attributes of this kind are combined in.</summary>
    public override string GroupName => nameof(RunsOnlyWhenAnUpstreamCaseIsNamedAttribute);
}
