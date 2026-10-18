using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests.Callbacks.Sockets;

[TestClass]
public sealed class EasySocketCallbackSettersTests
{
    [TestMethod]
    public void SetOption_SockoptFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        SocketOptionCallback callback = (_, _) => SocketOptionResult.Ok;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Sockoptfunction, callback));
        Assert.AreSame(callback, options.Callbacks.SocketOption);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Sockoptfunction, null));
        Assert.IsNull(options.Callbacks.SocketOption);
    }

    [TestMethod]
    public void SetOption_OpensocketFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        OpenSocketCallback callback = (_, _) => CurlSocket.Bad;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Opensocketfunction, callback));
        Assert.AreSame(callback, options.Callbacks.OpenSocket);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Opensocketfunction, null));
        Assert.IsNull(options.Callbacks.OpenSocket);
    }

    [TestMethod]
    public void SetOption_ClosesocketFunction_SetsTheCallbackAndNullClearsIt()
    {
        var options = new EasyOptions();
        CloseSocketCallback callback = _ => 0;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Closesocketfunction, callback));
        Assert.AreSame(callback, options.Callbacks.CloseSocket);

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Closesocketfunction, null));
        Assert.IsNull(options.Callbacks.CloseSocket);
    }

    [TestMethod]
    [DataRow(CurlOption.Sockoptfunction)]
    [DataRow(CurlOption.Opensocketfunction)]
    [DataRow(CurlOption.Closesocketfunction)]
    public void SetOption_SocketFunctionGivenAnotherDelegateType_ReturnsBadFunctionArgumentAndChangesNothing(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, new Func<int>(() => 1)));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.Sockoptfunction)]
    [DataRow(CurlOption.Opensocketfunction)]
    [DataRow(CurlOption.Closesocketfunction)]
    public void SetOption_SocketFunctionGivenANonDelegate_ReturnsBadFunctionArgument(CurlOption option)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, "callback"));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.Sockoptdata)]
    [DataRow(CurlOption.Opensocketdata)]
    [DataRow(CurlOption.Closesocketdata)]
    public void SetOption_SocketData_TakesAnyValueAndChangesNothing(CurlOption option)
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
