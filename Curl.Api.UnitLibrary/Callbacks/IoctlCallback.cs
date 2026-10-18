namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_IOCTLFUNCTION</c>: carries out a command on the upload source; libcurl only ever passes
/// <see cref="IoctlCommand.RestartRead"/>, to rewind a read-callback upload it must send again when no
/// <c>CURLOPT_SEEKFUNCTION</c> is set. <c>CURLOPT_IOCTLDATA</c> is the delegate's closure.
/// </summary>
/// <param name="command">The command.</param>
/// <returns>Whether the command was done.</returns>
public delegate IoctlResult IoctlCallback(IoctlCommand command);
