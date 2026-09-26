using System.Diagnostics.CodeAnalysis;

namespace Curl.Output;

/// <summary>
/// The values a <c>-w</c> / <c>--write-out</c> template reads from one finished transfer:
/// its <c>%{name}</c> variables and its <c>%header{name}</c> response headers.
/// </summary>
/// <remarks>
/// <c>%{stdout}</c> and <c>%{stderr}</c> are not variables of the transfer and are never
/// asked for here; <see cref="WriteOutTemplateRenderer"/> handles them itself.
/// </remarks>
public interface IWriteOutVariableSource
{
    /// <summary>
    /// Gets the text a <c>%{name}</c> variable renders as.
    /// </summary>
    /// <param name="name">The variable name exactly as written between the braces; curl matches it case-sensitively.</param>
    /// <param name="text">The rendered text when the variable is known, which may be empty.</param>
    /// <returns><see langword="true"/> when curl knows the variable; <see langword="false"/> makes the renderer print the unknown-variable warning.</returns>
    bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text);

    /// <summary>
    /// Finds the value of the first response header with the given name, as <c>%header{name}</c> prints it.
    /// </summary>
    /// <param name="name">The header name exactly as written between the braces; header names match case-insensitively.</param>
    /// <returns>The first value, or <see langword="null"/> when the response has no such header.</returns>
    string? FindFirstHeaderValue(string name);
}
