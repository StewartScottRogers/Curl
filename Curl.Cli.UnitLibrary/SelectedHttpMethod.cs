namespace Curl.Cli;

/// <summary>
/// The HTTP request method an option has selected so far, which curl 8.21.0 lets be selected only
/// once per command line: selecting a different one is refused. The values are in the order of
/// curl's own <c>reqname</c> table, which <see cref="CommandLineWarning.OnlyOneRequestMethod"/> indexes.
/// </summary>
public enum SelectedHttpMethod
{
    /// <summary>No option has selected a method yet.</summary>
    None = 0,

    /// <summary><c>--no-head</c> selected <c>GET</c>, or <c>-G</c> / <c>--get</c> turned the body into a query.</summary>
    Get,

    /// <summary><c>-I</c> / <c>--head</c> selected <c>HEAD</c>.</summary>
    Head,

    /// <summary><c>-F</c> / <c>--form</c> or <c>--form-string</c> selected a multipart form post.</summary>
    MultipartFormPost,

    /// <summary>
    /// A <c>-d</c> / <c>--data</c> body: never stored, only named when the command line, once read,
    /// asks for it and a multipart form post both.
    /// </summary>
    Post,

    /// <summary>
    /// A <c>-T</c> / <c>--upload-file</c> upload: never stored, only named when a transfer that uploads a
    /// file finds another method already selected.
    /// </summary>
    Put,
}
