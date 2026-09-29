namespace Curl.Http2;

/// <summary>
/// The deprecated stream priority fields of HEADERS and PRIORITY (RFC 9113 sections 5.3.2
/// and 6.3). Read so the frame can be validated, then ignored.
/// </summary>
/// <param name="StreamDependency">The stream this one depends on.</param>
/// <param name="IsExclusive">Whether the dependency is exclusive.</param>
/// <param name="Weight">The weight, 1 to 256; the wire carries it minus one.</param>
public readonly record struct Http2Priority(int StreamDependency, bool IsExclusive, int Weight);
