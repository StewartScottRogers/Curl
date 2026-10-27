using System.Text;

namespace Curl.Authentication;

/// <summary>
/// CGR-074: a .netrc login or password holding bytes outside ASCII is sent as the file's bytes, as
/// curl 8.22.0 sends them, when the file is decoded with the credential encoding and the entry
/// found in it is encoded with that encoding again (measured 2026-10-26, BL-3603).
/// </summary>
[TestClass]
public sealed class Cgr074Tests
{
    /// <summary>A netrc entry whose login is <c>us\xE9r</c> and password <c>p\xE4ss</c> in Latin-1 bytes.</summary>
    private static readonly byte[] Latin1File =
        [.. "machine 127.0.0.1 login us"u8, 0xE9, .. "r password p"u8, 0xE4, .. "ss\n"u8];

    /// <summary>The same entry saved as UTF-8 (<c>c3 a9</c> and <c>c3 a4</c>).</summary>
    private static readonly byte[] Utf8File =
        [.. "machine 127.0.0.1 login us"u8, 0xC3, 0xA9, .. "r password p"u8, 0xC3, 0xA4, .. "ss\n"u8];

    public static IEnumerable<object[]> FilesAndPlatforms =>
    [
        [Latin1File, true, "dXPpcjpw5HNz"],
        [Utf8File, true, "dXPDqXI6cMOkc3M="],
        [Latin1File, false, "dXPpcjpw5HNz"],
        [Utf8File, false, "dXPDqXI6cMOkc3M="],
    ];

    [TestMethod]
    [DynamicData(nameof(FilesAndPlatforms))]
    public void NetrcCredentials_DecodedAndEncodedWithTheCredentialEncoding_AreTheFilesBytes(byte[] file, bool isWindows, string expectedBasic)
    {
        Encoding encoding = CredentialEncoding.ForPlatformGivenSystemAnsiCodePage(isWindows, systemAnsiCodePage: null);

        NetrcLookupResult result = NetrcFile.FindWithReadMode(encoding.GetString(file), "127.0.0.1", userName: null, readsInTextMode: isWindows);

        Assert.AreEqual(NetrcLookupOutcome.Found, result.Outcome);
        Assert.AreEqual(expectedBasic, Convert.ToBase64String(encoding.GetBytes($"{result.Login}:{result.Password}")));
    }
}
