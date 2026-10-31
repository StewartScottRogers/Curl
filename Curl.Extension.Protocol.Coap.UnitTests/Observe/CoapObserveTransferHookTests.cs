using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Coap.Observe;

/// <summary>Checks the <c>--coap-observe</c> option: activated only when given, and marking only <c>coap://</c> and <c>coaps://</c> transfers.</summary>
[TestClass]
public sealed class CoapObserveTransferHookTests
{
    [TestMethod]
    public void Option_IsAFlagNamedCoapObserve()
    {
        Assert.AreEqual("coap-observe", CoapObserveTransferHook.Option.Name);
        Assert.AreEqual(ExtensionOptionArgument.None, CoapObserveTransferHook.Option.Argument);
    }

    [TestMethod]
    public void ActivatedBy_WithoutTheOption_IsNull() =>
        Assert.IsNull(CoapObserveTransferHook.ActivatedBy(ExtensionOptionValues.None));

    [TestMethod]
    public void ActivatedBy_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CoapObserveTransferHook.ActivatedBy(null!));

    [TestMethod]
    [DataRow("coap://h/r", true)]
    [DataRow("COAP://h/r", true)]
    [DataRow("coaps://h/r", true)]
    [DataRow("http://h/r", false)]
    public async Task ExecuteAsync_ATransfer_MarksItOnlyWhenItIsCoapOrCoaps(string url, bool marked)
    {
        ITransferHook hook = CoapObserveTransferHook.ActivatedBy(new ExtensionOptionValues(new Dictionary<string, string> { ["coap-observe"] = "" }))!;
        var context = new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() };
        ITransferContext? given = null;

        await hook.ExecuteAsync(context, next =>
        {
            given = next;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreEqual(marked, given is CoapObserveContext);
        Assert.AreSame(context.Url, given!.Url);
    }
}
