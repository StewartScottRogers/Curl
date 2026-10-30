namespace Curl.Extension.WebDavTools.Sync;

/// <summary>Why the sync planner refused a listed href instead of mapping it to a local path.</summary>
public enum WebDavSyncRefusalKind
{
    /// <summary>The href's path does not start with the listed collection's path.</summary>
    OutsideCollection,

    /// <summary>The name under the collection is <c>.</c> or <c>..</c>, or holds a <c>/</c> or <c>\</c>.</summary>
    TraversalSegment,

    /// <summary>The name under the collection is empty or holds a character no file name may hold on any platform.</summary>
    InvalidFileName,
}
