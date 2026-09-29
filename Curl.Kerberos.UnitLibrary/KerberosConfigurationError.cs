namespace Curl.Kerberos;

/// <summary>
/// Why a <c>krb5.conf</c> file could not be read, one value for each error MIT's profile
/// library (<c>prof_parse.c</c>) returns for it.
/// </summary>
public enum KerberosConfigurationError
{
    /// <summary>A <c>[</c> section header has no closing <c>]</c> (MIT's <c>PROF_SECTION_SYNTAX</c>).</summary>
    SectionSyntax,

    /// <summary>A <c>[</c> section header appears inside a <c>{</c> group (MIT's <c>PROF_SECTION_NOTOP</c>).</summary>
    SectionNotTop,

    /// <summary>A <c>}</c> closes no open group (MIT's <c>PROF_EXTRA_CBRACE</c>).</summary>
    ExtraClosingBrace,

    /// <summary>
    /// A line in a section has no <c>=</c>, an empty or space-split tag, or text after a
    /// <c>{</c> (MIT's <c>PROF_RELATION_SYNTAX</c>).
    /// </summary>
    RelationSyntax,

    /// <summary>
    /// A relation with an empty value is not followed by a line that opens its group with
    /// <c>{</c> (MIT's <c>PROF_MISSING_OBRACE</c>).
    /// </summary>
    MissingOpeningBrace,

    /// <summary>The file an <c>include</c> directive names does not exist (MIT's <c>PROF_FAIL_INCLUDE_FILE</c>).</summary>
    IncludeFileNotFound,

    /// <summary>The directory an <c>includedir</c> directive names does not exist (MIT's <c>PROF_FAIL_INCLUDE_DIR</c>).</summary>
    IncludeDirectoryNotFound,

    /// <summary>
    /// Includes nest more than <see cref="KerberosConfigurationReader.MaximumIncludeDepth" />
    /// deep, as an include loop does.
    /// </summary>
    TooManyIncludes,

    /// <summary>
    /// A <c>kdc</c> entry is not <c>host</c>, <c>host:port</c> or <c>[address]:port</c> with a
    /// port from 1 to 65535, which fails MIT's whole KDC lookup with <c>EINVAL</c>.
    /// </summary>
    InvalidKdcAddress,
}
