using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests.Callbacks.Seek;

[TestClass]
public sealed class EasyIoctlCallbackSettersTests
{
    [TestMethod]
    public void SetOption_IoctlFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        IoctlCallback ioctl = _ => IoctlResult.Ok;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Ioctlfunction, ioctl));
        Assert.AreSame(ioctl, options.Callbacks.Ioctl);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Ioctlfunction, null));
        Assert.IsNull(options.Callbacks.Ioctl);
    }

    [TestMethod]
    public void SetOption_IoctlFunctionGivenAnotherDelegateType_ReturnsBadFunctionArgumentAndChangesNothing()
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, CurlOption.Ioctlfunction, new Func<int, int>(command => command)));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    public void SetOption_IoctlData_TakesAnyValueAndChangesNothing()
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Ioctldata, new object()));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Ioctldata, null));
        Assert.AreSame(before, options);
    }

    private static CurlCode Set(ref EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);
}
