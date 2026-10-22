namespace Curl.Conformance.UnitProgram.UnitTests;

[TestClass]
public sealed class UnitProgramContextTests
{
    [TestMethod]
    public void Constructor_KeepsTheNumberAndACopyOfTheArguments()
    {
        string[] arguments = ["a", "b"];

        UnitProgramContext context = new(1302, arguments);
        arguments[0] = "changed";

        Assert.AreEqual(1302, context.TestNumber);
        CollectionAssert.AreEqual(new[] { "a", "b" }, context.Arguments.ToArray());
    }

    [TestMethod]
    public void Streams_DefaultToNothingKept()
    {
        UnitProgramContext context = new(1302, []);

        Assert.AreSame(Stream.Null, context.StandardOutput);
        Assert.AreSame(TextWriter.Null, context.StandardError);
    }

    [TestMethod]
    public void Streams_AreTheOnesSet()
    {
        using MemoryStream output = new();
        using StringWriter error = new();

        UnitProgramContext context = new(1302, []) { StandardOutput = output, StandardError = error };

        Assert.AreSame(output, context.StandardOutput);
        Assert.AreSame(error, context.StandardError);
    }

    [TestMethod]
    public void Constructor_NegativeNumber_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new UnitProgramContext(-1, []));
    }

    [TestMethod]
    public void Constructor_NullArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UnitProgramContext(1302, null!));
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new UnitProgramContext(1302, [null!]));

        Assert.AreEqual("arguments", exception.ParamName);
    }
}
