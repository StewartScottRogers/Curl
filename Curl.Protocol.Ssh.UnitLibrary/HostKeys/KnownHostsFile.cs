using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// An OpenSSH known-hosts file as libssh2 1.11.1 reads it for curl 8.21.0
/// (<c>libssh2_knownhost_readfile</c>), and the two lookups curl makes in it: the entry
/// that narrows the host-key list before the handshake, and the check of the server's
/// host key after it. Measured 2026-09-29 against the Windows reference build (BL-566).
/// </summary>
/// <remarks>
/// <para>
/// A line is <c>names keytype key [comment]</c>. Blank lines and lines starting with
/// <c>#</c> are skipped. <c>names</c> is a comma-separated list of plain names, each one
/// entry, or one hashed name <c>|1|salt|hash</c>. A marker line, <c>@revoked</c> or
/// <c>@cert-authority</c>, is read as an entry for a host named after the marker, so it
/// names no real host: a revoked key alone is not found, and a plain entry for the same
/// key beside it still matches.
/// </para>
/// <para>
/// The first line libssh2 cannot parse (no key, or fewer than 20 characters after the
/// names, or a hashed name whose base64 is broken) ends the reading: entries before it
/// stay and every line after it is dropped. A line's trailing CR is removed, as the
/// Windows build's text-mode read removes it.
/// </para>
/// </remarks>
internal sealed class KnownHostsFile
{
    private const string HashedNamePrefix = "|1|";

    private const int ShortestKeyPart = 20;

    private static readonly char[] Whitespace = [' ', '\t'];

    private readonly List<KnownHostsEntry> entries;

    private KnownHostsFile(List<KnownHostsEntry> entries) => this.entries = entries;

    /// <summary>
    /// Gets the entries read, in libssh2's order: lines top to bottom, and a line's plain
    /// names last to first.
    /// </summary>
    internal IReadOnlyList<KnownHostsEntry> Entries => entries;

    /// <summary>
    /// Reads a known-hosts file's text.
    /// </summary>
    /// <param name="text">The file's content.</param>
    /// <returns>The entries up to the first line that cannot be parsed.</returns>
    internal static KnownHostsFile Parse(string text)
    {
        List<KnownHostsEntry> entries = [];
        foreach (string line in text.Split('\n'))
        {
            if (!TryReadLine(line.EndsWith('\r') ? line[..^1] : line, entries))
            {
                break;
            }
        }

        return new KnownHostsFile(entries);
    }

