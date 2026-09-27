namespace Curl.Console;

/// <summary>
/// The file a transfer saves its body to, as the runner resolved it before the transfer.
/// </summary>
/// <param name="Path">
/// The file: the <c>-o</c> name or the remote name, rewritten on Windows and put under
/// <c>--output-dir</c>.
/// </param>
/// <param name="TakesContentDispositionName">
/// Whether a <c>Content-Disposition</c> header may rename it, under <c>-J</c> for a remote name.
/// </param>
internal sealed record OutputFileTarget(string Path, bool TakesContentDispositionName);
