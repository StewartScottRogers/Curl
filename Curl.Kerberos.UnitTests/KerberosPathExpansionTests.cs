namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosPathExpansion" /> expands MIT's Unix <c>%{token}</c>
/// parameters, leaves other text alone, does not re-expand what a token gives, and refuses
/// an unclosed or unknown token as MIT's <c>k5_expand_path_tokens</c> does.
/// </summary>
[TestClass]
public sealed class KerberosPathExpansionTests
{
    [TestMethod]
    [DataRow("FILE:/tmp/krb5cc_%{uid}", "FILE:/tmp/krb5cc_1000")]
    [DataRow("FILE:/tmp/krb5cc_%{euid}", "FILE:/tmp/krb5cc_1000")]
    [DataRow("FILE:/tmp/krb5cc_%{USERID}", "FILE:/tmp/krb5cc_1000")]
    [DataRow("FILE:/home/%{username}/cache", "FILE:/home/alice/cache")]
    [DataRow("FILE:%{LIBDIR}/k", "FILE:/usr/local/lib/k")]
    [DataRow("FILE:%{BINDIR}/k", "FILE:/usr/local/bin/k")]
    [DataRow("FILE:%{SBINDIR}/k", "FILE:/usr/local/sbin/k")]
    [DataRow("FILE:/tmp/x%{null}y", "FILE:/tmp/xy")]
    [DataRow("KEYRING:persistent:%{uid}", "KEYRING:persistent:1000")]
    [DataRow("DIR:/run/%{uid}/%{username}", "DIR:/run/1000/alice")]
    [DataRow("FILE:/etc/krb5.keytab", "FILE:/etc/krb5.keytab")]
    [DataRow("FILE:/tmp/100%/x}", "FILE:/tmp/100%/x}")]
    [DataRow("", "")]
    public void Expand_KnownTokens_AreReplaced(string value, string expected)
    {
        Assert.AreEqual(expected, Expansion(null).Expand(value));
    }

    [TestMethod]
    public void Expand_TempWithTmpdirUnset_IsTmp()
    {
        Assert.AreEqual("FILE:/tmp/krb5cc", Expansion(null).Expand("FILE:%{TEMP}/krb5cc"));
    }

    [TestMethod]
    public void Expand_TempWithTmpdirSet_IsTmpdir()
    {
        Assert.AreEqual("FILE:/var/tmp/krb5cc", Expansion("/var/tmp").Expand("FILE:%{TEMP}/krb5cc"));
    }

    [TestMethod]
    public void Expand_TokenGivesATokenLikeValue_IsNotReExpanded()
    {
        Assert.AreEqual("FILE:%{uid}/krb5cc", Expansion("%{uid}").Expand("FILE:%{TEMP}/krb5cc"));
    }

    [TestMethod]
    [DataRow("FILE:/tmp/krb5cc_%{uid")]
    [DataRow("FILE:/tmp/krb5cc_%{UID}")]
    [DataRow("FILE:/tmp/krb5cc_%{}")]
    [DataRow("FILE:%{APPDATA}/krb5cc")]
    public void Expand_UnclosedOrUnknownToken_FailsAsPathTokenInvalid(string value)
    {
        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Expansion(null).Expand(value));

        Assert.AreEqual(KerberosFileError.PathTokenInvalid, failure.Error);
    }

    private static KerberosPathExpansion Expansion(string? tmpdir) =>
        new(1000, name => name == KerberosPathExpansion.TemporaryDirectoryVariable ? tmpdir : "unexpected", () => "alice");
}
