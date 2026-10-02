namespace Curl.Console;

/// <summary>
/// Sets one extended attribute on a local file, which is how <c>--xattr</c> stores a transfer's
/// metadata beside its <c>-o</c> / <c>-O</c> file (BL-651, ADR-0320).
/// </summary>
internal interface IExtendedAttributeWriter
{
    /// <summary>
    /// Sets the attribute <paramref name="name" /> of the file at <paramref name="path" /> to
    /// <paramref name="value" />, replacing any value it had.
    /// </summary>
    /// <param name="path">The operating-system path of an existing file.</param>
    /// <param name="name">The attribute's name, such as <c>user.mime_type</c>.</param>
    /// <param name="value">The attribute's value, stored as its UTF-8 bytes with no terminator.</param>
    /// <param name="errorText">
    /// Empty when the attribute was set; otherwise the operating system's text for the failure,
    /// as C's <c>strerror</c> gives it, such as <c>Operation not supported</c>.
    /// </param>
    /// <returns><see langword="true" /> when the attribute was set.</returns>
    bool TryWrite(string path, string name, string value, out string errorText);
}
