using Curl.Conformance.UnitProgram.Asn1.Names;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Names;

[TestClass]
public sealed class Unit3205CipherSuiteNamesTests
{
    [TestMethod]
    public async Task RunAsync_AnyPlatform_PassesAndWritesNothing()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await new Unit3205CipherSuiteNames().RunAsync(new UnitProgramContext(3205, []) { StandardError = standardError, StandardOutput = standardOutput }, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0L, standardOutput.Length);
        Assert.AreEqual("unit3205", new Unit3205CipherSuiteNames().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit3205CipherSuiteNames().RunAsync(null!, CancellationToken.None));
    }
}
