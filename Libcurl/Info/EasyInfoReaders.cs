namespace Libcurl.Info;

/// <summary>
/// The readers <see cref="EasyInfoDispatch.Default"/> dispatches through, one row per info.
/// </summary>
/// <remarks>
/// Each info group adds its rows from its own folder (<c>Libcurl/Info/&lt;Group&gt;</c>) by
/// implementing its partial method below in a part of this class there, in this namespace, so group
/// tasks never edit one shared file (ADR-0564). Until a group's part exists its method adds nothing,
/// and its infos answer <see cref="Curl.Api.Codes.CurlCode.NotBuiltIn"/>.
/// </remarks>
internal static partial class EasyInfoReaders
{
    /// <summary>Builds the reader rows every info group has added.</summary>
    /// <returns>The readers, by info.</returns>
    internal static Dictionary<CurlInfo, EasyInfoReader> Create()
    {
        var readers = new Dictionary<CurlInfo, EasyInfoReader>();
        AddStringReaders(readers);
        AddLongReaders(readers);
        AddTimeReaders(readers);
        return readers;
    }

    /// <summary>Adds the string, list and TLS pointer rows (<c>Libcurl/Info/Strings</c>).</summary>
    /// <param name="readers">The rows to add to.</param>
    static partial void AddStringReaders(Dictionary<CurlInfo, EasyInfoReader> readers);

    /// <summary>Adds the long and socket rows (<c>Libcurl/Info/Longs</c>).</summary>
    /// <param name="readers">The rows to add to.</param>
    static partial void AddLongReaders(Dictionary<CurlInfo, EasyInfoReader> readers);

    /// <summary>Adds the time, size and speed rows (<c>Libcurl/Info/Times</c>).</summary>
    /// <param name="readers">The rows to add to.</param>
    static partial void AddTimeReaders(Dictionary<CurlInfo, EasyInfoReader> readers);
}
