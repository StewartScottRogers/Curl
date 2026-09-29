namespace Curl.Protocol.Smb;

/// <summary>
/// The bytes of one SMB session, measured on 2026-09-29 with <c>Record-CurlExchange.ps1
/// -Script</c> running Ubuntu's curl 8.18.0 (OpenSSL, WSL) against the recorder:
/// <c>curl -u User:Password smb://172.26.96.1:14450/share/x.txt</c>, and the same with
/// <c>-u DOM\Us:pw</c>. The server's side was hand-assembled from <c>lib/smb.c</c>'s
/// structures (ADR-0200): session key <c>0x12345678</c>, challenge
/// <c>0123456789abcdef</c> (MS-NLMP 4.2.1's), UID <c>0x0064</c>. The download that
/// follows (BL-596) was measured the same way, with TID <c>0x0007</c> and FID <c>0x4001</c>,
/// and so was the upload (BL-597).
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

    /// <summary>
    /// The URL of the download measured on 2026-09-29 (BL-596): the same curl and recorder,
    /// <c>curl -u User:Password smb://172.26.96.1:14450/share/dir/x.txt</c>.
    /// </summary>
    public const string DownloadUrl = "smb://" + Host + "/share/dir/x.txt";

    /// <summary>The file's bytes: <c>hello world</c>.</summary>
    public const string FileContent = "hello world";

    /// <summary>The file's last change time the open response carries.</summary>
    public static readonly DateTimeOffset FileLastChangeTimeUtc = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>curl's SMB_COM_TREE_CONNECT_ANDX to <c>\\172.26.96.1\share</c>, UID 0x64.</summary>
    public static byte[] TreeConnectRequest => Hex(
        "00 00 00 45 ff 53 4d 42 75 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 00 00 1d d7 64 00 00 00 "
        + "04 ff 00 00 00 00 00 00 00 1a 00 5c 5c 31 37 32 2e 32 36 2e 39 36 2e 31 5c 73 68 61 72 65 00 3f 3f 3f 3f 3f 00");

    /// <summary>The server's tree connect response accepting it, TID 0x0007.</summary>
    public static byte[] TreeConnectAccepted => Hex(
        "00 00 00 2c ff 53 4d 42 75 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 "
        + "03 ff 00 00 00 01 00 03 00 41 3a 00");

    /// <summary>The server's tree connect response refusing it, STATUS_BAD_NETWORK_NAME; curl exits 78.</summary>
    public static byte[] TreeConnectMissingShare => Hex(
        "00 00 00 23 ff 53 4d 42 75 cc 00 00 c0 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 64 00 00 00 00 00 00");

    /// <summary>The server's tree connect response refusing it with the DOS error ERRnoaccess; curl exits 9.</summary>
    public static byte[] TreeConnectNoAccess => Hex(
        "00 00 00 23 ff 53 4d 42 75 01 00 05 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 64 00 00 00 00 00 00");

    /// <summary>curl's SMB_COM_NT_CREATE_ANDX opening <c>dir\x.txt</c> for reading.</summary>
    public static byte[] OpenRequest => Hex(
        "00 00 00 5d ff 53 4d 42 a2 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 "
        + "18 ff 00 00 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 80 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 "
        + "01 00 00 00 00 00 00 00 00 00 00 00 00 0a 00 64 69 72 5c 78 2e 74 78 74 00");

    /// <summary>The server's open response for the 11-byte file, FID 0x4001, changed at <see cref="FileLastChangeTimeUtc" />.</summary>
    public static byte[] OpenAccepted => Hex(
        "00 00 00 67 ff 53 4d 42 a2 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 "
        + "22 ff 00 00 00 00 01 40 01 00 00 00 80 00 40 74 94 7b dc 01 80 00 40 74 94 7b dc 01 80 00 40 74 94 7b dc 01 "
        + "80 00 40 74 94 7b dc 01 80 00 00 00 0b 00 00 00 00 00 00 00 0b 00 00 00 00 00 00 00 00 00 00 00 00 00 00");

    /// <summary>The server's open response for a directory, FID 0x4001, size 0.</summary>
    public static byte[] OpenDirectory => Hex(
        "00 00 00 67 ff 53 4d 42 a2 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 "
        + "22 ff 00 00 00 00 01 40 01 00 00 00 80 00 40 74 94 7b dc 01 80 00 40 74 94 7b dc 01 80 00 40 74 94 7b dc 01 "
        + "80 00 40 74 94 7b dc 01 10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 00");

    /// <summary>The server's open response refusing it, STATUS_OBJECT_NAME_NOT_FOUND; curl exits 78.</summary>
    public static byte[] OpenMissingFile => Hex(
        "00 00 00 23 ff 53 4d 42 a2 34 00 00 c0 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 00 00 00");

    /// <summary>curl's SMB_COM_READ_ANDX for up to 0x8000 bytes at offset 0 of FID 0x4001.</summary>
    public static byte[] ReadRequest => Hex(
        "00 00 00 3b ff 53 4d 42 2e 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 "
        + "0c ff 00 00 00 01 40 00 00 00 00 00 80 00 80 00 00 00 00 00 00 00 00 00 00 00 00");

    /// <summary>The server's read response carrying <see cref="FileContent" />.</summary>
    public static byte[] ReadAccepted => Hex(
        "00 00 00 46 ff 53 4d 42 2e 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 "
        + "0c ff 00 00 00 ff ff 00 00 00 00 0b 00 3b 00 00 00 00 00 00 00 00 00 00 00 0b 00 68 65 6c 6c 6f 20 77 6f 72 6c 64");

    /// <summary>The server's read response refusing a read of a directory, STATUS_INVALID_DEVICE_REQUEST; curl exits 56.</summary>
    public static byte[] ReadRefused => Hex(
        "00 00 00 23 ff 53 4d 42 2e 10 00 00 c0 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 00 00 00");

    /// <summary>curl's SMB_COM_CLOSE of FID 0x4001.</summary>
    public static byte[] CloseRequest => Hex(
        "00 00 00 29 ff 53 4d 42 04 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 "
        + "03 01 40 00 00 00 00 00 00");

    /// <summary>The server's close response.</summary>
    public static byte[] CloseAccepted => Hex(
        "00 00 00 23 ff 53 4d 42 04 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 00 00 00");

    /// <summary>curl's SMB_COM_TREE_DISCONNECT from TID 0x0007.</summary>
    public static byte[] TreeDisconnectRequest => Hex(
        "00 00 00 23 ff 53 4d 42 71 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 00 00 00");

    /// <summary>The server's tree disconnect response.</summary>
    public static byte[] TreeDisconnectAccepted => Hex(
        "00 00 00 23 ff 53 4d 42 71 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 00 00 00");

    /// <summary>
    /// The upload measured on 2026-09-29 (BL-597): the same curl and recorder,
    /// <c>curl -u User:Password -T up.txt smb://172.26.96.1:14450/share/dir/x.txt</c>,
    /// <c>up.txt</c> holding <see cref="FileContent" />.
    /// </summary>
    public const string UploadUrl = DownloadUrl;

    /// <summary>curl's SMB_COM_NT_CREATE_ANDX opening <c>dir\x.txt</c> for an upload: GENERIC_READ | GENERIC_WRITE, FILE_OVERWRITE_IF.</summary>
    public static byte[] UploadOpenRequest => Hex(
        "00 00 00 5d ff 53 4d 42 a2 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 "
        + "18 ff 00 00 00 00 09 00 00 00 00 00 00 00 00 00 00 00 00 c0 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 "
        + "05 00 00 00 00 00 00 00 00 00 00 00 00 0a 00 64 69 72 5c 78 2e 74 78 74 00");

    /// <summary>The server's open response for a file it created, FID 0x4001, create action FILE_CREATED.</summary>
    public static byte[] UploadOpenCreated => Hex(
        "00 00 00 67 ff 53 4d 42 a2 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 "
        + "22 ff 00 00 00 00 01 40 02 00 00 00 80 00 40 74 94 7b dc 01 80 00 40 74 94 7b dc 01 80 00 40 74 94 7b dc 01 "
        + "80 00 40 74 94 7b dc 01 80 00 00 00 0b 00 00 00 00 00 00 00 0b 00 00 00 00 00 00 00 00 00 00 00 00 00 00");

    /// <summary>The server's open response refusing a read-only share, STATUS_ACCESS_DENIED; curl exits 78.</summary>
    public static byte[] OpenAccessDenied => Hex(
        "00 00 00 23 ff 53 4d 42 a2 22 00 00 c0 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 00 00 00");

    /// <summary>curl's SMB_COM_WRITE_ANDX of <see cref="FileContent" /> at offset 0 of FID 0x4001.</summary>
    public static byte[] WriteRequest => Hex(
        "00 00 00 4b ff 53 4d 42 2f 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 "
        + "0e ff 00 00 00 01 40 00 00 00 00 00 00 00 00 00 00 00 00 00 00 0b 00 40 00 00 00 00 00 0c 00 00 "
        + "68 65 6c 6c 6f 20 77 6f 72 6c 64");

    /// <summary>curl's SMB_COM_WRITE_ANDX uploading an empty file: no data, byte count 1 for the padding.</summary>
    public static byte[] EmptyWriteRequest => Hex(
        "00 00 00 40 ff 53 4d 42 2f 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 "
        + "0e ff 00 00 00 01 40 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 40 00 00 00 00 00 01 00 00");

    /// <summary>
    /// The first 68 bytes of curl's two writes of a 40000-byte file: 0x7fff bytes at 0, then
    /// 0x1c41 at 0x7fff.
    /// </summary>
    public static byte[][] LargeWriteHeaders =>
    [
        Hex("00 00 80 3f ff 53 4d 42 2f 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 "
            + "0e ff 00 00 00 01 40 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 7f 40 00 00 00 00 00 00 80 00"),
        Hex("00 00 1c 81 ff 53 4d 42 2f 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 "
            + "0e ff 00 00 00 01 40 ff 7f 00 00 00 00 00 00 00 00 00 00 00 00 41 1c 40 00 00 00 00 00 42 1c 00"),
    ];

    /// <summary>
    /// curl's second write when the server said it wrote only 5 of the 11 bytes: 6 bytes
    /// declared at offset 5, none sent, the source being at its end.
    /// </summary>
    public static byte[] ShortWriteRequest => Hex(
        "00 00 00 46 ff 53 4d 42 2f 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00 00 00 00 00 07 00 1d d7 64 00 00 00 "
        + "0e ff 00 00 00 01 40 05 00 00 00 00 00 00 00 00 00 00 00 00 00 06 00 40 00 00 00 00 00 07 00 00");

    /// <summary>The server's write response saying it wrote <paramref name="count" /> bytes.</summary>
    /// <param name="count">The bytes written, as the response's count word.</param>
    /// <returns>The response.</returns>
    public static byte[] WriteAccepted(ushort count)
    {
        byte[] response = Hex(
            "00 00 00 2f ff 53 4d 42 2f 00 00 00 00 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 "
            + "06 ff 00 00 00 00 00 ff ff 00 00 00 00 00 00");
        response[41] = (byte)count;
        response[42] = (byte)(count >> 8);
        return response;
    }

    /// <summary>The server's write response refusing it, STATUS_ACCESS_DENIED; curl exits 25.</summary>
    public static byte[] WriteRefused => Hex(
        "00 00 00 23 ff 53 4d 42 2f 22 00 00 c0 98 01 00 00 00 00 00 00 00 00 00 00 00 00 00 07 00 00 00 64 00 00 00 00 00 00");

    /// <summary>A frame curl refuses with exit 56: a NetBIOS length of 1.</summary>
    public static byte[] TooSmallFrame => Hex("00 00 00 01 00");

    /// <summary>Decodes space-separated hex pairs.</summary>
    /// <param name="hex">The pairs.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));
}
