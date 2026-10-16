namespace Libcurl.Options;

/// <summary>
/// The <c>EasyCallbackOptions</c> part of <see cref="EasyOptionSetters"/> (ADR-0560): each callback area adds its
/// rows from its own folder (<c>Libcurl/Callbacks/&lt;Area&gt;</c>) by implementing its partial method below in a
/// part of this class there, in this namespace, so the callback tasks never edit one shared file (ADR-0569).
/// Until an area's part exists its method adds nothing, and its options answer
/// <see cref="Curl.Api.Codes.CurlCode.NotBuiltIn"/>.
/// </summary>
internal static partial class EasyOptionSetters
{
    static partial void AddCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        AddReadWriteCallbackSetters(setters);
        AddProgressCallbackSetters(setters);
        AddSeekCallbackSetters(setters);
        AddSocketsCallbackSetters(setters);
        AddPhasesCallbackSetters(setters);
        AddWildcardCallbackSetters(setters);
        AddHstsCallbackSetters(setters);
        AddKeysCallbackSetters(setters);
    }

    /// <summary>Adds the write, read and header rows (<c>Libcurl/Callbacks/ReadWrite</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddReadWriteCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the progress, xferinfo and debug rows (<c>Libcurl/Callbacks/Progress</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddProgressCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the seek rows (<c>Libcurl/Callbacks/Seek</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddSeekCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the sockopt, opensocket and closesocket rows (<c>Libcurl/Callbacks/Sockets</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddSocketsCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the prerequest, resolver-start and trailer rows (<c>Libcurl/Callbacks/Phases</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddPhasesCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the chunk, fnmatch and interleave rows (<c>Libcurl/Callbacks/Wildcard</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddWildcardCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the HSTS read and write rows (<c>Libcurl/Callbacks/Hsts</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddHstsCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the SSH key and SSL context rows (<c>Libcurl/Callbacks/Keys</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddKeysCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters);
}
