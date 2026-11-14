using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;

namespace Curl.Extension.Composition;

/// <summary>
/// Refuses an extension connection hook beside curl's own <c>--unix-socket</c> or <c>--abstract-unix-socket</c>
/// (ADR-1270): each supplies the connection a transfer runs over, so, as with two extension connection options
/// (ADR-1014), only one may be given.
/// </summary>
public static class CurlSocketConnectionConflict
{
    /// <summary>Throws when an option group activated a connection hook and also gave curl's socket option.</summary>
    /// <param name="hooks">The connection hook the group's extension options activated.</param>
    /// <param name="curlSocketOption">The name, without dashes, of the curl socket option the group gave (<c>unix-socket</c> or <c>abstract-unix-socket</c>), or <see langword="null" /> for none.</param>
    /// <exception cref="ExtensionHookConflictException">Both were given; the message names both options.</exception>
    public static void RefuseBesideCurlSocket(ConnectionHooks hooks, string? curlSocketOption)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        if (hooks.Hook is { } hook && curlSocketOption is not null)
        {
            throw new ExtensionHookConflictException(
                $"the options --{hook.Option.Name} and --{curlSocketOption} each supply the connection a transfer runs over; give only one of them");
        }
    }
}
