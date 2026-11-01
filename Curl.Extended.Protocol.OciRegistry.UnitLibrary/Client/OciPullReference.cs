namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>
/// What an <c>oci://</c> URL's path asks to pull, or with <c>-T</c> to push (ADR-1093, ADR-1097): <c>/name[:tag]</c> a manifest, the tag <c>latest</c> when
/// the path names none, or <c>/name@digest</c> a blob, checked against the OCI Distribution Specification 1.1's
/// grammar for names, tags and digests ("Pulling manifests", "Pulling blobs").
/// </summary>
/// <param name="Repository">The repository name, such as <c>library/alpine</c>.</param>
/// <param name="Reference">The tag of a manifest, or the digest of a blob.</param>
/// <param name="IsBlob">Whether the URL names a blob by its digest rather than a manifest by its tag.</param>
internal sealed record OciPullReference(string Repository, string Reference, bool IsBlob)
{
    /// <summary>The tag pulled when the path names none, as <c>docker pull</c> does.</summary>
    internal const string DefaultTag = "latest";

    /// <summary>Gets the registry API path: <c>/v2/&lt;name&gt;/manifests/&lt;tag&gt;</c> or <c>/v2/&lt;name&gt;/blobs/&lt;digest&gt;</c>.</summary>
    public string ApiPath => $"/v2/{Repository}/{(IsBlob ? "blobs" : "manifests")}/{Reference}";

    /// <summary>Parses an <c>oci://</c> URL's path.</summary>
    /// <param name="absolutePath">The path, starting with <c>/</c>.</param>
    /// <returns>What it asks to pull, or <see langword="null"/> when its name, tag or digest breaks the grammar.</returns>
    public static OciPullReference? Parse(string absolutePath)
    {
        var text = absolutePath.TrimStart('/');
        var at = text.IndexOf('@', StringComparison.Ordinal);
        return at >= 0 ? ParseDigested(text[..at], text[(at + 1)..]) : ParseTagged(text);
    }

    private static OciPullReference? ParseDigested(string repository, string digest) =>
        IsName(repository) && IsDigest(digest) ? new(repository, digest, IsBlob: true) : null;

    // The tag follows the last colon after the last slash; a colon before it belongs to no tag and fails the name grammar.
    private static OciPullReference? ParseTagged(string text)
    {
        var colon = text.LastIndexOf(':');
        var (repository, tag) = colon > text.LastIndexOf('/') ? (text[..colon], text[(colon + 1)..]) : (text, DefaultTag);
        return IsName(repository) && IsTag(tag) ? new(repository, tag, IsBlob: false) : null;
    }

    private const string LowerAlphanumerics = "abcdefghijklmnopqrstuvwxyz0123456789";

    // name: path components joined by "/", each lowercase alphanumeric runs joined by ".", "_", "__" or a run of "-".
    private static bool IsName(string name) => name.Split('/').All(IsPathComponent);

    private static bool IsPathComponent(string component) =>
        component.Length > 0
        && IsLowerAlphanumeric(component[0])
        && IsLowerAlphanumeric(component[^1])
        && component.Split(LowerAlphanumerics.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).All(IsSeparator);

    private static bool IsSeparator(string separator) => separator is "." or "_" or "__" || separator.All(character => character == '-');

    // tag: 1 to 128 of [a-zA-Z0-9_.-], not starting with "." or "-".
    private static bool IsTag(string tag) =>
        tag.Length <= 128 && IsMadeOf(tag, character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-') && tag[0] is not ('.' or '-');

    // digest: algorithm ":" encoded; algorithm is lowercase alphanumeric parts joined by "+", ".", "_" or "-".
    private static bool IsDigest(string digest)
    {
        var colon = digest.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0
            && digest[..colon].Split('+', '.', '_', '-').All(part => IsMadeOf(part, IsLowerAlphanumeric))
            && IsMadeOf(digest[(colon + 1)..], character => char.IsAsciiLetterOrDigit(character) || character is '=' or '_' or '-');
    }

    private static bool IsMadeOf(string text, Func<char, bool> isAllowed) => text.Length > 0 && text.All(isAllowed);

    private static bool IsLowerAlphanumeric(char character) => char.IsAsciiDigit(character) || char.IsAsciiLetterLower(character);
}
