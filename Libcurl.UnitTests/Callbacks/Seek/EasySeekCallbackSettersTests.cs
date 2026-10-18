using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests.Callbacks.Seek;

[TestClass]
public sealed class EasySeekCallbackSettersTests
{
    [TestMethod]
    public void SetOption_SeekFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        SeekCallback seek = (_, _) => SeekResult.Ok;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Seekfunction, seek));
        Assert.AreSame(seek, options.Callbacks.Seek);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Seekfunction, null));
        Assert.IsNull(options.Callbacks.Seek);
    }

    [TestMethod]
    public void SetOption_SeekFunctionGivenAnotherDelegateType_ReturnsBadFunctionArgumentAndChangesNothing()
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, CurlOption.Seekfunction, new Func<int>(() => 1)));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    public void SetOption_SeekFunctionGivenANonDelegate_ReturnsBadFunctionArgument()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, CurlOption.Seekfunction, "callback"));
        Assert.IsNull(options.Callbacks.Seek);
    }

    [TestMethod]
    public void SetOption_SeekData_TakesAnyValueAndChangesNothing()
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Seekdata, new object()));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Seekdata, null));
        Assert.AreSame(before, options);
    }

    private static CurlCode Set(ref EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);
}
