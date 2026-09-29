using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Finds the default credential cache as MIT Kerberos does and reads a <c>FILE:</c> cache
/// or a <c>DIR:</c> collection's cache through the injected file reader, and a <c>KCM:</c>
/// cache through the injected KCM connector (ADR-0142, ADR-0158, ADR-0194), and stores a
/// credential in a <c>FILE:</c> or <c>DIR:</c> cache through the injected file writer
/// (ADR-0208) and in a <c>KCM:</c> cache through the KCM connector (BL-891).
/// </summary>
/// <param name="files">Reads the cache file, and a <c>DIR:</c> collection's <c>primary</c> file.</param>
/// <param name="readEnvironmentVariable">Reads an environment variable; <see langword="null" /> when unset.</param>
/// <param name="readUserId">Gives the user's numeric id, the <c>&lt;uid&gt;</c> of <c>/tmp/krb5cc_&lt;uid&gt;</c>.</param>
/// <param name="kcm">Connects to the KCM daemon; <see langword="null" /> when none can be reached, so a <c>KCM:</c> cache fails as <see cref="KerberosFileError.KcmNotRunning" />.</param>
/// <param name="configuration">
/// <c>krb5.conf</c>, for <c>[libdefaults] default_ccache_name</c> and <c>kcm_socket</c>;
/// <see langword="null" /> reads as an empty file.
/// </param>
/// <param name="readUserName">
/// Gives the user's login name for <c>%{username}</c>; <see langword="null" /> uses
/// <see cref="Environment.UserName" />, which is <c>getpwuid(geteuid())</c>'s name off Windows.
/// </param>
/// <param name="fileWriter">
/// Appends a stored credential to a <c>FILE:</c> or <c>DIR:</c> cache file;
/// <see langword="null" /> when caches are only read, so <see cref="Store" /> fails as
/// <see cref="KerberosFileError.NotWritable" /> for such a cache.
/// </param>
public sealed class CredentialCacheStore(
    IKerberosFileReader files,
    Func<string, string?> readEnvironmentVariable,
    Func<uint> readUserId,
    IKerberosKcmConnector? kcm = null,
    KerberosConfiguration? configuration = null,
    Func<string>? readUserName = null,
    IKerberosFileWriter? fileWriter = null)
{
    /// <summary>The environment variable that names the credential cache.</summary>
    public const string CacheNameVariable = "KRB5CCNAME";

    /// <summary>MIT's built-in KCM socket path (<c>DEFAULT_KCM_SOCKET_PATH</c>), shared with Heimdal.</summary>
    public const string DefaultKcmSocketPath = "/var/run/.heim_org.h5l.kcm-socket";

    /// <summary>The type of a name that names a <c>DIR:</c> collection or one cache in it.</summary>
    public const string DirectoryType = "DIR";

    /// <summary>The type of a name that names a cache held by the KCM daemon.</summary>
    public const string KcmType = "KCM";

    private const string PrimaryFileName = "primary";

    private const string DefaultSubsidiaryName = "tkt";

    private const string KcmSocketTurnedOff = "-";

    /// <summary>
    /// Gets the default cache's name: <c>KRB5CCNAME</c> when set and not empty, else
    /// <c>[libdefaults] default_ccache_name</c> with MIT's <c>%{token}</c> parameters
    /// expanded, else MIT's built-in <c>FILE:/tmp/krb5cc_&lt;uid&gt;</c>.
    /// </summary>
    /// <returns>The default cache's name, e.g. <c>FILE:/tmp/krb5cc_1000</c>.</returns>
    /// <exception cref="KerberosFileException">
    /// <c>default_ccache_name</c> holds an unclosed or unknown token
    /// (<see cref="KerberosFileError.PathTokenInvalid" />).
    /// </exception>
    public string DefaultCacheName()
    {
        string? named = readEnvironmentVariable(CacheNameVariable);
        if (!string.IsNullOrEmpty(named))
        {
            return named;
        }

        uint userId = readUserId();
        string? configured = ConfiguredValue("default_ccache_name");
        return configured is null
            ? "FILE:/tmp/krb5cc_" + userId.ToString(CultureInfo.InvariantCulture)
            : new KerberosPathExpansion(userId, readEnvironmentVariable, readUserName ?? (() => Environment.UserName)).Expand(configured);
    }

    /// <summary>Reads the default credential cache.</summary>
    /// <returns>The cache's default principal and credentials.</returns>
    /// <exception cref="KerberosFileException">The cache cannot be read; <see cref="KerberosFileException.Error" /> says why.</exception>
    public CredentialCache ReadDefault() => Read(DefaultCacheName());

    /// <summary>Reads the credential cache <paramref name="cacheName" /> names.</summary>
    /// <param name="cacheName">
    /// A cache name: <c>FILE:/tmp/krb5cc_1000</c> or a bare path; <c>DIR:/dir</c> for the
    /// cache the collection's <c>primary</c> file names (<c>tkt</c> when it has none), or
    /// <c>DIR::/dir/tkt2</c> for one cache in it; <c>KCM:</c> for the KCM daemon's default
    /// cache, or <c>KCM:name</c> for a named one.
    /// </param>
    /// <returns>The cache's default principal and credentials.</returns>
    /// <exception cref="KerberosFileException">
    /// The name's type is not <c>FILE</c>, <c>DIR</c> or <c>KCM</c>
    /// (<see cref="KerberosFileError.UnsupportedType" />), the file does not exist
    /// (<see cref="KerberosFileError.NotFound" />), a <c>DIR:</c> collection's <c>primary</c>
    /// file is malformed (<see cref="KerberosFileError.DirectoryPrimaryMalformed" />), the KCM
    /// daemon cannot be reached or refuses, or the cache is not a readable version 4 cache.
    /// </exception>
    public CredentialCache Read(string cacheName)
    {
        KerberosFileName parsed = KerberosFileName.Parse(cacheName);
        return parsed.Type switch
        {
            DirectoryType => ReadFileCache(ReadExistingFile(DirectoryCachePath(parsed.Residual))),
            KcmType => ReadKcmCache(parsed.Residual),
            _ => ReadFileCache(KerberosFileName.ReadFile(files, cacheName, KerberosFileName.FileType)),
        };
    }

    /// <summary>
    /// Stores <paramref name="credential" /> in the cache <paramref name="cacheName" /> names:
    /// in a <c>FILE:</c> or <c>DIR:</c> cache by appending it to the cache file after the
    /// credentials already there, as MIT's <c>cc_file.c</c> does (ADR-0208), the file never
    /// being created; in a <c>KCM:</c> cache by sending the KCM daemon <c>KCM_OP_STORE</c>, as
    /// MIT's <c>cc_kcm.c</c> does (BL-891).
    /// </summary>
    /// <param name="cacheName">
    /// A <c>FILE:</c> name or bare path, a <c>DIR:</c> name, or a <c>KCM:</c> name, resolved as
    /// <see cref="Read" /> resolves it.
    /// </param>
    /// <param name="credential">The credential, e.g. a service ticket from a TGS exchange; it stays the caller's.</param>
    /// <exception cref="KerberosFileException">
    /// For a <c>FILE:</c> or <c>DIR:</c> cache, no file writer was given
    /// (<see cref="KerberosFileError.NotWritable" />), a <c>DIR:</c> collection's
    /// <c>primary</c> file is malformed (<see cref="KerberosFileError.DirectoryPrimaryMalformed" />),
    /// or the file does not exist (<see cref="KerberosFileError.NotFound" />); for a
    /// <c>KCM:</c> cache, the daemon cannot be reached (<see cref="KerberosFileError.KcmNotRunning" />)
    /// or refuses (<see cref="KerberosFileError.KcmFailed" />); or the name's type is none of
    /// these (<see cref="KerberosFileError.UnsupportedType" />).
    /// </exception>
    public void Store(string cacheName, KerberosCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        KerberosFileName parsed = KerberosFileName.Parse(cacheName);
        if (parsed.Type == KcmType)
        {
            StoreMarshalled(credential, bytes => StoreKcmCredential(parsed.Residual, bytes));
            return;
        }

        IKerberosFileWriter writer = fileWriter ?? throw new KerberosFileException(KerberosFileError.NotWritable);
        string path = CacheFilePath(parsed);
        StoreMarshalled(credential, bytes => AppendToExistingFile(writer, path, bytes));
    }

    private static void StoreMarshalled(KerberosCredential credential, Action<byte[]> store)
    {
        CachedCredential cached = credential.ToCached();
        byte[] bytes = CredentialCacheWriter.WriteCredential(cached);
        cached.SessionKey.Dispose();
        try
        {
            store(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void AppendToExistingFile(IKerberosFileWriter writer, string path, byte[] bytes)
    {
        if (!writer.AppendAllBytes(path, bytes))
        {
            throw new KerberosFileException(KerberosFileError.NotFound);
        }
    }

    private string CacheFilePath(KerberosFileName name) => name.Type switch
    {
        KerberosFileName.FileType => name.Residual,
        DirectoryType => DirectoryCachePath(name.Residual),
        _ => throw new KerberosFileException(KerberosFileError.UnsupportedType),
    };

    private static CredentialCache ReadFileCache(byte[] bytes)
    {
        try
        {
            return CredentialCacheReader.Read(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string JoinPath(string directory, string fileName) =>
        directory.EndsWith('/') ? directory + fileName : directory + "/" + fileName;

    private static string ReadPrimaryLine(byte[] primary)
    {
        int newline = Array.IndexOf(primary, (byte)'\n');
        string line = newline < 0 ? string.Empty : Encoding.UTF8.GetString(primary, 0, newline);
        return line.StartsWith(DefaultSubsidiaryName, StringComparison.Ordinal) && !line.Contains('/', StringComparison.Ordinal)
            ? line
            : throw new KerberosFileException(KerberosFileError.DirectoryPrimaryMalformed);
    }

    private string DirectoryCachePath(string residual)
    {
        if (residual.StartsWith(':'))
        {
            return residual[1..];
        }

        byte[]? primary = files.ReadAllBytes(JoinPath(residual, PrimaryFileName));
        return JoinPath(residual, primary is null ? DefaultSubsidiaryName : ReadPrimaryLine(primary));
    }

    private byte[] ReadExistingFile(string path) =>
        files.ReadAllBytes(path) ?? throw new KerberosFileException(KerberosFileError.NotFound);

    private CredentialCache ReadKcmCache(string residual)
    {
        using Stream connection = ConnectToKcm();
        return KcmCredentialCacheReader.Read(new KerberosKcmClient(connection), residual);
    }

    private void StoreKcmCredential(string residual, byte[] credential)
    {
        using Stream connection = ConnectToKcm();
        KcmCredentialCacheWriter.Store(new KerberosKcmClient(connection), residual, credential);
    }

    private Stream ConnectToKcm()
    {
        string socketPath = ConfiguredValue("kcm_socket") ?? DefaultKcmSocketPath;
        Stream? connection = socketPath == KcmSocketTurnedOff ? null : kcm?.Connect(socketPath);
        return connection ?? throw new KerberosFileException(KerberosFileError.KcmNotRunning);
    }

    private string? ConfiguredValue(string tag) =>
        configuration?.GetValues("libdefaults", tag) is [string value, ..] ? value : null;
}
