using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="CurlComposition.CreateDialingSecurityContextFactory" />: the hand-built NTLM
/// wrapper is put around the router only when asked for (BL-1858, BL-1946).
/// </summary>
[TestClass]
public sealed class CurlCompositionDialingSecurityContextFactoryTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CreateDialingSecurityContextFactory_UsesHandBuiltNtlm_WrapsTheRouterOnlyWhenSet(bool usesHandBuiltNtlm)
    {
        Diagnostics.Arrange("usesHandBuiltNtlm", usesHandBuiltNtlm);

        ISecurityContextFactory factory = CurlComposition.CreateDialingSecurityContextFactory(
            new ScriptedConnector([]),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
            null,
            usesHandBuiltNtlm);

        Assert.AreEqual(usesHandBuiltNtlm, factory is HandBuiltNtlmSecurityContextFactory);
    }
}
