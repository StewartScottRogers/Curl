namespace Curl.Cli;

/// <summary>
/// The HTTP request method an option has selected so far, which curl 8.21.0 lets be selected only
/// once per command line: selecting a different one is refused.
/// </summary>
internal enum SelectedHttpMethod
{
    /// <summary>No option has selected a method yet.</summary>
    None = 0,

    /// <summary><c>--no-head</c> selected <c>GET</c>.</summary>
    Get,

    /// <summary><c>-I</c> / <c>--head</c> selected <c>HEAD</c>.</summary>
    Head,
}
