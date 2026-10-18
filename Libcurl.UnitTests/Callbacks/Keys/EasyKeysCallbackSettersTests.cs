using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests.Callbacks.Keys;

[TestClass]
public sealed class EasyKeysCallbackSettersTests
{
    [TestMethod]
    public void SetOption_SshKeyfunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        SshKeyCallback key = (_, _, _) => SshKeyStatus.Fine;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.SshKeyfunction, key));
        Assert.AreSame(key, options.Callbacks.SshKey);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.SshKeyfunction, null));
        Assert.IsNull(options.Callbacks.SshKey);
    }

    [TestMethod]
    public void SetOption_SshHostkeyfunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        SshHostKeyCallback hostKey = (_, _) => SshKeyMatch.Ok;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.SshHostkeyfunction, hostKey));
        Assert.AreSame(hostKey, options.Callbacks.SshHostKey);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.SshHostkeyfunction, null));
        Assert.IsNull(options.Callbacks.SshHostKey);
    }

    [TestMethod]
    [DataRow(CurlOption.SshKeyfunction)]
    [DataRow(CurlOption.SshHostkeyfunction)]
    public void SetOption_FunctionGivenAnotherDelegateType_ReturnsBadFunctionArgumentAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, new Func<int>(() => 1)));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.SshKeydata)]
    [DataRow(CurlOption.SshHostkeydata)]
    public void SetOption_Data_TakesAnyValueAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, new object()));
        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, null));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.SslCtxFunction)]
    [DataRow(CurlOption.SslCtxData)]
    public void SetOption_SslCtx_AnswersNotBuiltInAsTheSchannelBuildDoesAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;
        object value = option == CurlOption.SslCtxFunction ? new SslContextCallback(_ => CurlCode.Ok) : new object();

        Assert.AreEqual(CurlCode.NotBuiltIn, Set(ref options, option, value));
        Assert.AreEqual(CurlCode.NotBuiltIn, Set(ref options, option, null));
        Assert.AreSame(before, options);
        Assert.IsNull(options.Callbacks.SslContext);
    }

    private static CurlCode Set(ref EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);
}
