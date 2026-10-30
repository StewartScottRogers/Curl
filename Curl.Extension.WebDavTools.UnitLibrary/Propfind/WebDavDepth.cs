namespace Curl.Extension.WebDavTools.Propfind;

/// <summary>How far below the request URL a <c>PROPFIND</c> reaches (RFC 4918 section 10.2).</summary>
public enum WebDavDepth
{
    /// <summary>The resource itself only (<c>Depth: 0</c>).</summary>
    Zero,

    /// <summary>The resource and its internal members (<c>Depth: 1</c>).</summary>
    One,

    /// <summary>The resource and every member below it (<c>Depth: infinity</c>).</summary>
    Infinity,
}
