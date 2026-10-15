namespace Curl.Api.Codes;

/// <summary>
/// The result of a libcurl multi function: libcurl's <c>CURLMcode</c> (<c>CURLM_*</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/multi.h</c>.
/// </summary>
public enum CurlMultiCode
{
    /// <summary>
    /// <c>CURLM_CALL_MULTI_PERFORM</c> (-1), also <c>CURLM_CALL_MULTI_SOCKET</c>: call the perform
    /// function again soon.
    /// </summary>
    CallMultiPerform = -1,

    /// <summary><c>CURLM_OK</c> (0): the function succeeded.</summary>
    Ok = 0,

    /// <summary><c>CURLM_BAD_HANDLE</c> (1): the handle passed is not a valid multi handle.</summary>
    BadHandle = 1,

    /// <summary><c>CURLM_BAD_EASY_HANDLE</c> (2): an easy handle was not valid.</summary>
    BadEasyHandle = 2,

    /// <summary><c>CURLM_OUT_OF_MEMORY</c> (3): a memory allocation failed.</summary>
    OutOfMemory = 3,

    /// <summary><c>CURLM_INTERNAL_ERROR</c> (4): an internal libcurl error occurred.</summary>
    InternalError = 4,

    /// <summary><c>CURLM_BAD_SOCKET</c> (5): the socket passed did not match a known one.</summary>
    BadSocket = 5,

    /// <summary><c>CURLM_UNKNOWN_OPTION</c> (6): a multi option was not recognised.</summary>
    UnknownOption = 6,

    /// <summary><c>CURLM_ADDED_ALREADY</c> (7): the easy handle is already added to a multi handle.</summary>
    AddedAlready = 7,

    /// <summary><c>CURLM_RECURSIVE_API_CALL</c> (8): an API function was called from inside a callback.</summary>
    RecursiveApiCall = 8,

    /// <summary><c>CURLM_WAKEUP_FAILURE</c> (9): waking the multi handle is unavailable or failed.</summary>
    WakeupFailure = 9,

    /// <summary><c>CURLM_BAD_FUNCTION_ARGUMENT</c> (10): a function was called with a bad argument.</summary>
    BadFunctionArgument = 10,

    /// <summary><c>CURLM_ABORTED_BY_CALLBACK</c> (11): a callback aborted the operation.</summary>
    AbortedByCallback = 11,

    /// <summary><c>CURLM_UNRECOVERABLE_POLL</c> (12): polling the sockets failed unrecoverably.</summary>
    UnrecoverablePoll = 12,
}
