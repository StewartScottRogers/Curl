namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class ILibTestTests
{
    [TestMethod]
    public async Task RunAsync_GivesAPortItsContextAndReturnsItsResult()
    {
        ILibTest program = new ArgumentCountProgram();
        var context = new LibTestContext(500, ["url", "two"]);

        LibTestResult result = await program.RunAsync(context, CancellationToken.None);

        Assert.AreEqual("lib500", program.ProgramName);
        Assert.AreEqual(2, result.ExitCode);
    }

    private sealed class ArgumentCountProgram : ILibTest
    {
        public string ProgramName => "lib500";

        public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new LibTestResult(context.Arguments.Count));
    }
}
