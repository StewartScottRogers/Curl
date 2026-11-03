namespace Curl.Extended.Protocol.Plugin;

/// <summary>Checks that a helper started through the host's process starter (ADR-1184) is that process, seen as a helper.</summary>
[TestClass]
public sealed class ExtensionHelperProcessStarterTests
{
    [TestMethod]
    public async Task Start_GivesTheHostsProcessWithTheUrlAsItsOneArgument()
    {
        StandInExtensionProcess process = new("ok\n", 3);
        StandInExtensionProcessStarter starter = new(process);

        IHelperProcess helper = new ExtensionHelperProcessStarter(starter).Start("/bin/curl-proto-demo", "demo://host/a");

        Assert.AreEqual("/bin/curl-proto-demo", starter.StartedProgram);
        CollectionAssert.AreEqual(new[] { "demo://host/a" }, starter.StartedArguments!.ToArray());
        Assert.AreSame(process.StandardInput, helper.Input);
        Assert.AreSame(process.StandardOutput, helper.Output);
        Assert.AreEqual(3, await helper.WaitForExitAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task DisposeAsync_EndsAndReleasesTheHostsProcess()
    {
        StandInExtensionProcess process = new(string.Empty);

        await new ExtensionHelperProcessStarter(new StandInExtensionProcessStarter(process)).Start("p", "demo://h").DisposeAsync();

        Assert.IsTrue(process.IsKilled);
        Assert.IsTrue(process.IsDisposed);
    }

    [TestMethod]
    public void Start_WhenTheHostCannotStartIt_ThrowsTheHostsIOException()
    {
        IOException thrown = Assert.ThrowsExactly<IOException>(() => new ExtensionHelperProcessStarter(new StandInExtensionProcessStarter(null)).Start("p", "demo://h"));

        Assert.AreEqual("The system cannot find the file specified.", thrown.Message);
        Assert.IsNull(thrown.InnerException);
    }
}
