using Curl.Conformance.Differential.Api;

namespace Curl.Conformance.Differential.UnitTests.Api;

[TestClass]
public sealed class ApiDifferentialCasesTests
{
    [TestMethod]
    public void All_EveryCase_HasAUniqueNameAndARequestOnTheLoopbackPathItsCommandNames()
    {
        var cases = ApiDifferentialCases.All;

        Assert.HasCount(20, cases);
        Assert.HasCount(cases.Count, cases.Select(differentialCase => differentialCase.Name).Distinct());
        foreach (var differentialCase in cases)
        {
            StringAssert.Contains(differentialCase.Command, "{U}" + differentialCase.Request.Path, differentialCase.Name);
        }
    }

    [TestMethod]
    public void All_EveryCase_FollowsRedirectsExactlyWhenItsCommandPassesTheLocationOption()
    {
        foreach (var differentialCase in ApiDifferentialCases.All)
        {
            var commandFollows = differentialCase.Command.Split(' ')
                .Any(token => token.StartsWith('-') && !token.StartsWith("--", StringComparison.Ordinal) && token.Contains('L'));

            Assert.AreEqual(commandFollows, differentialCase.Request.FollowsRedirects, differentialCase.Name);
        }
    }

    [TestMethod]
    public void All_Redirect307PostCase_FollowsTheRedirectWithItsPostBody()
    {
        var request = ApiDifferentialCases.All.Single(candidate => candidate.Name == "api-redirect-307-post").Request;

        Assert.AreEqual("POST", request.Method);
        Assert.AreEqual("x=1", request.Body);
        Assert.IsTrue(request.FollowsRedirects);
    }

    [TestMethod]
    public void All_CookieEngineRedirectCase_DescribesTheRequestItsCommandMakes()
    {
        var request = ApiDifferentialCases.All.Single(candidate => candidate.Name == "api-cookie-engine-redirect").Request;

        Assert.AreEqual("GET", request.Method);
        Assert.IsTrue(request.StartsCookieEngine);
        Assert.IsTrue(request.FollowsRedirects);
        Assert.IsNull(request.Body);
        Assert.IsEmpty(request.Headers);
        Assert.IsEmpty(request.FormFields);
        Assert.IsNull(request.UserName);
        Assert.IsNull(request.Password);
        Assert.IsFalse(request.UsesDigest);
        Assert.IsNull(request.Cookie);
        Assert.IsNull(request.UserAgent);
    }
}
