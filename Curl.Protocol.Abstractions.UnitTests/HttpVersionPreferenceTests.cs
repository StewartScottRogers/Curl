namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the values of <see cref="HttpVersionPreference" />: HTTP/1.1 is the default, and
/// the HTTP/3 and HTTP/2 values follow the ones already in use.
/// </summary>
[TestClass]
public sealed class HttpVersionPreferenceTests
{
    [TestMethod]
    public void Values_StartAtHttp11WithHttp3AndHttp2Appended()
    {
        HttpVersionPreference[] expected =
        [
            HttpVersionPreference.Http11,
            HttpVersionPreference.Http10,
            HttpVersionPreference.Http3,
            HttpVersionPreference.Http3Only,
            HttpVersionPreference.Http2PriorKnowledge,
            HttpVersionPreference.Http2,
        ];

        CollectionAssert.AreEqual(expected, Enum.GetValues<HttpVersionPreference>());
        Assert.AreEqual(0, (int)HttpVersionPreference.Http11);
        Assert.AreEqual(2, (int)HttpVersionPreference.Http3);
        Assert.AreEqual(3, (int)HttpVersionPreference.Http3Only);
        Assert.AreEqual(4, (int)HttpVersionPreference.Http2PriorKnowledge);
        Assert.AreEqual(5, (int)HttpVersionPreference.Http2);
    }
}
