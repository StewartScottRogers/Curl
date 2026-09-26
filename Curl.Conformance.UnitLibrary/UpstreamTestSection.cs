namespace Curl.Conformance;

/// <summary>
/// One part of an upstream curl test file, such as <c>&lt;reply&gt;&lt;data&gt;</c> or
/// <c>&lt;verify&gt;&lt;stdout&gt;</c>: the section it sits in, its own name, its attributes as
/// written, and its body as written.
/// </summary>
/// <remarks>
/// The body is kept as written because upstream's <c>runtests.pl</c> applies <c>nonewline</c>,
/// <c>crlf</c> and <c>mode="text"</c> only where it uses a part, after variable substitution,
/// and in a different order per part (<c>&lt;verify&gt;&lt;stdout&gt;</c> cuts the final newline
/// before forcing CRLF, <c>&lt;verify&gt;&lt;file&gt;</c> forces CRLF first). The transforms
/// themselves are in <see cref="UpstreamTestSectionLineEndings"/>.
/// </remarks>
public sealed class UpstreamTestSection
{
    /// <summary>Creates a part from its parsed pieces.</summary>
    /// <param name="section">The enclosing section's name, such as <c>reply</c>.</param>
    /// <param name="name">The part's own name, such as <c>data</c>.</param>
    /// <param name="attributes">The attributes on the part's opening tag, as written.</param>
    /// <param name="content">The body as written.</param>
    /// <param name="lineNumber">The 1-based line of the part's opening tag.</param>
    public UpstreamTestSection(string section, string name, IReadOnlyDictionary<string, string> attributes, ReadOnlyMemory<byte> content, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(attributes);
        Section = section;
        Name = name;
        Attributes = attributes;
        Content = content;
        LineNumber = lineNumber;
    }

    /// <summary>The enclosing section's name: <c>info</c>, <c>reply</c>, <c>client</c> or <c>verify</c>.</summary>
    public string Section { get; }

    /// <summary>The part's own name, such as <c>data</c>, <c>data1</c>, <c>command</c> or <c>file</c>.</summary>
    public string Name { get; }

    /// <summary>
    /// Every attribute on the part's opening tag, value as written (variables such as
    /// <c>%LOGDIR</c> are not substituted here).
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; }

    /// <summary>
    /// The body as written: every byte after the opening tag's line and before the closing
    /// tag's line, including the final line feed. Variables and <c>%if</c> blocks are not
    /// processed and no attribute is applied.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>The 1-based line of the part's opening tag in the test file.</summary>
    public int LineNumber { get; }

    /// <summary>The value of the named attribute, or <see langword="null"/> when the tag does not carry it.</summary>
    /// <param name="attributeName">The attribute name, such as <c>name</c> or <c>mode</c>.</param>
    /// <returns>The value as written, or <see langword="null"/>.</returns>
    public string? GetAttribute(string attributeName) =>
        Attributes.TryGetValue(attributeName, out string? value) ? value : null;

    /// <summary>
    /// Whether the named attribute is switched on, the way <c>runtests.pl</c> tests it
    /// (<c>if($hash{'nonewline'})</c>): present with a value other than empty and <c>0</c>.
    /// So <c>nonewline="yes"</c> and even <c>nonewline="no"</c> are on.
    /// </summary>
    /// <param name="attributeName">The attribute name, such as <c>nonewline</c> or <c>crlf</c>.</param>
    /// <returns><see langword="true"/> when upstream would treat the attribute as set.</returns>
    public bool IsAttributeSet(string attributeName) =>
        GetAttribute(attributeName) is { Length: > 0 } value && value != "0";
}
