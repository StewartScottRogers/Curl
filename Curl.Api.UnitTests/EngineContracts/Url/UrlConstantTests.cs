using Curl.Api.EngineContracts.Url;

namespace Curl.Api.UnitTests.EngineContracts.Url;

[TestClass]
public sealed class UrlConstantTests
{
    [TestMethod]
    [DataRow(CurlUrlPart.Url, 0, DisplayName = "CURLUPART_URL")]
    [DataRow(CurlUrlPart.Scheme, 1, DisplayName = "CURLUPART_SCHEME")]
    [DataRow(CurlUrlPart.User, 2, DisplayName = "CURLUPART_USER")]
    [DataRow(CurlUrlPart.Password, 3, DisplayName = "CURLUPART_PASSWORD")]
    [DataRow(CurlUrlPart.Options, 4, DisplayName = "CURLUPART_OPTIONS")]
    [DataRow(CurlUrlPart.Host, 5, DisplayName = "CURLUPART_HOST")]
    [DataRow(CurlUrlPart.Port, 6, DisplayName = "CURLUPART_PORT")]
    [DataRow(CurlUrlPart.Path, 7, DisplayName = "CURLUPART_PATH")]
    [DataRow(CurlUrlPart.Query, 8, DisplayName = "CURLUPART_QUERY")]
    [DataRow(CurlUrlPart.Fragment, 9, DisplayName = "CURLUPART_FRAGMENT")]
    [DataRow(CurlUrlPart.ZoneId, 10, DisplayName = "CURLUPART_ZONEID")]
    public void CurlUrlPart_Member_HasUpstreamNumber(CurlUrlPart part, int expected)
    {
        Assert.AreEqual(expected, (int)part);
    }

    [TestMethod]
    public void CurlUrlPart_HasOneMemberPerUpstreamPart()
    {
        Assert.HasCount(11, Enum.GetValues<CurlUrlPart>());
    }

    [TestMethod]
    [DataRow(CurlUrlFlags.None, 0, DisplayName = "no flag")]
    [DataRow(CurlUrlFlags.DefaultPort, 1 << 0, DisplayName = "CURLU_DEFAULT_PORT")]
    [DataRow(CurlUrlFlags.NoDefaultPort, 1 << 1, DisplayName = "CURLU_NO_DEFAULT_PORT")]
    [DataRow(CurlUrlFlags.DefaultScheme, 1 << 2, DisplayName = "CURLU_DEFAULT_SCHEME")]
    [DataRow(CurlUrlFlags.NonSupportScheme, 1 << 3, DisplayName = "CURLU_NON_SUPPORT_SCHEME")]
    [DataRow(CurlUrlFlags.PathAsIs, 1 << 4, DisplayName = "CURLU_PATH_AS_IS")]
    [DataRow(CurlUrlFlags.DisallowUser, 1 << 5, DisplayName = "CURLU_DISALLOW_USER")]
    [DataRow(CurlUrlFlags.UrlDecode, 1 << 6, DisplayName = "CURLU_URLDECODE")]
    [DataRow(CurlUrlFlags.UrlEncode, 1 << 7, DisplayName = "CURLU_URLENCODE")]
    [DataRow(CurlUrlFlags.AppendQuery, 1 << 8, DisplayName = "CURLU_APPENDQUERY")]
    [DataRow(CurlUrlFlags.GuessScheme, 1 << 9, DisplayName = "CURLU_GUESS_SCHEME")]
    [DataRow(CurlUrlFlags.NoAuthority, 1 << 10, DisplayName = "CURLU_NO_AUTHORITY")]
    [DataRow(CurlUrlFlags.AllowSpace, 1 << 11, DisplayName = "CURLU_ALLOW_SPACE")]
    [DataRow(CurlUrlFlags.Punycode, 1 << 12, DisplayName = "CURLU_PUNYCODE")]
    [DataRow(CurlUrlFlags.PunycodeToIdn, 1 << 13, DisplayName = "CURLU_PUNY2IDN")]
    [DataRow(CurlUrlFlags.GetEmpty, 1 << 14, DisplayName = "CURLU_GET_EMPTY")]
    [DataRow(CurlUrlFlags.NoGuessScheme, 1 << 15, DisplayName = "CURLU_NO_GUESS_SCHEME")]
    public void CurlUrlFlags_Member_HasUpstreamNumber(CurlUrlFlags flag, int expected)
    {
        Assert.AreEqual(expected, (int)flag);
    }

    [TestMethod]
    public void CurlUrlFlags_HasOneMemberPerUpstreamFlagAndNone()
    {
        Assert.HasCount(17, Enum.GetValues<CurlUrlFlags>());
    }
}
