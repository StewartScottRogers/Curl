namespace Curl.Cli;

/// <summary>
/// One upload a globbed <c>-T</c> argument expands to: the local file and the URL it is sent to.
/// </summary>
/// <param name="UploadFile">The upload file name, one match of the <c>-T</c> glob.</param>
/// <param name="TransferUrl">
/// The URL <see cref="UploadTransferUrl.TryResolve" /> resolved for <paramref name="UploadFile" />;
/// the empty string when <paramref name="IsUrlWellFormed" /> is <see langword="false" />.
/// </param>
/// <param name="IsUrlWellFormed">
/// <see langword="false" /> when the URL cannot be parsed, which curl reports with
/// <see cref="Curl.Protocol.Abstractions.CurlExitCode.UrlMalformat" /> and
/// <see cref="UploadTransferUrl.MalformedUrlMessage" />.
/// </param>
public sealed record UploadTransferTarget(string UploadFile, string TransferUrl, bool IsUrlWellFormed);
