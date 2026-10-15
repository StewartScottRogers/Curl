namespace Curl.Api.EngineContracts.Share;

/// <summary>
/// <c>CURLSHOPT_LOCKFUNC</c>: takes the lock on one kind of shared data before a transfer uses it.
/// <c>CURLSHOPT_USERDATA</c> is the delegate's closure.
/// </summary>
/// <param name="transfer"><c>handle</c>: the transfer asking, or <see langword="null"/> when the share handle itself asks.</param>
/// <param name="data"><c>data</c>: the kind of data to lock.</param>
/// <param name="access"><c>locktype</c>: the access wanted.</param>
public delegate void ShareLockCallback(IEasyTransfer? transfer, CurlLockData data, CurlLockAccess access);
