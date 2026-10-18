namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestContextTests
{
    [TestMethod]
    public void Constructor_KeepsTheTestNumberAndArguments()
    {
        var context = new LibTestContext(500, ["http://127.0.0.1:8990/500", "log/ip500"]);

        Assert.AreEqual(500, context.TestNumber);
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:8990/500", "log/ip500" }, context.Arguments.ToArray());
    }

    [TestMethod]
    public void Url_IsTheFirstArgument()
    {
        var context = new LibTestContext(500, ["http://127.0.0.1:8990/500"]);

        Assert.AreEqual("http://127.0.0.1:8990/500", context.Url);
    }

    [TestMethod]
    public void Argument2To4_AreTheSecondToFourthArguments()
    {
        var context = new LibTestContext(1, ["url", "two", "three", "four"]);

        Assert.AreEqual("two", context.Argument2);
        Assert.AreEqual("three", context.Argument3);
        Assert.AreEqual("four", context.Argument4);
    }

    [TestMethod]
    public void Argument2To4_AreNullWhenNotGiven()
    {
        var context = new LibTestContext(1, ["url"]);

        Assert.IsNull(context.Argument2);
        Assert.IsNull(context.Argument3);
        Assert.IsNull(context.Argument4);
    }

    [TestMethod]
    public void Constructor_CopiesTheArguments()
    {
        var arguments = new List<string> { "url", "two" };
        var context = new LibTestContext(1, arguments);

        arguments[0] = "changed";
        arguments.Add("three");

        Assert.AreEqual("url", context.Url);
        Assert.HasCount(2, context.Arguments);
    }

    [TestMethod]
    public void Constructor_AcceptsTestNumberZero()
    {
        var context = new LibTestContext(0, ["url"]);

        Assert.AreEqual(0, context.TestNumber);
    }

    [TestMethod]
    public void Constructor_RefusesANegativeTestNumber()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LibTestContext(-1, ["url"]));
    }

    [TestMethod]
    public void Constructor_RefusesNullArguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestContext(1, null!));
    }

    [TestMethod]
    public void Url_WithNoArguments_IsNullAsFirstCLeavesIt()
    {
        var context = new LibTestContext(501, []);

        Assert.IsNull(context.Url);
        Assert.IsEmpty(context.Arguments);
        Assert.IsNull(context.Argument2);
    }

    [TestMethod]
    public void Constructor_RefusesANullArgument()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestContext(1, ["url", null!]));
    }

    [TestMethod]
    public void Constructor_RefusesANullFirstArgument()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestContext(1, [null!, "arg2"]));
    }

    [TestMethod]
    public void EngineAndStreams_WhenNotSet_AreNoEngineAndNullStreams()
    {
        var context = new LibTestContext(1, ["url"]);

        Assert.IsNull(context.Engine);
        Assert.AreSame(Stream.Null, context.StandardOutput);
        Assert.AreSame(TextWriter.Null, context.StandardError);
    }

    [TestMethod]
    public void EngineAndStreams_WhenSet_AreThoseGiven()
    {
        FakeCurlEngine engine = new();
        using MemoryStream output = new();
        using StringWriter error = new();

        var context = new LibTestContext(1, ["url"]) { Engine = engine, StandardOutput = output, StandardError = error };

        Assert.AreSame(engine, context.Engine);
        Assert.AreSame(output, context.StandardOutput);
        Assert.AreSame(error, context.StandardError);
    }
}
