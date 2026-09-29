using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <content>
/// Pins the output each reference build writes for a search's entries, recorded with
/// <c>Record-CurlExchange.ps1 -Script</c> on 2026-09-28 (BL-588) with
/// <c>-sS -u cn=u:p -w [%{size_download}]</c>: curl 8.21.0 with WinLDAP on Windows for
/// <see cref="LdapDialect.WinLdap" />, curl 8.18.0 with OpenLDAP 2.6.10 on Linux for
/// <see cref="LdapDialect.OpenLdap" />. Each row names its recording, the replies the server
/// sent after the BindResponse, and stdout byte for byte (one <see cref="char" /> per byte);
/// <c>size_download</c> was the length of stdout every time.
/// </content>
public sealed partial class LdapProtocolHandlerTests
{
    private const string EntryF = "30 17 02 01 02 64 12 04 04 63 6e 3d 66 30 0a 30 08 04 01 61 31 03 04 01 31";

    [TestMethod]
    [DataRow("astral-win", "30 19 02 01 02 64 14 04 06 61 f0 9f 98 80 62 30 0a 30 08 04 01 61 31 03 04 01 31 30 1b 02 01 02 64 16 04 04 61 ff fe 62 30 0e 30 0c 04 05 61 e2 82 ac 62 31 03 04 01 32 30 1c 02 01 02 64 17 04 06 61 c2 62 e0 a0 62 30 0d 30 0b 04 04 c2 81 c2 80 31 03 04 01 33 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: a??b\n\ta: 1\n\nDN: a??b\n\ta\u0080b: 2\n\nDN: a?b?b\n\n")]
    [DataRow("badentry-win", "30 0a 02 01 02 64 05 04 03 6e 3d 78 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: n=x\n")]
    [DataRow("binname-win", "30 6f 02 01 02 64 6a 04 04 63 6e 3d 71 30 62 30 11 04 08 78 3b 62 69 6e 61 72 79 31 05 04 03 61 62 63 30 11 04 08 79 3b 42 49 4e 41 52 59 31 05 04 03 61 62 63 30 10 04 07 7a 62 69 6e 61 72 79 31 05 04 03 61 62 63 30 10 04 07 3b 62 69 6e 61 72 79 31 05 04 03 61 62 63 30 16 04 05 6d 69 78 65 64 31 0d 04 02 6f 6b 04 02 00 01 04 03 6f 6b 32 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=q\n\tx;binary:: YWJj\n\n\ty;BINARY:: YWJj\n\n\tzbinary: abc\n\n\t;binary: abc\n\n\tmixed: ok\n\tmixed:: AAE=\n\tmixed: ok2\n\n")]
    [DataRow("dn-win", "30 13 02 01 02 64 0e 04 00 30 0a 30 08 04 01 61 31 03 04 01 31 30 18 02 01 02 64 13 04 05 63 6e 3d c3 a9 30 0a 30 08 04 01 62 31 03 04 01 32 30 19 02 01 02 64 14 04 06 20 63 6e 3d 73 70 30 0a 30 08 04 01 63 31 03 04 01 33 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: \n\ta: 1\n\nDN: cn=\u00e9\n\tb: 2\n\nDN:  cn=sp\n\tc: 3\n\n")]
    [DataRow("empty-win", "30 20 02 01 02 64 1b 04 04 63 6e 3d 65 30 13 30 07 04 01 65 31 02 04 00 30 08 04 01 66 31 03 04 01 78 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=e\n\te: \n\n\tf: x\n\n")]
    [DataRow("emptybin-win", "30 2a 02 01 02 64 25 04 05 63 6e 3d 65 62 30 1c 30 0e 04 08 78 3b 62 69 6e 61 72 79 31 02 04 00 30 0a 04 01 79 31 05 04 00 04 01 00 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=eb\n\tx;binary:: \n\n\ty: \n\ty:: AA==\n\n")]
    [DataRow("long-win", "30 81 c7 02 01 02 64 81 c1 04 04 63 6e 3d 6c 30 81 b8 30 6e 04 04 6c 6f 6e 67 31 66 04 64 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 30 46 04 04 6c 62 69 6e 31 3e 04 3c 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=l\n\tlong: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\n\n\tlbin:: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n\n")]
    [DataRow("manyattrsnone-win", "30 15 02 01 02 64 10 04 04 63 6e 3d 7a 30 08 30 06 04 02 6e 76 31 00 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=z\n\n")]
    [DataRow("names-win", "30 23 02 01 02 64 1e 04 05 63 6e 3d c4 80 30 15 30 09 04 02 c3 a9 31 03 04 01 76 30 08 04 01 61 31 03 04 01 31 30 17 02 01 02 64 12 04 04 63 6e 3d ff 30 0a 30 08 04 01 62 31 03 04 01 32 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=?\n\t\u00e9: v\n\n\ta: 1\n\nDN: cn=?\n\tb: 2\n\n")]
    [DataRow("noattr-win", "30 0d 02 01 02 64 08 04 04 63 6e 3d 63 30 00 30 18 02 01 02 64 13 04 04 63 6e 3d 64 30 0b 30 09 04 02 73 6e 31 03 04 01 7a 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=c\nDN: cn=d\n\tsn: z\n\n")]
    [DataRow("novals-win", "30 1f 02 01 02 64 1a 04 04 63 6e 3d 6e 30 12 30 06 04 02 6e 76 31 00 30 08 04 01 66 31 03 04 01 78 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=n\n\n\tf: x\n\n")]
    [DataRow("print-win", "30 81 b3 02 01 02 64 81 ad 04 04 63 6e 3d 70 30 81 a4 30 0b 04 03 62 69 6e 31 04 04 02 00 ff 30 0c 04 04 6c 65 61 64 31 04 04 02 20 61 30 0d 04 05 74 72 61 69 6c 31 04 04 02 61 20 30 0c 04 03 74 61 62 31 05 04 03 61 09 62 30 0c 04 04 6c 74 61 62 31 04 04 02 09 61 30 0c 04 04 75 74 66 38 31 04 04 02 c3 a9 30 0a 04 02 68 69 31 04 04 02 61 e9 30 0c 04 03 63 74 6c 31 05 04 03 61 01 62 30 0b 04 03 64 65 6c 31 04 04 02 61 7f 30 0b 04 02 6e 6c 31 05 04 03 61 0a 62 30 0b 04 02 63 72 31 05 04 03 61 0d 62 30 0d 04 03 73 79 6d 31 06 04 04 7e 21 3a 3c 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=p\n\tbin:: AP8=\n\n\tlead:: IGE=\n\n\ttrail:: YSA=\n\n\ttab: a\tb\n\n\tltab:: CWE=\n\n\tutf8:: w6k=\n\n\thi:: Yek=\n\n\tctl:: YQFi\n\n\tdel:: YX8=\n\n\tnl: a\nb\n\n\tcr: a\rb\n\n\tsym: ~!:<\n\n")]
    [DataRow("ref-win", "30 18 02 01 02 64 13 04 05 63 6e 3d 72 31 30 0a 30 08 04 01 61 31 03 04 01 31 30 25 02 01 02 73 20 04 11 6c 64 61 70 3a 2f 2f 6f 74 68 65 72 2f 64 63 3d 78 04 0b 6c 64 61 70 3a 2f 2f 74 77 6f 2f 30 18 02 01 02 64 13 04 05 63 6e 3d 72 32 30 0a 30 08 04 01 62 31 03 04 01 32 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=r1\n\ta: 1\n\nDN: cn=r2\n\tb: 2\n\n")]
    [DataRow("size-win", "30 17 02 01 02 64 12 04 04 63 6e 3d 73 30 0a 30 08 04 01 61 31 03 04 01 31 30 0c 02 01 02 65 07 0a 01 04 04 00 04 00", "DN: cn=s\n\ta: 1\n\n")]
    [DataRow("two-win", "30 35 02 01 02 64 30 04 0f 63 6e 3d 61 2c 64 63 3d 65 78 61 6d 70 6c 65 30 1d 30 0c 04 02 63 6e 31 06 04 01 61 04 01 62 30 0d 04 04 6d 61 69 6c 31 05 04 03 78 40 79 30 23 02 01 02 64 1e 04 0f 63 6e 3d 62 2c 64 63 3d 65 78 61 6d 70 6c 65 30 0b 30 09 04 02 73 6e 31 03 04 01 7a 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=a,dc=example\n\tcn: a\n\tcn: b\n\n\tmail: x@y\n\nDN: cn=b,dc=example\n\tsn: z\n\n")]
    [DataRow("ws-win", "30 81 86 02 01 02 64 81 80 04 04 63 6e 3d 77 30 78 30 0b 04 03 6c 6e 6c 31 04 04 02 0a 61 30 0b 04 03 74 6e 6c 31 04 04 02 61 0a 30 0b 04 03 6c 63 72 31 04 04 02 0d 61 30 0b 04 02 76 74 31 05 04 03 61 0b 62 30 0b 04 02 66 66 31 05 04 03 61 0c 62 30 0c 04 03 6e 75 6c 31 05 04 03 61 00 62 30 09 04 02 73 70 31 03 04 01 20 30 0e 04 07 74 61 62 6f 6e 6c 79 31 03 04 01 09 30 0c 04 03 6d 69 64 31 05 04 03 61 20 62 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=w\n\tlnl: \na\n\n\ttnl: a\n\n\n\tlcr: \ra\n\n\tvt: a\u000bb\n\n\tff: a\u000cb\n\n\tnul:: YQBi\n\n\tsp:: IA==\n\n\ttabonly:: CQ==\n\n\tmid: a b\n\n")]
    public async Task ExecuteAsync_WinLdapEntries_WritesWhatWinLdapWrote(string recording, string replies, string expected)
    {
        (TransferResult result, byte[] sent, byte[] output) = await RunSearchWritingAsync(LdapDialect.WinLdap, replies);

        Assert.AreEqual(TransferResult.Success(expected.Length), result, recording);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(expected), output, recording);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindCnU + " " + WinLdapSearchX + " " + WinLdapUnbind3), sent, recording);
    }

    [TestMethod]
    [DataRow("binname-linux", "30 6f 02 01 02 64 6a 04 04 63 6e 3d 71 30 62 30 11 04 08 78 3b 62 69 6e 61 72 79 31 05 04 03 61 62 63 30 11 04 08 79 3b 42 49 4e 41 52 59 31 05 04 03 61 62 63 30 10 04 07 7a 62 69 6e 61 72 79 31 05 04 03 61 62 63 30 10 04 07 3b 62 69 6e 61 72 79 31 05 04 03 61 62 63 30 16 04 05 6d 69 78 65 64 31 0d 04 02 6f 6b 04 02 00 01 04 03 6f 6b 32 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=q\n\tx;binary:: YWJj\n\n\ty;BINARY:: YWJj\n\n\tzbinary: abc\n\n\t;binary: abc\n\n\tmixed: ok\n\tmixed:: AAE=\n\tmixed: ok2\n\n\n")]
    [DataRow("dn-linux", "30 13 02 01 02 64 0e 04 00 30 0a 30 08 04 01 61 31 03 04 01 31 30 18 02 01 02 64 13 04 05 63 6e 3d c3 a9 30 0a 30 08 04 01 62 31 03 04 01 32 30 19 02 01 02 64 14 04 06 20 63 6e 3d 73 70 30 0a 30 08 04 01 63 31 03 04 01 33 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN:\n\ta: 1\n\n\nDN: cn=\u00c3\u00a9\n\tb: 2\n\n\nDN:  cn=sp\n\tc: 3\n\n\n")]
    [DataRow("empty-linux", "30 20 02 01 02 64 1b 04 04 63 6e 3d 65 30 13 30 07 04 01 65 31 02 04 00 30 08 04 01 66 31 03 04 01 78 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=e\n\te:\n\n\tf: x\n\n\n")]
    [DataRow("emptybin-linux", "30 2a 02 01 02 64 25 04 05 63 6e 3d 65 62 30 1c 30 0e 04 08 78 3b 62 69 6e 61 72 79 31 02 04 00 30 0a 04 01 79 31 05 04 00 04 01 00 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=eb\n\tx;binary::\n\n\ty:\n\ty:: AA==\n\n\n")]
    [DataRow("long-linux", "30 81 c7 02 01 02 64 81 c1 04 04 63 6e 3d 6c 30 81 b8 30 6e 04 04 6c 6f 6e 67 31 66 04 64 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 78 30 46 04 04 6c 62 69 6e 31 3e 04 3c 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=l\n\tlong: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\n\n\tlbin:: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n\n\n")]
    [DataRow("manyattrsnone-linux", "30 15 02 01 02 64 10 04 04 63 6e 3d 7a 30 08 30 06 04 02 6e 76 31 00 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=z\n\tnv:\n\n")]
    [DataRow("names-linux", "30 23 02 01 02 64 1e 04 05 63 6e 3d c4 80 30 15 30 09 04 02 c3 a9 31 03 04 01 76 30 08 04 01 61 31 03 04 01 31 30 17 02 01 02 64 12 04 04 63 6e 3d ff 30 0a 30 08 04 01 62 31 03 04 01 32 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=\u00c4\u0080\n\t\u00c3\u00a9: v\n\n\ta: 1\n\n\nDN: cn=\u00ff\n\tb: 2\n\n\n")]
    [DataRow("noattr-linux", "30 0d 02 01 02 64 08 04 04 63 6e 3d 63 30 00 30 18 02 01 02 64 13 04 04 63 6e 3d 64 30 0b 30 09 04 02 73 6e 31 03 04 01 7a 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=c\n\nDN: cn=d\n\tsn: z\n\n\n")]
    [DataRow("novals-linux", "30 1f 02 01 02 64 1a 04 04 63 6e 3d 6e 30 12 30 06 04 02 6e 76 31 00 30 08 04 01 66 31 03 04 01 78 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=n\n\tnv:\n\tf: x\n\n\n")]
    [DataRow("print-linux", "30 81 b3 02 01 02 64 81 ad 04 04 63 6e 3d 70 30 81 a4 30 0b 04 03 62 69 6e 31 04 04 02 00 ff 30 0c 04 04 6c 65 61 64 31 04 04 02 20 61 30 0d 04 05 74 72 61 69 6c 31 04 04 02 61 20 30 0c 04 03 74 61 62 31 05 04 03 61 09 62 30 0c 04 04 6c 74 61 62 31 04 04 02 09 61 30 0c 04 04 75 74 66 38 31 04 04 02 c3 a9 30 0a 04 02 68 69 31 04 04 02 61 e9 30 0c 04 03 63 74 6c 31 05 04 03 61 01 62 30 0b 04 03 64 65 6c 31 04 04 02 61 7f 30 0b 04 02 6e 6c 31 05 04 03 61 0a 62 30 0b 04 02 63 72 31 05 04 03 61 0d 62 30 0d 04 03 73 79 6d 31 06 04 04 7e 21 3a 3c 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=p\n\tbin:: AP8=\n\n\tlead:: IGE=\n\n\ttrail:: YSA=\n\n\ttab: a\tb\n\n\tltab:: CWE=\n\n\tutf8:: w6k=\n\n\thi:: Yek=\n\n\tctl:: YQFi\n\n\tdel:: YX8=\n\n\tnl: a\nb\n\n\tcr: a\rb\n\n\tsym: ~!:<\n\n\n")]
    [DataRow("ref-linux", "30 18 02 01 02 64 13 04 05 63 6e 3d 72 31 30 0a 30 08 04 01 61 31 03 04 01 31 30 25 02 01 02 73 20 04 11 6c 64 61 70 3a 2f 2f 6f 74 68 65 72 2f 64 63 3d 78 04 0b 6c 64 61 70 3a 2f 2f 74 77 6f 2f 30 18 02 01 02 64 13 04 05 63 6e 3d 72 32 30 0a 30 08 04 01 62 31 03 04 01 32 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=r1\n\ta: 1\n\n\n")]
    [DataRow("size-linux", "30 17 02 01 02 64 12 04 04 63 6e 3d 73 30 0a 30 08 04 01 61 31 03 04 01 31 30 0c 02 01 02 65 07 0a 01 04 04 00 04 00", "DN: cn=s\n\ta: 1\n\n\n")]
    [DataRow("two-linux", "30 35 02 01 02 64 30 04 0f 63 6e 3d 61 2c 64 63 3d 65 78 61 6d 70 6c 65 30 1d 30 0c 04 02 63 6e 31 06 04 01 61 04 01 62 30 0d 04 04 6d 61 69 6c 31 05 04 03 78 40 79 30 23 02 01 02 64 1e 04 0f 63 6e 3d 62 2c 64 63 3d 65 78 61 6d 70 6c 65 30 0b 30 09 04 02 73 6e 31 03 04 01 7a 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=a,dc=example\n\tcn: a\n\tcn: b\n\n\tmail: x@y\n\n\nDN: cn=b,dc=example\n\tsn: z\n\n\n")]
    [DataRow("ws-linux", "30 81 86 02 01 02 64 81 80 04 04 63 6e 3d 77 30 78 30 0b 04 03 6c 6e 6c 31 04 04 02 0a 61 30 0b 04 03 74 6e 6c 31 04 04 02 61 0a 30 0b 04 03 6c 63 72 31 04 04 02 0d 61 30 0b 04 02 76 74 31 05 04 03 61 0b 62 30 0b 04 02 66 66 31 05 04 03 61 0c 62 30 0c 04 03 6e 75 6c 31 05 04 03 61 00 62 30 09 04 02 73 70 31 03 04 01 20 30 0e 04 07 74 61 62 6f 6e 6c 79 31 03 04 01 09 30 0c 04 03 6d 69 64 31 05 04 03 61 20 62 30 0c 02 01 02 65 07 0a 01 00 04 00 04 00", "DN: cn=w\n\tlnl: \na\n\n\ttnl: a\n\n\n\tlcr: \ra\n\n\tvt: a\u000bb\n\n\tff: a\u000cb\n\n\tnul:: YQBi\n\n\tsp:: IA==\n\n\ttabonly:: CQ==\n\n\tmid: a b\n\n\n")]
    public async Task ExecuteAsync_OpenLdapEntries_WritesWhatOpenLdapWrote(string recording, string replies, string expected)
    {
        (TransferResult result, _, byte[] output) = await RunSearchWritingAsync(LdapDialect.OpenLdap, replies);

        Assert.AreEqual(TransferResult.Success(expected.Length), result, recording);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(expected), output, recording);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapSearchFailsAfterAnEntry_WritesNothing()
    {
        (TransferResult result, byte[] sent, byte[] output) = await RunSearchWritingAsync(LdapDialect.WinLdap, EntryF + " " + SearchNoSuchObject);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapSearchFailed, "LDAP remote: No Such Object"), result);
        Assert.AreEqual(0, output.Length);
        CollectionAssert.AreEqual(Hex.Bytes(WinLdapBindCnU + " " + WinLdapSearchX + " " + WinLdapUnbind3), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapSearchFailsAfterAnEntry_HasWrittenTheEntry()
    {
        (TransferResult result, byte[] sent, byte[] output) = await RunSearchWritingAsync(LdapDialect.OpenLdap, EntryF + " " + SearchNoSuchObject);

        Assert.AreEqual(new TransferResult(CurlExitCode.LdapSearchFailed, 17, "LDAP remote: search failed No such object "), result);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("DN: cn=f\n\ta: 1\n\n\n"), output);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBindCnU + " " + OpenLdapSearchX + " " + OpenLdapUnbind3), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenLdapServerClosesAfterAnEntry_HasWrittenTheEntry()
    {
        (TransferResult result, _, byte[] output) = await RunSearchWritingAsync(LdapDialect.OpenLdap, EntryF);

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 17, "LDAP local: search ldap_result Can't contact LDAP server"), result);
        Assert.AreEqual(17, output.Length);
    }

    [TestMethod]
    [DataRow("30 0a 02 01 02 64 05 04 03 6e 3d 78")]
    [DataRow("30 10 02 01 02 64 0b 04 03 6e 3d 78 30 04 30 02 04 00")]
    [DataRow("30 12 02 01 02 64 0d 04 03 6e 3d 78 30 06 30 04 04 00 04 00")]
    public async Task ExecuteAsync_OpenLdapEntryWithoutReadableAttributes_AbandonsAndFailsWith56WithoutUnbinding(string entry)
    {
        (TransferResult result, byte[] sent, byte[] output) = await RunSearchWritingAsync(LdapDialect.OpenLdap, EntryF + " " + entry + " " + SearchSuccess);

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 17, "Failure when receiving data from the peer"), result);
        Assert.AreEqual(17, output.Length);
        CollectionAssert.AreEqual(Hex.Bytes(OpenLdapBindCnU + " " + OpenLdapSearchX + " 30 06 02 01 03 50 01 02"), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapEntryWithoutReadableAttributes_WritesItsDnLine()
    {
        (TransferResult result, _, byte[] output) = await RunSearchWritingAsync(LdapDialect.WinLdap, "30 10 02 01 02 64 0b 04 03 6e 3d 78 30 04 30 02 04 00 " + SearchSuccess);

        Assert.AreEqual(TransferResult.Success(8), result);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("DN: n=x\n"), output);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap, CurlExitCode.LdapSearchFailed, "LDAP remote: Server Down")]
    [DataRow(LdapDialect.OpenLdap, CurlExitCode.RecvError, "LDAP local: search ldap_result Can't contact LDAP server")]
    public async Task ExecuteAsync_EntryWithoutADn_FailsAsALostServer(LdapDialect dialect, CurlExitCode exitCode, string message)
    {
        (TransferResult result, _, byte[] output) = await RunSearchWritingAsync(dialect, "30 07 02 01 02 64 02 30 00 " + SearchSuccess);

        Assert.AreEqual(TransferResult.Failure(exitCode, message), result);
        Assert.AreEqual(0, output.Length);
    }

    /// <summary>Runs <paramref name="dialect" />'s handler on <c>ldap://127.0.0.1:38901/x</c>, bound, with <paramref name="replies" /> answering the search.</summary>
    private static async Task<(TransferResult Result, byte[] Sent, byte[] Output)> RunSearchWritingAsync(LdapDialect dialect, string replies)
    {
        var connection = new ScriptedConnection([Hex.Bytes(BindSuccess1), Hex.Bytes(replies)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), dialect);
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:38901/x"),
            Output = output,
            Credentials = new NetworkCredential("cn=u", "p"),
        };

        TransferResult result = await handler.ExecuteAsync(context);

        return (result, connection.Sent, output.ToArray());
    }
}
