namespace Curl.Api.EngineContracts.Share;

/// <summary>
/// <c>CURLSHOPT_UNLOCKFUNC</c>: releases the lock <see cref="ShareLockCallback"/> took.
/// <c>CURLSHOPT_USERDATA</c> is the delegate's closure.
/// </summary>
/// <param name="transfer"><c>handle</c>: the transfer releasing, or <see langword="null"/> when the share handle itself releases.</param>
/// <param name="data"><c>data</c>: the kind of data to unlock.</param>
public delegate void ShareUnlockCallback(IEasyTransfer? transfer, CurlLockData data);
