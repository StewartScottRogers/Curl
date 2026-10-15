namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_DEBUGFUNCTION</c>: takes the verbose trace when <c>CURLOPT_VERBOSE</c> is set.
/// <c>CURLOPT_DEBUGDATA</c> is the delegate's closure.
/// </summary>
/// <param name="type">What kind of data <paramref name="data"/> is.</param>
/// <param name="data">The trace bytes, not zero-terminated, valid only during the call.</param>
/// <returns>0; upstream requires it.</returns>
public delegate int DebugCallback(CurlInfoType type, ReadOnlySpan<byte> data);
