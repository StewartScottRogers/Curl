namespace Curl.Extension.Abstractions.Resolution;

/// <summary>
/// What an activated extension supplies in place of curl's resolver (ADR-1012): the <see cref="IDnsResolver" />
/// every transfer of its option group looks host names up with - DNS-over-TLS, DNS-over-QUIC or Oblivious
/// DoH - as <c>--doh-url</c> replaces curl's.
/// </summary>
public interface IResolverHook
{
    /// <summary>Gets the option that activated the hook, named when another resolver claims the same transfer.</summary>
    ExtensionOption Option { get; }

    /// <summary>Gives the resolver a transfer's host names are looked up with.</summary>
    /// <param name="curlResolver">The resolver curl would use, for a hook that resolves its own server's name through it.</param>
    /// <returns>The extension's resolver.</returns>
    IDnsResolver ResolverOver(IDnsResolver curlResolver);
}
