using System.Security.Cryptography;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Reads a <c>KCM:</c> credential cache through a <see cref="KerberosKcmClient" /> as MIT's
/// <c>cc_kcm.c</c> does: an empty residual asks the daemon for its default cache
/// (<c>GET_DEFAULT_CACHE</c>), then the cache's principal (<c>GET_PRINCIPAL</c>), its KDC
/// time offset (<c>GET_KDC_OFFSET</c>), its credentials' UUIDs (<c>GET_CRED_UUID_LIST</c>) and
/// each credential (<c>GET_CRED_BY_UUID</c>). Principals and credentials are marshalled as in
/// a version 4 <c>FILE:</c> cache.
/// </summary>
internal static class KcmCredentialCacheReader
{
    private const int UuidLength = 16;

    /// <summary>Reads the cache <paramref name="residual" /> names, or the daemon's default cache when it is empty.</summary>
    /// <exception cref="KerberosFileException">
    /// The daemon refuses a request (<see cref="KerberosFileError.KcmFailed" />), the cache has
    /// no principal (<see cref="KerberosFileError.NotFound" />), or a reply is malformed.
    /// </exception>
    public static CredentialCache Read(KerberosKcmClient client, string residual)
    {
        string cacheName = residual.Length == 0 ? ReadDefaultCacheName(client) : residual;
        byte[] name = [.. Encoding.UTF8.GetBytes(cacheName), 0];
        KerberosPrincipal principal = ReadPrincipal(client, name);
        TimeSpan? kdcTimeOffset = ReadKdcTimeOffset(client, name);
        return new CredentialCache(kdcTimeOffset, principal, ReadCredentials(client, name));
    }

    internal static string ReadDefaultCacheName(KerberosKcmClient client)
    {
        byte[] payload = RequireSuccess(client.Call(KerberosKcmOperation.GetDefaultCache));
        int end = Array.IndexOf(payload, (byte)0);
        return end < 0
            ? throw new KerberosFileException(KerberosFileError.KcmReplyMalformed)
            : Encoding.UTF8.GetString(payload, 0, end);
    }

    private static KerberosPrincipal ReadPrincipal(KerberosKcmClient client, byte[] name)
    {
        byte[] payload = RequireSuccess(client.Call(KerberosKcmOperation.GetPrincipal, name));
        return payload.Length == 0
            ? throw new KerberosFileException(KerberosFileError.NotFound)
            : CredentialCacheReader.ReadPrincipal(payload);
    }

    private static TimeSpan? ReadKdcTimeOffset(KerberosKcmClient client, byte[] name)
    {
        KerberosKcmReply reply = client.Call(KerberosKcmOperation.GetKdcOffset, name);
        return reply.IsSuccess ? TimeSpan.FromSeconds(new BigEndianFileCursor(reply.Payload).ReadInt32()) : null;
    }

    private static List<CachedCredential> ReadCredentials(KerberosKcmClient client, byte[] name)
    {
        byte[] uuids = RequireSuccess(client.Call(KerberosKcmOperation.GetCredentialUuidList, name));
        if (uuids.Length % UuidLength != 0)
        {
            throw new KerberosFileException(KerberosFileError.KcmReplyMalformed);
        }

        List<CachedCredential> credentials = [];
        try
        {
            foreach (byte[] uuid in uuids.Chunk(UuidLength))
            {
                credentials.Add(ReadCredential(client, name, uuid));
            }
        }
        catch (KerberosFileException)
        {
            credentials.ForEach(credential => credential.SessionKey.Dispose());
            throw;
        }

        return credentials;
    }

    private static CachedCredential ReadCredential(KerberosKcmClient client, byte[] name, byte[] uuid)
    {
        byte[] payload = RequireSuccess(client.Call(KerberosKcmOperation.GetCredentialByUuid, name, uuid));
        try
        {
            return CredentialCacheReader.ReadCredential(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    internal static byte[] RequireSuccess(KerberosKcmReply reply) =>
        reply.IsSuccess ? reply.Payload : throw new KerberosFileException(KerberosFileError.KcmFailed, reply.Status);
}
