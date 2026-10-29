using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Abstractions.Arguments;

[TestClass]
public sealed class ArgumentExpansionTests
{
    private static readonly ExtensionOption RequestFile = new("example-request-file", ExtensionOptionArgument.Value, "Read requests from a file");

    private static readonly ExtensionOption Flag = new("example-flag", ExtensionOptionArgument.None, "Add a request");

    private static readonly ArgumentExpansionContext Context = new(name => name == "HOST" ? "h" : null, path => path == "f" ? Encoding.UTF8.GetBytes("http://h/f") : null);

    [TestMethod]
    public void Expand_WithNoExpanders_GivesTheArgumentsBack()
    {
        string[] arguments = ["--example-request-file", "f"];

        Assert.AreSame(arguments, ArgumentExpansion.Expand(arguments, [], Context));
    }

    [TestMethod]
    public void Expand_WithNoExpandersOption_GivesTheSameArguments()
    {
        string[] arguments = ["-H", "A: b", "--verbose", "--data=x", "http://h/"];

        CollectionAssert.AreEqual(arguments, ArgumentExpansion.Expand(arguments, [new FileExpander(RequestFile)], Context).ToArray());
    }

    [TestMethod]
    public void Expand_OptionAndNextValue_AreReplacedByTheExpandersArguments()
    {
        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["-s", "--example-request-file", "f", "-v"], [new FileExpander(RequestFile)], Context);

        CollectionAssert.AreEqual(new[] { "-s", "-X", "GET", "http://h/f", "-v" }, expanded.ToArray());
    }

    [TestMethod]
    public void Expand_OptionWithAttachedValue_IsReplacedByTheExpandersArguments()
    {
        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["--example-request-file=f", "-v"], [new FileExpander(RequestFile)], Context);

        CollectionAssert.AreEqual(new[] { "-X", "GET", "http://h/f", "-v" }, expanded.ToArray());
    }

    [TestMethod]
    public void Expand_Flag_IsReplacedWithoutTakingTheNextArgument()
    {
        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["--example-flag", "http://x/"], [new FileExpander(Flag)], Context);

        CollectionAssert.AreEqual(new[] { "flag:", "h", "http://x/" }, expanded.ToArray());
    }

    [TestMethod]
    public void Expand_OptionWithNoValueAfterIt_IsLeftForTheParser()
    {
        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["http://x/", "--example-request-file"], [new FileExpander(RequestFile)], Context);

        CollectionAssert.AreEqual(new[] { "http://x/", "--example-request-file" }, expanded.ToArray());
    }

    [TestMethod]
    public void Expand_AfterEndOfOptions_LeavesEveryArgument()
    {
        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(["--example-flag", "--", "--example-flag", "x"], [new FileExpander(Flag)], Context);

        CollectionAssert.AreEqual(new[] { "flag:", "h", "--", "--example-flag", "x" }, expanded.ToArray());
    }

    [TestMethod]
    public void Expand_ExpandersFailure_ReachesTheCaller()
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(
            () => ArgumentExpansion.Expand(["--example-request-file", "missing"], [new FileExpander(RequestFile)], Context));

        Assert.AreEqual("cannot read missing", failure.Message);
        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
    }

    [TestMethod]
    public void Expand_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ArgumentExpansion.Expand(null!, [], Context));
        Assert.ThrowsExactly<ArgumentNullException>(() => ArgumentExpansion.Expand([], null!, Context));
        Assert.ThrowsExactly<ArgumentNullException>(() => ArgumentExpansion.Expand([], [], null!));
    }

    /// <summary>Expands a file's text into a GET of the URL it holds, and a flag into an argument naming the HOST variable.</summary>
    private sealed class FileExpander(ExtensionOption option) : IArgumentExpander
    {
        public ExtensionOption Option => option;

        public IReadOnlyList<string> ArgumentsFor(string value, ArgumentExpansionContext context)
        {
            if (option.Argument == ExtensionOptionArgument.None)
            {
                return ["flag:" + value, context.ReadEnvironmentVariable("HOST")!];
            }

            byte[] contents = context.ReadFile(value) ?? throw new ArgumentExpansionException("cannot read " + value, CurlExitCode.ReadError);
            return ["-X", "GET", Encoding.UTF8.GetString(contents)];
        }
    }
}
