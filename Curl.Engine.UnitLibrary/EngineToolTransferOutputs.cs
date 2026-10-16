using Curl.Output;

namespace Curl.Engine;

/// <summary>
/// Where one transfer of the curl tool's runner writes its body and header bytes, which a
/// <see cref="ManagedEasyTransfer" /> given them through <see cref="ManagedEasyTransfer.ToolOutputs" /> hands to
/// <see cref="TransferContextFactory.Create" /> as they are, in place of the write and header callbacks (BL-2396,
/// ADR-0558). The runner builds each stream chain itself - <c>--limit-rate</c>, <c>-N</c>'s flush after every write,
/// <c>--use-ascii</c>'s text mode and the standard-output or output-file stream whose write failures become its
/// exit 23 texts - and the factory keeps wrapping <see cref="Output" /> in the attempt's low-speed watchdog,
/// teeing <c>-i</c> header lines with <see cref="HeaderOutput" /> and passing the header output to
/// <see cref="WatchHeaderOutput" />, which a callback could not do.
/// </summary>
/// <param name="Output">Where the transfer's body goes, and its <c>-i</c> and <c>-I</c> header lines.</param>
/// <param name="HeaderOutput">Where the <c>-D</c> header lines go, or <see langword="null" /> without <c>-D</c>.</param>
/// <param name="WatchHeaderOutput">Wraps the header output for <c>-J</c>'s <c>RemoteHeaderNameStream</c>, or <see langword="null" /> without <c>-J</c>.</param>
/// <param name="BodyHeaderStyles">How <c>--styled-output</c> styles the header lines written to <see cref="Output" />, or <see langword="null" /> to write them as they come.</param>
internal sealed record EngineToolTransferOutputs(
    Stream Output,
    Stream? HeaderOutput = null,
    Func<Stream?, Stream>? WatchHeaderOutput = null,
    StyledHeaderLines? BodyHeaderStyles = null);
