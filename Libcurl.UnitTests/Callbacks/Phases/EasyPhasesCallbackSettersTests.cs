using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests.Callbacks.Phases;

[TestClass]
public sealed class EasyPhasesCallbackSettersTests
{
    [TestMethod]
    public void SetOption_PrereqFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        PrerequestCallback prerequest = (_, _, _, _) => PrerequestResult.Ok;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Prereqfunction, prerequest));
        Assert.AreSame(prerequest, options.Callbacks.Prerequest);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Prereqfunction, null));
        Assert.IsNull(options.Callbacks.Prerequest);
    }

    [TestMethod]
    public void SetOption_ResolverStartFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        ResolverStartCallback resolverStart = _ => 0;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.ResolverStartFunction, resolverStart));
        Assert.AreSame(resolverStart, options.Callbacks.ResolverStart);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.ResolverStartFunction, null));
        Assert.IsNull(options.Callbacks.ResolverStart);
    }

    [TestMethod]
    [DataRow(CurlOption.Prereqfunction)]
    [DataRow(CurlOption.ResolverStartFunction)]
    public void SetOption_FunctionGivenAnotherDelegateType_ReturnsBadFunctionArgumentAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, new Func<int>(() => 1)));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.Prereqdata)]
    [DataRow(CurlOption.ResolverStartData)]
    public void SetOption_Data_TakesAnyValueAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, new object()));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, null));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    public void SetOption_TrailerFunction_StillAnswersNotBuiltIn()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.NotBuiltIn, Set(ref options, CurlOption.Trailerfunction, null));
    }

    private static CurlCode Set(ref EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);
}
