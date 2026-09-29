using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Stores a credential in a <c>KCM:</c> credential cache through a
/// <see cref="KerberosKcmClient" /> as MIT's <c>cc_kcm.c</c> does: an empty residual asks the
/// daemon for its default cache (<c>GET_DEFAULT_CACHE</c>), then <c>STORE</c> sends the cache's
/// name, null-terminated, followed by the credential marshalled as in a version 4
/// <c>FILE:</c> cache.
/// </summary>
internal static class KcmCredentialCacheWriter
{
    /// <summary>Stores <paramref name="credential" /> in the cache <paramref name="residual" /> names, or the daemon's default cache when it is empty.</summary>
    /// <param name="client">The KCM client.</param>
    /// <param name="residual">The cache name after <c>KCM:</c>.</param>
    /// <param name="credential">The credential as <see cref="CredentialCacheWriter.WriteCredential(CachedCredential)" /> marshals it; the caller zeroes it.</param>
    /// <exception cref="KerberosFileException">
    /// The daemon refuses a request (<see cref="KerberosFileError.KcmFailed" />) or its default
    /// cache name is malformed (<see cref="KerberosFileError.KcmReplyMalformed" />).
    /// </exception>
    public static void Store(KerberosKcmClient client, string residual, byte[] credential)
    {
        string cacheName = residual.Length == 0 ? KcmCredentialCacheReader.ReadDefaultCacheName(client) : residual;
        byte[] name = [.. Encoding.UTF8.GetBytes(cacheName), 0];
        KcmCredentialCacheReader.RequireSuccess(client.Call(KerberosKcmOperation.Store, name, credential));
    }
}
