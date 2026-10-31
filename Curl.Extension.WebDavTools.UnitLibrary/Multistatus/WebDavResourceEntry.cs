namespace Curl.Extension.WebDavTools.Multistatus;

/// <summary>
/// One <c>response</c> of a <c>207 Multistatus</c> body (RFC 4918 section 14.24): a resource and the
/// properties its successful (2xx) <c>propstat</c> elements report.
/// </summary>
/// <param name="Href">The resource's <c>href</c>, percent-decoded.</param>
/// <param name="IsCollection">Whether its <c>resourcetype</c> holds <c>collection</c>.</param>
/// <param name="ContentLength">Its <c>getcontentlength</c> in bytes, or <see langword="null"/> when not reported or not a whole number.</param>
/// <param name="LastModified">Its <c>getlastmodified</c>, or <see langword="null"/> when not reported or not an RFC 1123 date.</param>
/// <param name="ETag">Its <c>getetag</c>, quotes included as the server sends it, or <see langword="null"/> when not reported.</param>
/// <param name="Status">
/// The response's own <c>status</c> code when it has one; otherwise the code of its first 2xx <c>propstat</c>, or of
/// its first <c>propstat</c> when none succeeded.
/// </param>
public sealed record WebDavResourceEntry(
    string Href,
    bool IsCollection,
    long? ContentLength,
    DateTimeOffset? LastModified,
    string? ETag,
    int Status);
