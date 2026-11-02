namespace Curl.Conformance.Extended.GoogleCloudStorage;

/// <summary>How the reference endpoint's truncated List Objects page names the next page.</summary>
public enum GcsListPagination
{
    /// <summary><c>NextContinuationToken</c>, an opaque token, as Cloud Storage answers <c>list-type=2</c>.</summary>
    NextContinuationToken,

    /// <summary><c>NextMarker</c>, the last name of the page, as a listing without <c>list-type=2</c> (and some emulators) answer.</summary>
    NextMarker,
}
