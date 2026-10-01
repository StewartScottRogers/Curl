using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Finds and reads the user's key files for <c>publickey</c> authentication as curl 8.21.0
/// and libssh2 1.11.1 do (ADR-0230): <c>--key</c>, or else the first of
/// <c>$HOME/.ssh/id_rsa</c>, <c>$HOME/.ssh/id_dsa</c>, <c>id_rsa</c> and <c>id_dsa</c> (in
/// the working directory) that exists, or else the empty path, which opens nothing; the
/// public key from <c>--pubkey</c> or derived from the private key; the private key
/// decrypted with <c>--pass</c>, or with an empty passphrase without it.
/// </summary>
/// <param name="fileSystem">Where the key files are opened.</param>
/// <param name="readEnvironmentVariable">
/// Reads an environment variable, <see langword="null" /> when unset: the seam through
/// which <c>HOME</c> is read.
/// </param>
/// <param name="options">The transfer's SSH options.</param>
/// <param name="passphraseEncoding">How <c>--pass</c> becomes bytes, the same as the password's (ADR-0022).</param>
internal sealed class SshUserKeySource(
    IFileSystem fileSystem,
    Func<string, string?> readEnvironmentVariable,
    SshOptions options,
    Encoding passphraseEncoding)
{
    /// <summary>
    /// Resolves which files curl hands libssh2. A default file counts as existing when it
    /// opens for reading.
    /// </summary>
    /// <param name="cancellationToken">Cancels the probes.</param>
    /// <returns>The files.</returns>
    internal async ValueTask<SshUserKeyFiles> LocateAsync(CancellationToken cancellationToken)
    {
        string? publicKeyPath = string.IsNullOrEmpty(options.PublicKeyPath) ? null : options.PublicKeyPath;
        if (options.PrivateKeyPath is { } privateKeyPath)
        {
            return new SshUserKeyFiles(privateKeyPath, publicKeyPath);
        }

        foreach (string candidate in DefaultPrivateKeyPaths())
        {
            if (await ReadTextAsync(candidate, cancellationToken).ConfigureAwait(false) is not null)
            {
                return new SshUserKeyFiles(candidate, publicKeyPath);
            }
        }

        return new SshUserKeyFiles(string.Empty, publicKeyPath);
    }

    /// <summary>
    /// Reads the public key: the <c>--pubkey</c> file when one is given, the private key's
    /// public half otherwise.
    /// </summary>
    /// <param name="files">The located files.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The public key; or none, with libssh2's reason when the <c>--pubkey</c> file did not
    /// open or parse, and without one when the private key could not be read.
    /// </returns>
    internal async ValueTask<SshPublicKeyReading> ReadPublicKeyAsync(SshUserKeyFiles files, CancellationToken cancellationToken)
    {
        if (files.PublicKeyPath is { } publicKeyPath)
        {
            string? text = await ReadTextAsync(publicKeyPath, cancellationToken).ConfigureAwait(false);
            return text is null ? new SshPublicKeyReading(null, SshInfoLines.PublicKeyFileUnopened) : SshPublicKeyFile.Parse(text);
        }

        return new SshPublicKeyReading((await ReadPrivateKeyAsync(files, cancellationToken).ConfigureAwait(false))?.PublicKey, null);
    }

    /// <summary>
    /// Reads and, where it is encrypted, decrypts the private key.
    /// </summary>
    /// <param name="files">The located files.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The key, or <see langword="null" /> when the file cannot be opened or read.</returns>
    internal async ValueTask<SshPrivateKey?> ReadPrivateKeyAsync(SshUserKeyFiles files, CancellationToken cancellationToken)
    {
        string? text = await ReadTextAsync(files.PrivateKeyPath, cancellationToken).ConfigureAwait(false);
        return text is null ? null : SshPrivateKeyReader.Read(text, passphraseEncoding.GetBytes(options.PrivateKeyPassphrase ?? string.Empty));
    }

    /// <summary>
    /// Whether the private key file opens for reading, whatever it holds.
    /// </summary>
    /// <param name="files">The located files.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns><see langword="true" /> when the file opens and reads.</returns>
    internal async ValueTask<bool> PrivateKeyFileOpensAsync(SshUserKeyFiles files, CancellationToken cancellationToken) =>
        await ReadTextAsync(files.PrivateKeyPath, cancellationToken).ConfigureAwait(false) is not null;

    // curl joins HOME and the rest with a forward slash on every platform.
    private IEnumerable<string> DefaultPrivateKeyPaths()
    {
        string? home = readEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
        {
            yield return home + "/.ssh/id_rsa";
            yield return home + "/.ssh/id_dsa";
        }

        yield return "id_rsa";
        yield return "id_dsa";
    }

    private async ValueTask<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        if (path.Length == 0)
        {
            return null;
        }

        FileOpenResult opened = await fileSystem.OpenForReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (opened.Content is not { } content)
        {
            return null;
        }

        await using (content.ConfigureAwait(false))
        {
            using StreamReader reader = new(content, Encoding.Latin1);
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
