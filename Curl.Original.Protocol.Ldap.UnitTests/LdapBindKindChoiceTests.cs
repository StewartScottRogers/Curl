using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>Pins <see cref="LdapBindKindChoice.Choose" /> against curl 8.22.0 Schannel's <c>ldap_win_bind</c>.</summary>
[TestClass]
public sealed class LdapBindKindChoiceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow((int)HttpAuthSchemes.None, (int)LdapBindKind.LogonUser)]
    [DataRow((int)HttpAuthSchemes.Basic, (int)LdapBindKind.Simple)]
    [DataRow((int)HttpAuthSchemes.Ntlm, (int)LdapBindKind.Ntlm)]
    [DataRow((int)HttpAuthSchemes.Digest, (int)LdapBindKind.Digest)]
    [DataRow((int)HttpAuthSchemes.Negotiate, (int)LdapBindKind.Negotiate)]
    [DataRow((int)HttpAuthSchemes.Bearer, (int)LdapBindKind.LogonUser)]
    [DataRow((int)(HttpAuthSchemes.Basic | HttpAuthSchemes.Ntlm), (int)LdapBindKind.Simple)]
    [DataRow((int)HttpAuthSchemes.Any, (int)LdapBindKind.Simple)]
    [DataRow((int)(HttpAuthSchemes.Ntlm | HttpAuthSchemes.Negotiate), (int)LdapBindKind.Negotiate)]
    [DataRow((int)(HttpAuthSchemes.Digest | HttpAuthSchemes.Ntlm), (int)LdapBindKind.Ntlm)]
    [DataRow((int)(HttpAuthSchemes.Digest | HttpAuthSchemes.Negotiate), (int)LdapBindKind.Negotiate)]
    [DataRow((int)(HttpAuthSchemes.Digest | HttpAuthSchemes.Ntlm | HttpAuthSchemes.Negotiate), (int)LdapBindKind.Negotiate)]
    public void Choose_WithCredentials_FollowsTheAuthMask(int schemes, int expected)
    {
        Diagnostics.Arrange("schemes", (HttpAuthSchemes)schemes);

        LdapBindKind actual = LdapBindKindChoice.Choose((HttpAuthSchemes)schemes, hasCredentials: true);

        Diagnostics.Act("Choose", actual);
        Diagnostics.Assert("bind kind", (LdapBindKind)expected, actual);
        Assert.AreEqual((LdapBindKind)expected, actual);
    }

    [TestMethod]
    [DataRow((int)HttpAuthSchemes.None)]
    [DataRow((int)HttpAuthSchemes.Basic)]
    [DataRow((int)HttpAuthSchemes.Ntlm)]
    [DataRow((int)HttpAuthSchemes.Digest)]
    [DataRow((int)HttpAuthSchemes.Negotiate)]
    [DataRow((int)HttpAuthSchemes.Any)]
    public void Choose_WithoutCredentials_BindsAsTheLogonUser(int schemes)
    {
        Diagnostics.Arrange("schemes", (HttpAuthSchemes)schemes);

        LdapBindKind actual = LdapBindKindChoice.Choose((HttpAuthSchemes)schemes, hasCredentials: false);

        Diagnostics.Act("Choose", actual);
        Diagnostics.Assert("bind kind", LdapBindKind.LogonUser, actual);
        Assert.AreEqual(LdapBindKind.LogonUser, actual);
    }

    [TestMethod]
    public void Choose_AnyMaskWithoutBasic_NeverYieldsTheSimpleBind()
    {
        Diagnostics.Arrange("masks", "every combination of Basic, Digest, NTLM, Negotiate and Bearer without Basic");

        var simpleMasks = Enumerable.Range(0, 32)
            .Select(bits => (HttpAuthSchemes)bits)
            .Where(schemes => !schemes.HasFlag(HttpAuthSchemes.Basic))
            .Where(schemes => LdapBindKindChoice.Choose(schemes, hasCredentials: true) == LdapBindKind.Simple)
            .ToList();

        Diagnostics.Act("masks yielding Simple", simpleMasks.Count);
        Diagnostics.Assert("masks yielding Simple", 0, simpleMasks.Count);
        Assert.IsEmpty(simpleMasks);
    }
}
