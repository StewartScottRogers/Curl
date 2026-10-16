using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests.Callbacks.Hsts;

[TestClass]
public sealed class EasyHstsCallbackSettersTests
{
    [TestMethod]
    public void SetOption_Hstsreadfunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        HstsReadCallback read = (out HstsEntry? entry) =>
        {
            entry = null;
            return HstsResult.Done;
        };

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Hstsreadfunction, read));
        Assert.AreSame(read, options.Callbacks.HstsRead);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Hstsreadfunction, null));
        Assert.IsNull(options.Callbacks.HstsRead);
    }

    [TestMethod]
    public void SetOption_Hstswritefunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        HstsWriteCallback write = (_, _, _) => HstsResult.Ok;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Hstswritefunction, write));
        Assert.AreSame(write, options.Callbacks.HstsWrite);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Hstswritefunction, null));
        Assert.IsNull(options.Callbacks.HstsWrite);
    }

    [TestMethod]
    [DataRow(CurlOption.Hstsreadfunction)]
    [DataRow(CurlOption.Hstswritefunction)]
    public void SetOption_FunctionGivenAnotherDelegateType_ReturnsBadFunctionArgumentAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, new Func<int>(() => 1)));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.Hstsreaddata)]
    [DataRow(CurlOption.Hstswritedata)]
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
