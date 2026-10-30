using System.Net;

namespace Curl.Extension.OsCredentialStore;

/// <summary>One platform's credential store, read by the name <c>--user-from-keychain</c> gives (ADR-1144).</summary>
internal interface ICredentialStore
{
    /// <summary>Gets the store's name, as an error message names it.</summary>
    string Name { get; }

    /// <summary>Reads the user name and password stored under <paramref name="name" />.</summary>
    /// <param name="name">The credential's name: its target name, service or <c>service</c> attribute.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The credential, or <see langword="null" /> when the store holds none under that name.</returns>
    ValueTask<NetworkCredential?> FindAsync(string name, CancellationToken cancellationToken);
}
