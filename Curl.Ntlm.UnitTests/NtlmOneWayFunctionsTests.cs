namespace Curl.Ntlm;

/// <summary>
/// Pins <see cref="NtlmOneWayFunctions" /> to MS-NLMP section 4.2's password hashes, and to
/// curl 8.21.0's handling of what MS-NLMP's examples do not reach: LM's 14-byte limit, an
/// empty password (whose LM keys are DES weak keys) and ASCII-only uppercasing.
/// </summary>
[TestClass]
public sealed class NtlmOneWayFunctionsTests
{
    [TestMethod]
    public void ComputeLmOwfV1_MsNlmpPassword_GivesSection42211sHash()
    {
        // MS-NLMP 4.2.2.1.1: LMOWFv1("Password").
        Assert.AreEqual("e52cac67419a9a224a3b108f3fa6cb6d", Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeLmOwfV1("Password")));
    }

    [TestMethod]
    public void ComputeNtOwfV1_MsNlmpPassword_GivesSection42212sHash()
    {
        // MS-NLMP 4.2.2.1.2: NTOWFv1("Password").
        Assert.AreEqual("a4f49c406510bdcab6824ee7c30fd852", Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeNtOwfV1("Password")));
    }

    [TestMethod]
    public void ComputeNtOwfV2_MsNlmpUserAndDomain_GivesSection42411sHash()
    {
        // MS-NLMP 4.2.4.1.1: NTOWFv2("Password", "User", "Domain").
        byte[] ntOwfV1 = NtlmOneWayFunctions.ComputeNtOwfV1("Password");

        Assert.AreEqual("0c868a403bfd7a93a3001ef22ef02e3f", Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeNtOwfV2("User", "Domain", ntOwfV1)));
    }

    [TestMethod]
    public void ComputeNtOwfV2_UserInAnyCase_HashesTheSame()
    {
        byte[] ntOwfV1 = NtlmOneWayFunctions.ComputeNtOwfV1("Password");

        CollectionAssert.AreEqual(
            NtlmOneWayFunctions.ComputeNtOwfV2("USER", "Domain", ntOwfV1),
            NtlmOneWayFunctions.ComputeNtOwfV2("uSeR", "Domain", ntOwfV1));
    }

    [TestMethod]
    public void ComputeNtOwfV2_DomainInAnotherCase_HashesDifferently()
    {
        // curl uppercases the user only (ascii_uppercase_to_unicode_le), never the domain.
        byte[] ntOwfV1 = NtlmOneWayFunctions.ComputeNtOwfV1("Password");

        CollectionAssert.AreNotEqual(
            NtlmOneWayFunctions.ComputeNtOwfV2("User", "Domain", ntOwfV1),
            NtlmOneWayFunctions.ComputeNtOwfV2("User", "DOMAIN", ntOwfV1));
    }

    [TestMethod]
    public void ComputeNtOwfV2_NtOwfV1NotSixteenBytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => NtlmOneWayFunctions.ComputeNtOwfV2("User", "Domain", new byte[15]));
    }

    [TestMethod]
    public void ComputeLmOwfV1_EmptyPassword_GivesTheWellKnownEmptyLmHash()
    {
        // Both halves are DES under the weak all-zero key, which the BCL's DES refuses.
        Assert.AreEqual("aad3b435b51404eeaad3b435b51404ee", Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeLmOwfV1(string.Empty)));
    }

    [TestMethod]
    public void ComputeNtOwfV1_EmptyPassword_GivesTheWellKnownEmptyNtHash()
    {
        Assert.AreEqual("31d6cfe0d16ae931b73c59d7e0c089c0", Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeNtOwfV1(string.Empty)));
    }

    [TestMethod]
    public void ComputeLmOwfV1_PasswordLongerThanFourteen_ReadsOnlyTheFirstFourteen()
    {
        CollectionAssert.AreEqual(
            NtlmOneWayFunctions.ComputeLmOwfV1("ABCDEFGHIJKLMN"),
            NtlmOneWayFunctions.ComputeLmOwfV1("abcdefghijklmnopqrstuvwxyz"));
    }

    [TestMethod]
    public void ComputeLmOwfV1_NonAsciiLetter_IsNotUppercased()
    {
        // Curl_strntoupper changes a to z only; "é" and "É" stay different bytes.
        CollectionAssert.AreNotEqual(NtlmOneWayFunctions.ComputeLmOwfV1("é"), NtlmOneWayFunctions.ComputeLmOwfV1("É"));
    }

    [TestMethod]
    public void ComputeNtOwfV1_NonAsciiPassword_HashesEachUtf8ByteWidened()
    {
        // curl's ascii_to_unicode_le widens the UTF-8 bytes C3 A9, not the UTF-16 unit E9 00.
        byte[] expected = new byte[16];
        Curl.Cryptography.Md4.HashData([0xC3, 0x00, 0xA9, 0x00], expected);

        CollectionAssert.AreEqual(expected, NtlmOneWayFunctions.ComputeNtOwfV1("é"));
    }

    [TestMethod]
    public void OneWayFunctions_NullString_Throw()
    {
        byte[] ntOwfV1 = new byte[16];

        Assert.ThrowsExactly<ArgumentNullException>(() => NtlmOneWayFunctions.ComputeNtOwfV1(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => NtlmOneWayFunctions.ComputeLmOwfV1(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => NtlmOneWayFunctions.ComputeNtOwfV2(null!, "Domain", ntOwfV1));
        Assert.ThrowsExactly<ArgumentNullException>(() => NtlmOneWayFunctions.ComputeNtOwfV2("User", null!, ntOwfV1));
    }
}
