namespace Curl.Extension.Protocol.Grpc.Wire;

/// <summary>
/// The status codes of gRPC's statuscodes.md, carried in <c>grpc-status</c>.
/// </summary>
internal enum GrpcStatusCode
{
    /// <summary>OK: the call succeeded.</summary>
    Ok = 0,

    /// <summary>CANCELLED: the call was cancelled, typically by the caller.</summary>
    Cancelled = 1,

    /// <summary>UNKNOWN: an error with no better code.</summary>
    Unknown = 2,

    /// <summary>INVALID_ARGUMENT: the client sent an invalid argument.</summary>
    InvalidArgument = 3,

    /// <summary>DEADLINE_EXCEEDED: the deadline passed before the call completed.</summary>
    DeadlineExceeded = 4,

    /// <summary>NOT_FOUND: something the call asked for was not found.</summary>
    NotFound = 5,

    /// <summary>ALREADY_EXISTS: something the call tried to create already exists.</summary>
    AlreadyExists = 6,

    /// <summary>PERMISSION_DENIED: the caller may not do this.</summary>
    PermissionDenied = 7,

    /// <summary>RESOURCE_EXHAUSTED: a resource or quota ran out.</summary>
    ResourceExhausted = 8,

    /// <summary>FAILED_PRECONDITION: the system is not in a state the call needs.</summary>
    FailedPrecondition = 9,

    /// <summary>ABORTED: the call was aborted, typically by a concurrency conflict.</summary>
    Aborted = 10,

    /// <summary>OUT_OF_RANGE: the call went past a valid range.</summary>
    OutOfRange = 11,

    /// <summary>UNIMPLEMENTED: the server does not implement the method.</summary>
    Unimplemented = 12,

    /// <summary>INTERNAL: an invariant the system relies on broke.</summary>
    Internal = 13,

    /// <summary>UNAVAILABLE: the service is unavailable for now.</summary>
    Unavailable = 14,

    /// <summary>DATA_LOSS: data was lost or corrupted beyond recovery.</summary>
    DataLoss = 15,

    /// <summary>UNAUTHENTICATED: the call has no valid credentials.</summary>
    Unauthenticated = 16,
}
