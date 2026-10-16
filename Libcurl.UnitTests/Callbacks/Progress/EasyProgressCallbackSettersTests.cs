using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests.Callbacks.Progress;

[TestClass]
public sealed class EasyProgressCallbackSettersTests
{
    [TestMethod]
    public void SetOption_ProgressXferinfoAndDebugFunctions_SetTheirCallbacksAndNullClearsThem()
    {
        var options = new EasyOptions();
        ProgressCallback progress = (_, _, _, _) => 0;
        TransferInfoCallback transferInfo = (_, _, _, _) => 0;
        DebugCallback debug = (_, _) => 0;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Progressfunction, progress));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Xferinfofunction, transferInfo));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Debugfunction, debug));
        Assert.AreSame(progress, options.Callbacks.Progress);
        Assert.AreSame(transferInfo, options.Callbacks.TransferInfo);
        Assert.AreSame(debug, options.Callbacks.Debug);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Progressfunction, null));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Xferinfofunction, null));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Debugfunction, null));
        Assert.IsNull(options.Callbacks.Progress);
        Assert.IsNull(options.Callbacks.TransferInfo);
        Assert.IsNull(options.Callbacks.Debug);
    }

    [TestMethod]
    [DataRow(CurlOption.Progressfunction)]
    [DataRow(CurlOption.Xferinfofunction)]
    [DataRow(CurlOption.Debugfunction)]
    public void SetOption_FunctionGivenAnotherDelegateType_ReturnsBadFunctionArgumentAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, new Func<int>(() => 1)));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.Progressfunction)]
    [DataRow(CurlOption.Xferinfofunction)]
    [DataRow(CurlOption.Debugfunction)]
    public void SetOption_FunctionGivenANonDelegate_ReturnsBadFunctionArgument(CurlOption option)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, "callback"));
    }

    [TestMethod]
    public void SetOption_ProgressFunctionGivenTheXferinfoType_ReturnsBadFunctionArgument()
    {
        var options = new EasyOptions();
        TransferInfoCallback transferInfo = (_, _, _, _) => 0;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, CurlOption.Progressfunction, transferInfo));
        Assert.IsNull(options.Callbacks.Progress);
    }

    [TestMethod]
    [DataRow(CurlOption.Xferinfodata)]
    [DataRow(CurlOption.Debugdata)]
    public void SetOption_DataPointerProgressdataBeingXferinfodata_TakesAnyValueAndChangesNothing(CurlOption option)
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
