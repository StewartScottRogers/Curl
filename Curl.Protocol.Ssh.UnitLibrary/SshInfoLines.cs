using System.Globalization;

namespace Curl.Protocol.Ssh;

/// <summary>
/// The <c>-v</c> lines curl 8.21.0's libssh2 backend (<c>lib/vssh/libssh2.c</c>) writes
/// for an <c>scp</c> or <c>sftp</c> transfer, as its <c>infof</c> words them, without the
/// leading <c>* </c> (ADR-0262, BL-578).
/// </summary>
internal static class SshInfoLines
{
    /// <summary>The line after the host-key check when <c>-k</c> left no known-hosts file.</summary>
    internal const string NoKnownHostsFile = "SSH: no knownhosts file configured";

    /// <summary>The line for a host key of a type the known-hosts check cannot compare.</summary>
    internal const string UnsupportedHostKeyType = "SSH: unsupported host key type for knownhosts check";

    /// <summary>The line before a known-hosts check refuses the host key.</summary>
    internal const string KnownHostCheckFailed = "SSH: knownhost check failed";

    /// <summary>The line after a known-hosts entry matched the host key.</summary>
    internal const string KnownHostMatches = "SSH: knownhost entry matches host key";

    /// <summary>The line for a server that answered the <c>none</c> request with success.</summary>
    internal const string AcceptedWithoutAuthentication = "SSH: user accepted with no authentication";

    /// <summary>The line after <c>publickey</c> authenticated the user.</summary>
    internal const string AuthenticatedViaPublicKey = "SSH: authenticated via publickey";

    /// <summary>The line after <c>password</c> authenticated the user.</summary>
    internal const string PasswordAuthenticated = "SSH: initialized password authentication";

    /// <summary>The first line of curl's agent attempt, written when the method list names <c>publickey</c>.</summary>
    internal const string TryingAgent = "SSH: trying publickey authentication via agent";

    /// <summary>The agent line when no agent answers, so curl goes on to <c>keyboard-interactive</c>.</summary>
    internal const string AgentConnectFailed = "SSH: failure connecting to agent";

    /// <summary>The agent line when the agent's identities cannot be read (ADR-0271).</summary>
    internal const string AgentIdentitiesFailed = "SSH: failure requesting identities to agent";

    /// <summary>The agent line when no identity authenticated the user, the agent holding none included (ADR-0271).</summary>
    internal const string NoAgentIdentityMatched = "SSH: no agent identity would match";

    /// <summary>The line after <c>keyboard-interactive</c> authenticated the user.</summary>
    internal const string KeyboardInteractiveAuthenticated = "SSH: initialized keyboard interactive authentication";

    /// <summary>The line once the user is authenticated.</summary>
    internal const string AuthenticationComplete = "SSH: authentication complete";

    /// <summary>The <c>scp</c> line after <see cref="AuthenticationComplete" />.</summary>
    internal const string ConnectionEstablished = "SSH: connection established";

    /// <summary>The <c>publickey</c> denial reason when the public key cannot be read, as the WinCNG build gives it.</summary>
    internal const string ReasonUnknown = "Reason unknown (-1)";

    /// <summary>
    /// The OpenSSL build's <c>publickey</c> denial reason when, with no <c>--pubkey</c>, the
    /// private key file does not open (ADR-0262).
    /// </summary>
    internal const string PrivateKeyFileUnopened = "Unable to extract public key from private key file: Unable to open private key file";

    /// <summary>
    /// The OpenSSL build's <c>publickey</c> denial reason when, with no <c>--pubkey</c>, the
    /// private key file opens but cannot be read: a missing or wrong passphrase, a cipher
    /// libssh2 cannot decrypt, or an unknown format (ADR-0262).
    /// </summary>
    internal const string PrivateKeyFileUnrecognized = "Unable to extract public key from private key file: Wrong passphrase or invalid/unrecognized private key file format";

    /// <summary>The <c>publickey</c> denial reason when the server refuses the unsigned question.</summary>
    internal const string PublicKeyCombinationInvalid = "Username/PublicKey combination invalid";

    /// <summary>The <c>publickey</c> denial reason when the private key cannot be read or cannot sign for the public key.</summary>
    internal const string SignCallbackFailed = "Callback returned error";

    /// <summary>The <c>publickey</c> denial reason when the server refuses the signed request.</summary>
    internal const string SignatureRefused = "Invalid signature for supplied public key, or bad username/public key combination";

    /// <summary>The line naming libssh2's cryptography backend.</summary>
    /// <param name="backend">The backend, <c>WinCNG</c> or <c>OpenSSL</c>.</param>
    /// <returns>The line, such as <c>SSH: libssh2 cryptography backend: WinCNG</c>.</returns>
    internal static string CryptographyBackend(string backend) => $"SSH: libssh2 cryptography backend: {backend}";

    /// <summary>The line naming the user.</summary>
    /// <param name="user">The user name, empty when none was given.</param>
    /// <returns>The line, such as <c>SSH: user 'tester'</c>.</returns>
    internal static string User(string user) => $"SSH: user '{user}'";

    /// <summary>The line for a known-hosts file that could not be read to its end.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The line.</returns>
    internal static string KnownHostsReadFailed(string path) => $"SSH: failed to read known hosts from {path}";

    /// <summary>The line for a known-hosts entry that narrows the host-key list.</summary>
    /// <param name="host">The URL's host.</param>
    /// <param name="path">The known-hosts file's path.</param>
    /// <returns>The line.</returns>
    internal static string FoundHost(string host, string path) => $"SSH: found host '{host}' in '{path}'";

