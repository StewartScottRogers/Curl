using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An SSH server that lives in memory, ADR-0122's <c>InMemorySshServer</c>: an
/// <see cref="IConnector" /> whose every connection is answered by a new
/// <see cref="InMemorySshServerSession" /> over an <see cref="InMemoryDuplexConnection" />.
/// It runs a real session, built from the library's own transport and packet protection in
/// the server role: <c>diffie-hellman-group14-sha256</c> with an <c>rsa-sha2-256</c> host key
/// (both in the Windows and the OpenSSL presets), <see cref="Cipher" /> (<c>aes128-ctr</c>
/// unless set) and <see cref="Mac" /> (<c>hmac-sha2-256</c> unless set);
/// <c>password</c> and <c>publickey</c> authentication; one <c>session</c> channel running
/// either the <c>sftp</c> subsystem or <c>scp -pf</c>, serving <see cref="Files" /> and, over
/// SFTP, listing the files directly under a directory path that ends with a slash and
/// storing an upload's writes in <see cref="Files" />, an open with <c>SSH_FXF_CREAT</c>
/// creating a missing file.
/// </summary>
/// <remarks>
/// Public so that another test project, such as <c>Curl.Console.UnitTests</c>, can reference
/// this one and run a whole <c>scp://</c> or <c>sftp://</c> transfer through it. Every
/// member takes and returns BCL or <c>Curl.Protocol.Abstractions</c> types only.
/// </remarks>
/// <param name="userName">The only user the server knows.</param>
/// <param name="password">That user's password.</param>
public sealed class InMemorySshServer(string userName, string password) : IConnector
{
    /// <summary>The server's identification string.</summary>
    public const string Identification = "SSH-2.0-OpenSSH_9.7";

    /// <summary>The home directory <c>REALPATH .</c> answers.</summary>
    public const string DefaultHomeDirectory = "/home/fake";

    private readonly ConcurrentQueue<string> events = new();

    private readonly ConcurrentQueue<ConnectTarget> targets = new();

    private readonly ConcurrentQueue<Task> sessions = new();

    /// <summary>Gets the only user the server knows.</summary>
    public string UserName { get; } = userName;

    /// <summary>Gets that user's password, compared with the bytes sent as UTF-8.</summary>
    public string Password { get; } = password;

    /// <summary>Gets the files served, by the absolute path SFTP opens and <c>scp -pf</c> names.</summary>
    public IDictionary<string, byte[]> Files { get; } = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);

    /// <summary>Gets the home directory <c>REALPATH .</c> answers; <see cref="DefaultHomeDirectory" /> by default.</summary>
    public string HomeDirectory { get; init; } = DefaultHomeDirectory;

    /// <summary>
    /// Gets the public key blob <c>publickey</c> accepts for <see cref="UserName" />, or
    /// <see langword="null" />, the default, to refuse every key.
    /// </summary>
    public byte[]? AuthorizedPublicKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server refuses every <c>session</c> channel with
    /// reason 2, <c>connect failed</c>, as OpenSSH does under <c>MaxSessions 0</c>.
    /// </summary>
    public bool RefusesSessionChannels { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server hangs up the moment a channel is opened,
    /// before answering it.
    /// </summary>
    public bool HangsUpOnChannelOpen { get; init; }

    /// <summary>
    /// Gets the one cipher the server offers in both directions, beside
    /// <see cref="Mac" />, which an AEAD cipher leaves unused: <c>aes128-ctr</c> by default.
    /// </summary>
    public string Cipher { get; init; } = "aes128-ctr";

    /// <summary>
    /// Gets the one MAC the server offers in both directions: <c>hmac-sha2-256</c> by default.
    /// </summary>
    public string Mac { get; init; } = "hmac-sha2-256";

    /// <summary>
    /// Gets the one compression method the server offers in both directions: <c>none</c> by
    /// default, or <c>zlib</c> from its <c>NEWKEYS</c> or <c>zlib@openssh.com</c> from its
    /// <c>SSH_MSG_USERAUTH_SUCCESS</c>, as OpenSSH compresses.
    /// </summary>
    public string Compression { get; init; } = "none";

    /// <summary>Gets the server's host key blob, <c>K_S</c>, an <c>ssh-rsa</c> key.</summary>
    public byte[] HostKeyBlob => HostKey.Blob;

    /// <summary>Gets the SHA-256 fingerprint of the host key in base64 without padding, as <c>--hostpubsha256</c> takes it.</summary>
    public string HostKeySha256 => Convert.ToBase64String(SHA256.HashData(HostKeyBlob)).TrimEnd('=');

    /// <summary>
    /// Gets what the sessions did, in order, one line per event: <c>auth password tester
    /// ok</c>, <c>subsystem sftp</c>, <c>exec scp -pf '/f'</c>, <c>open /f</c>, <c>channel
    /// eof</c>, <c>channel close</c>, <c>disconnect 11 Shutdown</c> and the like.
    /// </summary>
    public IReadOnlyList<string> Events => [.. events];

    /// <summary>Gets every target a connection was asked for, in order.</summary>
    public IReadOnlyList<ConnectTarget> Targets => [.. targets];

    internal TestHostKey HostKey { get; } = TestHostKey.Rsa("rsa-sha2-256", HashAlgorithmName.SHA256);

    /// <summary>
    /// Builds the <c>known_hosts</c> line that accepts this server's host key for
    /// <paramref name="host" />.
    /// </summary>
    /// <param name="host">The host name, or <c>[host]:port</c>.</param>
    /// <returns>The line, with its line end.</returns>
    public string KnownHostsLine(string host) => $"{host} ssh-rsa {Convert.ToBase64String(HostKeyBlob)}\n";

    /// <summary>
    /// Waits until every session started so far has ended, so its <see cref="Events" /> are
    /// all recorded.
    /// </summary>
    /// <returns>A task that completes when they have.</returns>
    public Task WhenSessionsEndAsync() => Task.WhenAll(sessions);

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        targets.Enqueue(target);
        (InMemoryDuplexConnection client, InMemoryDuplexConnection server) = InMemoryDuplexConnection.CreatePair();
        InMemorySshServerSession session = new(this, server);
        sessions.Enqueue(Task.Run(session.RunAsync, CancellationToken.None));
        return ValueTask.FromResult(ConnectResult.Connected(client));
    }

    /// <summary>Records one event.</summary>
    internal void Record(string text) => events.Enqueue(text);

    /// <summary>Gets whether <paramref name="user" /> and <paramref name="passwordBytes" /> are the known user's.</summary>
    internal bool IsPassword(byte[] user, byte[] passwordBytes) =>
        user.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(UserName)) && passwordBytes.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Password));
}
