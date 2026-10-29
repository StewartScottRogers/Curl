namespace Curl.Extension.NamedPipe;

/// <summary>Checks <see cref="NamedPipeName" /> against Microsoft's "Pipe Names": <c>\\ServerName\pipe\PipeName</c>.</summary>
[TestClass]
public sealed class NamedPipeNameTests
{
    [TestMethod]
    public void TryParse_PipeNamesLocalForm_GivesTheDotServer()
    {
        Assert.IsTrue(NamedPipeName.TryParse(@"\\.\pipe\docker_engine", out NamedPipeName? name));
        Assert.AreEqual(new NamedPipeName(".", "docker_engine"), name);
    }

    [TestMethod]
    public void TryParse_PipeNamesRemoteForm_GivesTheServer()
    {
        Assert.IsTrue(NamedPipeName.TryParse(@"\\build01\pipe\agent", out NamedPipeName? name));
        Assert.AreEqual(new NamedPipeName("build01", "agent"), name);
    }

    [TestMethod]
    public void TryParse_PipeNamesAreCaseInsensitive_AcceptsPipeInAnyCase()
    {
        Assert.IsTrue(NamedPipeName.TryParse(@"\\.\PIPE\Agent", out NamedPipeName? name));
        Assert.AreEqual(new NamedPipeName(".", "Agent"), name);
    }

    [TestMethod]
    public void TryParse_PipeNameMayHoldAnyCharacterButABackslash_KeepsThem()
    {
        Assert.IsTrue(NamedPipeName.TryParse(@"\\.\pipe\a b/c:1.2-$", out NamedPipeName? name));
        Assert.AreEqual("a b/c:1.2-$", name!.PipeName);
    }

    [TestMethod]
    public void TryParse_TheWholeStringUpTo256Characters_IsAccepted()
    {
        string text = @"\\.\pipe\" + new string('p', NamedPipeName.MaximumLength - 9);

        Assert.IsTrue(NamedPipeName.TryParse(text, out _));
        Assert.IsFalse(NamedPipeName.TryParse(text + "p", out NamedPipeName? name));
        Assert.IsNull(name);
    }

    [TestMethod]
    [DataRow(@"\.\pipe\x", DisplayName = "one leading backslash")]
    [DataRow(@"//./pipe/x", DisplayName = "forward slashes")]
    [DataRow(@"\\.\x", DisplayName = "no pipe segment")]
    [DataRow(@"\\\pipe\x", DisplayName = "no server")]
    [DataRow(@"\\a\b\pipe\x", DisplayName = "a backslash in the server")]
    [DataRow(@"\\.\pipe\", DisplayName = "no pipe name")]
    [DataRow(@"\\.\pipe\a\b", DisplayName = "a backslash in the pipe name")]
    [DataRow("", DisplayName = "empty")]
    public void TryParse_TextThatIsNotAPipeName_IsRefused(string text)
    {
        Assert.IsFalse(NamedPipeName.TryParse(text, out NamedPipeName? name));
        Assert.IsNull(name);
    }

    [TestMethod]
    public void TryParse_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => NamedPipeName.TryParse(null!, out _));

    [TestMethod]
    public void ToString_WritesThePipeNamesForm() =>
        Assert.AreEqual(@"\\build01\pipe\agent", new NamedPipeName("build01", "agent").ToString());
}
