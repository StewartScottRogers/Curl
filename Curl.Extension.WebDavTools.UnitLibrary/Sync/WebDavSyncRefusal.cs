namespace Curl.Extension.WebDavTools.Sync;

/// <summary>A listed href the planner refused to map to a local path.</summary>
/// <param name="Href">The href as listed (percent-decoded).</param>
/// <param name="Kind">Why it was refused.</param>
public sealed record WebDavSyncRefusal(string Href, WebDavSyncRefusalKind Kind);
