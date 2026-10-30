namespace Curl.Extension.WebDavTools.Sync;

/// <summary>One listed resource and the local path it maps to.</summary>
/// <param name="Href">The resource's href as listed (percent-decoded).</param>
/// <param name="LocalPath">The local file or folder path it maps to.</param>
/// <param name="LastModified">
/// Its <c>getlastmodified</c> (RFC 4918 section 15.7): the last-write time a download sets, or <see langword="null"/>
/// when not reported.
/// </param>
public sealed record WebDavSyncItem(string Href, string LocalPath, DateTimeOffset? LastModified);
