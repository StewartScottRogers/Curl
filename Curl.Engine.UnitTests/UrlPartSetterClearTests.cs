using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

/// <summary>Pins what <see cref="UrlPartSetter.Cleared" /> leaves of the scheme's guessed mark (AF-0880, BL-5544).</summary>
[TestClass]
public sealed class UrlPartSetterClearTests
{
    [TestMethod]
    public void Cleared_SchemeOfAGuessedSchemeHandle_ClearsTheSchemeAndTheGuessedMark()
    {
        UrlHandleParts guessed = new() { Scheme = "http", SchemeGuessed = true, Host = "example.com" };

        UrlHandleParts cleared = UrlPartSetter.Cleared(guessed, CurlUrlPart.Scheme);

        Assert.IsNull(cleared.Scheme);
        Assert.IsFalse(cleared.SchemeGuessed);
        Assert.AreEqual("example.com", cleared.Host);
    }
}