    /// <summary>The line after <see cref="FoundHost" /> naming the host-key algorithms offered.</summary>
    /// <param name="methods">The algorithms, comma-separated.</param>
    /// <returns>The line.</returns>
    internal static string HostKeyTypeSet(string methods) => $"SSH: set '{methods}' as hostkey type";

    /// <summary>The line for a known-hosts file with no entry for the host.</summary>
    /// <param name="host">The URL's host.</param>
    /// <param name="path">The known-hosts file's path.</param>
    /// <returns>The line.</returns>
    internal static string DidNotFindHost(string host, string path) => $"SSH: did not find host '{host}' in '{path}'";

    /// <summary>The known-hosts check's outcome.</summary>
    /// <param name="check">libssh2's number for the outcome: <c>0</c> match, <c>1</c> mismatch, <c>2</c> not found.</param>
    /// <param name="knownKey">The entry's base64 key, or <see langword="null" /> when none was found.</param>
    /// <returns>The line, such as <c>SSH: host check 2, key: &lt;none&gt;</c>.</returns>
    internal static string HostCheck(int check, string? knownKey) =>
        string.Create(CultureInfo.InvariantCulture, $"SSH: host check {check}, key: {knownKey ?? "<none>"}");

    /// <summary>The line before the <c>--hostpubsha256</c> comparison.</summary>
    /// <param name="expected">The fingerprint given.</param>
    /// <returns>The line.</returns>
    internal static string Sha256PublicKey(string expected) => $"SSH: SHA256 public key '{expected}'";

    /// <summary>The line naming the host key's SHA-256 fingerprint.</summary>
    /// <param name="fingerprint">The padded base64 fingerprint.</param>
    /// <returns>The line.</returns>
    internal static string Sha256Fingerprint(string fingerprint) => $"SSH: SHA256 fingerprint '{fingerprint}'";

    /// <summary>The line after the SHA-256 fingerprints matched.</summary>
    internal const string Sha256Match = "SSH: SHA256 checksum match";

    /// <summary>The line before the <c>--hostpubmd5</c> comparison.</summary>
    /// <param name="expected">The fingerprint given.</param>
    /// <returns>The line.</returns>
    internal static string Md5PublicKey(string expected) => $"SSH: MD5 public key '{expected}'";

    /// <summary>The line naming the host key's MD5 fingerprint.</summary>
    /// <param name="fingerprint">The fingerprint in lower-case hexadecimal.</param>
    /// <returns>The line.</returns>
    internal static string Md5Fingerprint(string fingerprint) => $"SSH: MD5 fingerprint '{fingerprint}'";

    /// <summary>The line after the MD5 fingerprints matched.</summary>
    internal const string Md5Match = "SSH: MD5 checksum match";

    /// <summary>The line listing the methods the server accepts.</summary>
    /// <param name="methods">The server's list, as sent.</param>
    /// <returns>The line.</returns>
    internal static string OffersAuthentication(string methods) => $"SSH: host offers authentication via: {methods}";

    /// <summary>The agent line when one of its identities authenticated the user (ADR-0271).</summary>
    /// <param name="user">The user name.</param>
    /// <param name="comment">The identity's comment.</param>
    /// <returns>The line.</returns>
    internal static string AgentAuthenticated(string user, string comment) => $"SSH: agent authenticated user '{user}' with key '{comment}'";

    /// <summary>The line naming the <c>--pubkey</c> file.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The line.</returns>
    internal static string TryingPublicKeyFile(string path) => $"SSH: trying public key file '{path}'";

    /// <summary>The line naming the private key file, empty when none was found.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The line.</returns>
    internal static string TryingPrivateKeyFile(string path) => $"SSH: trying private key file '{path}'";

    /// <summary>The line after <c>publickey</c> failed.</summary>
    /// <param name="reason">libssh2's reason, one of the <c>Reason</c>, <c>PublicKey</c>, <c>Sign</c> and <c>Signature</c> constants.</param>
    /// <returns>The line.</returns>
    internal static string PublicKeyDenied(string reason) => $"SSH: publickey authentication denied: {reason}";

    /// <summary>
    /// The line once an upload's every byte is sent: curl's own, after the transfer reads
    /// the source's end, measured 2026-10-01 (BL-988).
    /// </summary>
    /// <param name="bytesSent">The bytes sent.</param>
    /// <returns>
    /// The line, such as <c>upload completely sent off: 13 bytes</c>, or
    /// <c>Request completely sent off</c> for an empty source.
    /// </returns>
    internal static string UploadSent(long bytesSent) =>
        bytesSent == 0
            ? "Request completely sent off"
            : string.Create(CultureInfo.InvariantCulture, $"upload completely sent off: {bytesSent} bytes");

    /// <summary>The line after a transfer that leaves the connection for reuse.</summary>
    /// <param name="connectionNumber">The connection's number.</param>
    /// <param name="host">The URL's host.</param>
    /// <param name="port">The connection's port.</param>
    /// <returns>The line, such as <c>Connection #0 to host 127.0.0.1:22 left intact</c>.</returns>
    internal static string ConnectionLeftIntact(long connectionNumber, string host, int port) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection #{connectionNumber} to host {host}:{port} left intact");

    /// <summary>The line after a failure before the connection was set up, or while the transfer's bytes were moving.</summary>
    /// <param name="connectionNumber">The connection's number.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string ClosingConnection(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");
}
