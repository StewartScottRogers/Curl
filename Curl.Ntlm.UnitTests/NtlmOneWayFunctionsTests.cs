using Curl.Testing;

namespace Curl.Ntlm;

/// <summary>
/// Pins <see cref="NtlmOneWayFunctions" /> to MS-NLMP section 4.2's password hashes, and to
/// curl 8.21.0's handling of what MS-NLMP's examples do not reach: LM's 14-byte limit, an
/// empty password (whose LM keys are DES weak keys) and ASCII-only uppercasing.
/// </summary>
[TestClass]
public sealed class NtlmOneWayFunctionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ComputeLmOwfV1_MsNlmpPassword_GivesSection42211sHash()
    {
        // MS-NLMP 4.2.2.1.1: LMOWFv1("Password").
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("password", "Password");

        string hash = Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeLmOwfV1("Password"));
        diagnostics.Act("LMOWFv1", hash);

        diagnostics.Diff("LMOWFv1", "e52cac67419a9a224a3b108f3fa6cb6d", hash);
        Assert.AreEqual("e52cac67419a9a224a3b108f3fa6cb6d", hash);
    }

    [TestMethod]
    public void ComputeNtOwfV1_MsNlmpPassword_GivesSection42212sHash()
    {
        // MS-NLMP 4.2.2.1.2: NTOWFv1("Password").
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("password", "Password");

        string hash = Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeNtOwfV1("Password"));
        diagnostics.Act("NTOWFv1", hash);

        diagnostics.Diff("NTOWFv1", "a4f49c406510bdcab6824ee7c30fd852", hash);
        Assert.AreEqual("a4f49c406510bdcab6824ee7c30fd852", hash);
    }

    [TestMethod]
    public void ComputeNtOwfV2_MsNlmpUserAndDomain_GivesSection42411sHash()
    {
        // MS-NLMP 4.2.4.1.1: NTOWFv2("Password", "User", "Domain").
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntOwfV1 = NtlmOneWayFunctions.ComputeNtOwfV1("Password");
        diagnostics.Arrange("password, user, domain", "Password, User, Domain");
        diagnostics.Bytes("NTOWFv1", ntOwfV1);

        string hash = Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeNtOwfV2("User", "Domain", ntOwfV1));
        diagnostics.Act("NTOWFv2", hash);

        diagnostics.Diff("NTOWFv2", "0c868a403bfd7a93a3001ef22ef02e3f", hash);
        Assert.AreEqual("0c868a403bfd7a93a3001ef22ef02e3f", hash);
    }

    [TestMethod]
    public void ComputeNtOwfV2_UserInAnyCase_HashesTheSame()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntOwfV1 = NtlmOneWayFunctions.ComputeNtOwfV1("Password");
        diagnostics.Arrange("users, domain, password", "USER and uSeR, Domain, Password");

        byte[] upper = NtlmOneWayFunctions.ComputeNtOwfV2("USER", "Domain", ntOwfV1);
        byte[] mixed = NtlmOneWayFunctions.ComputeNtOwfV2("uSeR", "Domain", ntOwfV1);
        diagnostics.Act("NTOWFv2 for USER", Convert.ToHexStringLower(upper));
        diagnostics.Act("NTOWFv2 for uSeR", Convert.ToHexStringLower(mixed));

        diagnostics.Diff("NTOWFv2 uSeR against USER", upper, mixed);
        CollectionAssert.AreEqual(upper, mixed);
    }

    [TestMethod]
    public void ComputeNtOwfV2_DomainInAnotherCase_HashesDifferently()
    {
        // curl uppercases the user only (ascii_uppercase_to_unicode_le), never the domain.
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntOwfV1 = NtlmOneWayFunctions.ComputeNtOwfV1("Password");
        diagnostics.Arrange("user, domains, password", "User, Domain and DOMAIN, Password");

        byte[] mixed = NtlmOneWayFunctions.ComputeNtOwfV2("User", "Domain", ntOwfV1);
        byte[] upper = NtlmOneWayFunctions.ComputeNtOwfV2("User", "DOMAIN", ntOwfV1);
        diagnostics.Act("NTOWFv2 for Domain", Convert.ToHexStringLower(mixed));
        diagnostics.Act("NTOWFv2 for DOMAIN", Convert.ToHexStringLower(upper));

        diagnostics.Diff("NTOWFv2 DOMAIN against Domain (expected to differ)", mixed, upper);
        CollectionAssert.AreNotEqual(mixed, upper);
    }

    [TestMethod]
    public void ComputeNtOwfV2_NtOwfV1NotSixteenBytes_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("NTOWFv1 length", 15);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => NtlmOneWayFunctions.ComputeNtOwfV2("User", "Domain", new byte[15]));
        diagnostics.Act("exception", $"{exception.GetType().Name} for {exception.ParamName}");

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void ComputeLmOwfV1_EmptyPassword_GivesTheWellKnownEmptyLmHash()
    {
        // Both halves are DES under the weak all-zero key, which the BCL's DES refuses.
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("password", "(empty)");

        string hash = Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeLmOwfV1(string.Empty));
        diagnostics.Act("LMOWFv1", hash);

        diagnostics.Diff("LMOWFv1", "aad3b435b51404eeaad3b435b51404ee", hash);
        Assert.AreEqual("aad3b435b51404eeaad3b435b51404ee", hash);
    }

    [TestMethod]
    public void ComputeNtOwfV1_EmptyPassword_GivesTheWellKnownEmptyNtHash()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("password", "(empty)");

        string hash = Convert.ToHexStringLower(NtlmOneWayFunctions.ComputeNtOwfV1(string.Empty));
        diagnostics.Act("NTOWFv1", hash);

        diagnostics.Diff("NTOWFv1", "31d6cfe0d16ae931b73c59d7e0c089c0", hash);
        Assert.AreEqual("31d6cfe0d16ae931b73c59d7e0c089c0", hash);
    }

    [TestMethod]
    public void ComputeLmOwfV1_PasswordLongerThanFourteen_ReadsOnlyTheFirstFourteen()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("passwords", "ABCDEFGHIJKLMN and abcdefghijklmnopqrstuvwxyz");

        byte[] fourteen = NtlmOneWayFunctions.ComputeLmOwfV1("ABCDEFGHIJKLMN");
        byte[] longer = NtlmOneWayFunctions.ComputeLmOwfV1("abcdefghijklmnopqrstuvwxyz");
        diagnostics.Act("LMOWFv1 for ABCDEFGHIJKLMN", Convert.ToHexStringLower(fourteen));
        diagnostics.Act("LMOWFv1 for a to z", Convert.ToHexStringLower(longer));

        diagnostics.Diff("LMOWFv1 a to z against ABCDEFGHIJKLMN", fourteen, longer);
        CollectionAssert.AreEqual(fourteen, longer);
    }

    [TestMethod]
    public void ComputeLmOwfV1_NonAsciiLetter_IsNotUppercased()
    {
        // Curl_strntoupper changes a to z only; "é" and "É" stay different bytes.
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("passwords", "U+00E9 and U+00C9");

        byte[] lower = NtlmOneWayFunctions.ComputeLmOwfV1("é");
        byte[] upper = NtlmOneWayFunctions.ComputeLmOwfV1("É");
        diagnostics.Act("LMOWFv1 for U+00E9", Convert.ToHexStringLower(lower));
        diagnostics.Act("LMOWFv1 for U+00C9", Convert.ToHexStringLower(upper));

        diagnostics.Diff("LMOWFv1 U+00C9 against U+00E9 (expected to differ)", lower, upper);
        CollectionAssert.AreNotEqual(lower, upper);
    }

    [TestMethod]
    public void ComputeNtOwfV1_NonAsciiPassword_HashesEachUtf8ByteWidened()
    {
        // curl's ascii_to_unicode_le widens the UTF-8 bytes C3 A9, not the UTF-16 unit E9 00.
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] expected = new byte[16];
        Curl.Cryptography.Md4.HashData([0xC3, 0x00, 0xA9, 0x00], expected);
        diagnostics.Arrange("password", "U+00E9 (UTF-8 C3 A9)");
        diagnostics.Bytes("expected MD4 of C3 00 A9 00", expected);

        byte[] hash = NtlmOneWayFunctions.ComputeNtOwfV1("é");
        diagnostics.Act("NTOWFv1", Convert.ToHexStringLower(hash));

        diagnostics.Diff("NTOWFv1", expected, hash);
        CollectionAssert.AreEqual(expected, hash);
    }

    [TestMethod]
    public void OneWayFunctions_NullString_Throw()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntOwfV1 = new byte[16];
        diagnostics.Arrange("calls", "NTOWFv1(null), LMOWFv1(null), NTOWFv2(null user), NTOWFv2(null domain)");

        ArgumentNullException[] exceptions =
        [
            Assert.ThrowsExactly<ArgumentNullException>(() => NtlmOneWayFunctions.ComputeNtOwfV1(null!)),
            Assert.ThrowsExactly<ArgumentNullException>(() => NtlmOneWayFunctions.ComputeLmOwfV1(null!)),
            Assert.ThrowsExactly<ArgumentNullException>(() => NtlmOneWayFunctions.ComputeNtOwfV2(null!, "Domain", ntOwfV1)),
            Assert.ThrowsExactly<ArgumentNullException>(() => NtlmOneWayFunctions.ComputeNtOwfV2("User", null!, ntOwfV1)),
        ];
        diagnostics.Act("exception parameters", string.Join(", ", exceptions.Select(exception => exception.ParamName)));

        diagnostics.Assert("exceptions thrown", 4, exceptions.Length);
    }
}
