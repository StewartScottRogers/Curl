namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// <c>CURLMOPT_NOTIFYFUNCTION</c>: receives the notifications
/// <see cref="IMultiTransfer.EnableNotification"/> switched on. <c>CURLMOPT_NOTIFYDATA</c> is the
/// delegate's closure.
/// </summary>
/// <param name="notification"><c>notification</c>: what happened.</param>
/// <param name="transfer"><c>easy</c>: the transfer it happened to.</param>
public delegate void MultiNotifyCallback(CurlMultiNotification notification, IEasyTransfer transfer);
