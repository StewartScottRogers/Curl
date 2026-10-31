namespace Curl.Extension.RateLimit.UnitTests;

[TestClass]
public sealed class RateLimitFieldTests
{
    [TestMethod]
    public void Parse_TheRateLimitPolicyExample_GivesTheQuotaAndWindow_Section3Point2()
    {
        var policy = RateLimitField.Parse("\"default\";q=100;w=10").Single();

        Assert.AreEqual("default", policy.Name);
        Assert.AreEqual(100L, policy.ParameterOf("q"));
        Assert.AreEqual(10L, policy.ParameterOf("w"));
        Assert.IsNull(policy.ParameterOf("qu"));
    }

    [TestMethod]
    public void Parse_TheRateLimitExample_GivesTheAvailableQuotaAndEffectiveWindow_Section4Point2()
    {
        var limit = RateLimitField.Parse("\"default\";r=50;t=30").Single();

        Assert.AreEqual("default", limit.Name);
        Assert.AreEqual(50L, limit.ParameterOf("r"));
        Assert.AreEqual(30L, limit.ParameterOf("t"));
    }

    [TestMethod]
    public void Parse_SeveralMembersWithSpacesAndAPartitionKey_GivesEachMemberAndLeavesOutTheByteSequence_Section4Point1Point3()
    {
        var limits = RateLimitField.Parse("\"day\";r=0;t=3600 , \"hour\" ; r=5 ; pk=:cHJvamVjdC0xMjM=:");

        CollectionAssert.AreEqual(new[] { "day", "hour" }, limits.Select(limit => limit.Name).ToArray());
        Assert.AreEqual(0L, limits[0].ParameterOf("r"));
        Assert.AreEqual(5L, limits[1].ParameterOf("r"));
        Assert.IsNull(limits[1].ParameterOf("pk"));
    }

    [TestMethod]
    public void Parse_ANameThatIsNotAString_SkipsOnlyThatMember_Section4()
    {
        var limits = RateLimitField.Parse("default;r=0;t=9, ,\"\";r=1, \"a,b;c\";r=2;t=-1;x;t=7");

        Assert.HasCount(2, limits);
        Assert.AreEqual(string.Empty, limits[0].Name);
        Assert.AreEqual("a,b;c", limits[1].Name);
        Assert.AreEqual(2L, limits[1].ParameterOf("r"));
        Assert.AreEqual(7L, limits[1].ParameterOf("t"));
    }

    [TestMethod]
    public void Parse_AnAbsentField_GivesNoMembers()
    {
        Assert.IsEmpty(RateLimitField.Parse(null));
    }
}
