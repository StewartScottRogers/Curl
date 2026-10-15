using Curl.Api.Codes;
using Curl.Api.EngineContracts.ConnectOnly;

namespace Curl.Api.UnitTests.EngineContracts.ConnectOnly;

[TestClass]
public sealed class CurlConnectOnlyResultTests
{
    [TestMethod]
    public void Constructor_KeepsResultAndCount()
    {
        var result = new CurlConnectOnlyResult(CurlCode.Again, 0);

        Assert.AreEqual(CurlCode.Again, result.Result);
        Assert.AreEqual(0, result.ByteCount);
    }

    [TestMethod]
    public void Default_IsOkWithNoBytes()
    {
        var result = default(CurlConnectOnlyResult);

        Assert.AreEqual(CurlCode.Ok, result.Result);
        Assert.AreEqual(0, result.ByteCount);
    }
}
