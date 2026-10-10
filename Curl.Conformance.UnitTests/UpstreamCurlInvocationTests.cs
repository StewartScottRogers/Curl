using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins <see cref="UpstreamCurlInvocation"/>'s environment: empty unless one is given.</summary>
[TestClass]
public sealed class UpstreamCurlInvocationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void EnvironmentVariables_NoneGiven_IsEmpty()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        UpstreamCurlInvocation invocation = new([], Stream.Null, Stream.Null, Stream.Null, null!, new UnreachableDatagramConnector());

        diagnostics.Assert("variable count", 0, invocation.EnvironmentVariables.Count);
        Assert.IsEmpty(invocation.EnvironmentVariables);
    }

    [TestMethod]
    public void EnvironmentVariables_Given_AreThoseVariables()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["LC_ALL"] = "C.UTF-8" };

        UpstreamCurlInvocation invocation = new([], Stream.Null, Stream.Null, Stream.Null, null!, new UnreachableDatagramConnector(), variables);

        diagnostics.Assert("LC_ALL", "C.UTF-8", invocation.EnvironmentVariables["LC_ALL"]);
        Assert.AreSame(variables, invocation.EnvironmentVariables);
    }
}
