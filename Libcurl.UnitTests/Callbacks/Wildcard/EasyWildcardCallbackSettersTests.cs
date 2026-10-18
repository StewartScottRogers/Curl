using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests.Callbacks.Wildcard;

[TestClass]
public sealed class EasyWildcardCallbackSettersTests
{
    [TestMethod]
    public void SetOption_ChunkBgnFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        ChunkBeginCallback chunkBegin = (_, _) => ChunkBeginResult.Ok;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.ChunkBgnFunction, chunkBegin));
        Assert.AreSame(chunkBegin, options.Callbacks.ChunkBegin);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.ChunkBgnFunction, null));
        Assert.IsNull(options.Callbacks.ChunkBegin);
    }

    [TestMethod]
    public void SetOption_ChunkEndFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        ChunkEndCallback chunkEnd = () => ChunkEndResult.Ok;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.ChunkEndFunction, chunkEnd));
        Assert.AreSame(chunkEnd, options.Callbacks.ChunkEnd);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.ChunkEndFunction, null));
        Assert.IsNull(options.Callbacks.ChunkEnd);
    }

    [TestMethod]
    public void SetOption_FnmatchFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        FileNameMatchCallback fileNameMatch = (_, _) => FileNameMatchResult.Match;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.FnmatchFunction, fileNameMatch));
        Assert.AreSame(fileNameMatch, options.Callbacks.FileNameMatch);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.FnmatchFunction, null));
        Assert.IsNull(options.Callbacks.FileNameMatch);
    }

    [TestMethod]
    public void SetOption_Interleavefunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        InterleaveCallback interleave = data => data.Length;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Interleavefunction, interleave));
        Assert.AreSame(interleave, options.Callbacks.Interleave);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Interleavefunction, null));
        Assert.IsNull(options.Callbacks.Interleave);
    }

    [TestMethod]
    [DataRow(CurlOption.ChunkBgnFunction)]
    [DataRow(CurlOption.ChunkEndFunction)]
    [DataRow(CurlOption.FnmatchFunction)]
    [DataRow(CurlOption.Interleavefunction)]
    public void SetOption_FunctionGivenAnotherDelegateType_ReturnsBadFunctionArgumentAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, new Func<int>(() => 1)));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.ChunkData)]
    [DataRow(CurlOption.FnmatchData)]
    [DataRow(CurlOption.Interleavedata)]
    public void SetOption_Data_TakesAnyValueAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, new object()));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, null));
        Assert.AreSame(before, options);
    }

    private static CurlCode Set(ref EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);
}
