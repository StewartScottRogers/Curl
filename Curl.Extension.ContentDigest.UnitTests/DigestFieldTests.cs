namespace Curl.Extension.ContentDigest.UnitTests;

[TestClass]
public sealed class DigestFieldTests
{
    [TestMethod]
    public void Parse_TheRfc9530Section2Example_GivesTheSha256Digest()
    {
        var digests = DigestField.Parse("sha-256=:RK/0qy18MlBSVnWgjwz6lZEWjP/lF5HF9bvEF8FabDg=:");

        Assert.HasCount(1, digests);
        Assert.AreEqual("RK/0qy18MlBSVnWgjwz6lZEWjP/lF5HF9bvEF8FabDg=", Convert.ToBase64String(digests["sha-256"]));
    }

    [TestMethod]
    public void Parse_SeveralMembersWithParametersAndWhitespace_GivesEachAndTheLastOfARepeatedKey_Rfc8941Section4_2_2()
    {
        var digests = DigestField.Parse("sha-256=:AAAA:;p=1 ,\tsha-512=:AQID:, md5=:BBBB:, sha-256=:AQ==:");

        CollectionAssert.AreEquivalent(new[] { "sha-256", "sha-512", "md5" }, digests.Keys.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1 }, digests["sha-256"]);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, digests["sha-512"]);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("sha-256")]
    [DataRow("=:AAAA:")]
    [DataRow("Sha-256=:AAAA:")]
    [DataRow("1sha=:AAAA:")]
    [DataRow("sha 256=:AAAA:")]
    [DataRow("sha-256=AAAA")]
    [DataRow("sha-256=:AAAA")]
    [DataRow("sha-256=AAAA:")]
    [DataRow("sha-256=:")]
    [DataRow("sha-256=:A:")]
    [DataRow("sha-256=:AAAA:, sha-512")]
    public void Parse_AValueThatIsNotADictionaryOfByteSequences_IsIgnoredWhole_Rfc8941Section4_2(string? value)
    {
        Assert.IsEmpty(DigestField.Parse(value));
    }

    [TestMethod]
    public void Parse_AStarKeyAndAnEmptyByteSequence_AreValid()
    {
        Assert.IsEmpty(DigestField.Parse("*x_1.2=::")["*x_1.2"]);
    }

    [TestMethod]
    public void Format_GivesOneMemberWithAByteSequence_Rfc9530Section2()
    {
        Assert.AreEqual("sha-256=:AQID:", DigestField.Format("sha-256", [1, 2, 3]));
    }
}