    /// <summary>
    /// Opens and reads the known-hosts file at <paramref name="path" />. A file that cannot
    /// be opened reads as empty, as curl only notes it under <c>-v</c> and carries on, so
    /// every host is then not found.
    /// </summary>
    /// <param name="fileSystem">Where the file is opened.</param>
    /// <param name="path">The file's path, from <see cref="SshOptions.KnownHostsPath" />.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file's entries.</returns>
    internal static async ValueTask<KnownHostsFile> LoadAsync(IFileSystem fileSystem, string path, CancellationToken cancellationToken)
    {
        FileOpenResult opened = await fileSystem.OpenForReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (opened.Content is not { } content)
        {
            return new KnownHostsFile([]);
        }

        await using (content.ConfigureAwait(false))
        {
            using StreamReader reader = new(content, Encoding.UTF8);
            return Parse(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false));
        }
    }

    /// <summary>
    /// Finds the entry curl 8.21.0 narrows the host-key list by: the first entry that is
    /// hashed (whatever host it hashes, as curl cannot read a hashed name), is plain and
    /// equals <paramref name="host" />, or is <c>[name]:port</c> with this port and a
    /// <c>name</c> <paramref name="host" /> starts with (curl compares only the bracketed
    /// name's length). A <c>[</c> entry without <c>]:</c> is skipped.
    /// </summary>
    /// <param name="host">The URL's host name.</param>
    /// <param name="port">The port connected to, 22 included.</param>
    /// <returns>The entry, or <see langword="null" /> when none names the host.</returns>
    internal KnownHostsEntry? FindNarrowingEntry(string host, int port) =>
        entries.FirstOrDefault(entry => entry.PlainName is not { } name || NamesHostForNarrowing(name, host, port));

    /// <summary>
    /// Checks the server's host key as <c>libssh2_knownhost_checkp</c> does for curl: on a
    /// port other than 22 the entries are searched for <c>[host]:port</c> first and then
    /// for <c>host</c>; on port 22 for <c>host</c> alone. An entry takes part only when its
    /// key type is the host key's and both are recognized; the first whose key text equals
    /// the host key's base64 is a match, and without one, any that took part is a mismatch.
    /// </summary>
    /// <param name="host">The URL's host name.</param>
    /// <param name="port">The port connected to.</param>
    /// <param name="hostKey">The server's host key blob, <c>K_S</c>.</param>
    /// <returns>The outcome.</returns>
    internal KnownHostsCheck Check(string host, int port, byte[] hostKey)
    {
        KnownHostKeyType type = KnownHostKeyTypeNames.FromName(SshKeyBlobReader.ReadKeyTypeName(hostKey));
        string key = Convert.ToBase64String(hostKey);
        string[] names = port == 22 ? [host] : [$"[{host}]:{port}", host];
        List<KnownHostsEntry> candidates = [.. names.SelectMany(name => entries.Where(entry => entry.Names(name)))
            .Where(entry => type != KnownHostKeyType.Unknown && entry.KeyType == type)];
        return candidates.Count == 0
            ? KnownHostsCheck.NotFound
            : candidates.Any(entry => entry.Key == key) ? KnownHostsCheck.Match : KnownHostsCheck.Mismatch;
    }

    private static bool NamesHostForNarrowing(string name, string host, int port)
    {
        if (!name.StartsWith('['))
        {
            return name == host;
        }

        int end = name.IndexOf("]:", StringComparison.Ordinal);
        return end >= 0 && LeadingNumber(name[(end + 2)..]) == port && host.StartsWith(name[1..end], StringComparison.Ordinal);
    }

    private static int LeadingNumber(string text)
    {
        string digits = new([.. text.TakeWhile(char.IsAsciiDigit)]);
        return int.TryParse(digits, out int number) ? number : 0;
    }

    private static bool TryReadLine(string line, List<KnownHostsEntry> entries)
    {
        string trimmed = line.TrimStart(Whitespace);
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            return true;
        }

        int namesEnd = trimmed.IndexOfAny(Whitespace);
        string keyPart = namesEnd < 0 ? string.Empty : trimmed[namesEnd..].TrimStart(Whitespace);
        if (keyPart.Length < ShortestKeyPart)
        {
            return false;
        }

        (KnownHostKeyType type, string key) = ReadKey(keyPart);
        return TryAddNames(trimmed[..namesEnd], type, key, entries);
    }

    private static (KnownHostKeyType Type, string Key) ReadKey(string keyPart)
    {
        if (char.IsAsciiDigit(keyPart[0]))
        {
            return (KnownHostKeyType.Rsa1, keyPart);
        }

        int typeEnd = keyPart.IndexOfAny(Whitespace);
        string typeName = typeEnd < 0 ? keyPart : keyPart[..typeEnd];
        string rest = typeEnd < 0 ? string.Empty : keyPart[typeEnd..].TrimStart(Whitespace);
        int keyEnd = rest.IndexOfAny(Whitespace);
        return (KnownHostKeyTypeNames.FromName(typeName), keyEnd < 0 ? rest : rest[..keyEnd]);
    }

    private static bool TryAddNames(string names, KnownHostKeyType type, string key, List<KnownHostsEntry> entries)
    {
        if (!names.StartsWith(HashedNamePrefix, StringComparison.Ordinal))
        {
            entries.AddRange(names.Split(',').Reverse().Select(name => new KnownHostsEntry(name, [], [], type, key)));
            return true;
        }

        string saltAndHash = names[HashedNamePrefix.Length..];
        int separator = saltAndHash.IndexOf('|', StringComparison.Ordinal);
        if (separator < 0)
        {
            return true;
        }

        if (!Libssh2Base64.TryDecode(saltAndHash[..separator], out byte[]? salt)
            || !Libssh2Base64.TryDecode(saltAndHash[(separator + 1)..], out byte[]? hash))
        {
            return false;
        }

        entries.Add(new KnownHostsEntry(null, salt!, hash!, type, key));
        return true;
    }
}
