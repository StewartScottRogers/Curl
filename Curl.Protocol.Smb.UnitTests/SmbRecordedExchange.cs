namespace Curl.Protocol.Smb;

/// <summary>
/// The bytes of one SMB session, measured on 2026-09-29 with <c>Record-CurlExchange.ps1
/// -Script</c> running Ubuntu's curl 8.18.0 (OpenSSL, WSL) against the recorder:
/// <c>curl -u User:Password smb://172.26.96.1:14450/share/x.txt</c>, and the same with
/// <c>-u DOM\Us:pw</c>. The server's side was hand-assembled from <c>lib/smb.c</c>'s
/// structures (ADR-0200): session key <c>0x12345678</c>, challenge
/// <c>0123456789abcdef</c> (MS-NLMP 4.2.1's), UID <c>0x0064</c>.
/// </summary>
internal static class SmbRecordedExchange
{
    /// <summary>The host the measured URL named, sent as the domain when the user has none.</summary>
    public const string Host = "172.26.96.1";

    /// <summary>curl's SMB_COM_NEGOTIATE, the first bytes it sends.</summary>
    public static byte[] NegotiateRequest => Hex(
        "00 00 00 2f ff 53 4d 42 72 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 00 00 1d d7 00 00 00 00 "
        + "00 0c 00 02 4e 54 20 4c 4d 20 30 2e 31 32 00");

    /// <summary>The server's negotiate response: dialect 0, session key and challenge as above.</summary>
    public static byte[] NegotiateResponse => Hex(
        "00 00 00 4d ff 53 4d 42 72 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 "
        + "11 00 00 03 01 00 01 00 00 40 00 00 00 00 01 00 78 56 34 12 fd e3 00 00 00 00 00 00 00 00 00 00 00 00 08 "
        + "08 00 01 23 45 67 89 ab cd ef");

    /// <summary>curl's SMB_COM_SESSION_SETUP_ANDX for <c>User:Password</c>, domain <see cref="Host" />.</summary>
    public static byte[] SessionSetupRequest => Hex(
        "00 00 00 97 ff 53 4d 42 73 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 00 00 1d d7 00 00 00 00 "
        + "0d ff 00 00 00 00 90 01 00 01 00 78 56 34 12 18 00 18 00 00 00 00 00 08 00 00 00 5a 00 "
        + "98 de f7 b8 7f 88 aa 5d af e2 df 77 96 88 a1 72 de f1 1c 7d 5c cd ef 13 "
        + "67 c4 30 11 f3 02 98 a2 ad 35 ec e6 4f 16 33 1c 44 bd be d9 27 84 1f 94 "
        + "55 73 65 72 00 31 37 32 2e 32 36 2e 39 36 2e 31 00 78 38 36 5f 36 34 2d 70 63 2d 6c 69 6e 75 78 2d 67 6e 75 00 "
        + "63 75 72 6c 00");

    /// <summary>curl's SMB_COM_SESSION_SETUP_ANDX for <c>DOM\Us:pw</c>.</summary>
    public static byte[] DomainSessionSetupRequest => Hex(
        "00 00 00 8d ff 53 4d 42 73 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 00 00 1d d7 00 00 00 00 "
        + "0d ff 00 00 00 00 90 01 00 01 00 78 56 34 12 18 00 18 00 00 00 00 00 08 00 00 00 50 00 "
        + "1d 71 ae 6c 87 f7 76 28 51 7f 08 7f 82 ca 1c 3c 5f 32 31 38 4d 87 93 88 "
        + "30 3e 9b 61 4a 4e 2f 41 99 50 f5 ca 1f 9e bf 53 56 e6 c4 6a 19 ca 2c ec "
        + "55 73 00 44 4f 4d 00 78 38 36 5f 36 34 2d 70 63 2d 6c 69 6e 75 78 2d 67 6e 75 00 63 75 72 6c 00");

    /// <summary>The server's session setup response accepting the session, UID <c>0x0064</c>.</summary>
    public static byte[] SessionSetupAccepted => Hex(
        "00 00 00 29 ff 53 4d 42 73 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 64 00 00 00 "
        + "03 ff 00 00 00 00 00 00 00");

    /// <summary>The server's session setup response refusing it, STATUS_LOGON_FAILURE; curl exits 67.</summary>
    public static byte[] SessionSetupRefused => Hex(
        "00 00 00 23 ff 53 4d 42 73 6d 00 00 c0 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 "
        + "00 00 00");

    /// <summary>The server's negotiate response refusing it, STATUS_ACCESS_DENIED; curl exits 7.</summary>
    public static byte[] NegotiateRefused => Hex(
        "00 00 00 23 ff 53 4d 42 72 22 00 00 c0 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 "
        + "00 00 00");

    /// <summary>Decodes space-separated hex pairs.</summary>
    /// <param name="hex">The pairs.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));
}
