namespace Curl.Extension.Composition;

/// <summary>
/// The switch that turns every extension off, option extensions and extended protocols alike, leaving
/// exactly upstream curl's options and protocol handlers (ADR-0999).
/// </summary>
public static class ExtensionSwitch
{
    /// <summary>The environment variable that turns every extension off when set to <c>0</c>.</summary>
    public const string Variable = "CURL_EXTENSIONS";
}
