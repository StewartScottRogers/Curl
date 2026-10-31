using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.HttpFile;

/// <summary>Checks <see cref="HttpFileVariables" /> against the Visual Studio .http file format's variable references.</summary>
[TestClass]
public sealed class HttpFileVariablesTests
{
    [TestMethod]
    public void Substitute_ReferencesWithSpaces_AreReplaced()
    {
        HttpFileVariables variables = new(_ => null);
        variables.Set("a", "1");
        variables.Define("b", "{{ a }}2");

        Assert.AreEqual("x12y1", variables.Substitute("x{{b}}y{{a}}"));
    }

    [TestMethod]
    public void Substitute_OpenWithoutClose_StaysAsWritten()
    {
        HttpFileVariables variables = new(_ => null);
        variables.Set("a", "1");

        Assert.AreEqual("1 {{ a", variables.Substitute("{{a}} {{ a"));
    }

    [TestMethod]
    public void Substitute_ProcessEnv_ReadsTheEnvironmentVariable_OrGivesEmpty()
    {
        HttpFileVariables variables = new(name => name == "HOME_DIR" ? "/home/u" : null);

        Assert.AreEqual("/home/u|", variables.Substitute("{{$processEnv HOME_DIR}}|{{$processEnv UNSET}}"));
    }

    [TestMethod]
    public void Substitute_UndefinedOrSystemVariable_FailsWithExit2()
    {
        HttpFileVariables variables = new(_ => null);

        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => variables.Substitute("{{$guid}}"));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("http-file: variable '$guid' is not defined", failure.Message);
    }
}
