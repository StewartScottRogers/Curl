# Curl.Console

Phase 1. The entry point, published native ahead-of-time as a single file so it
drops onto PATH as `curl.exe`.

This is the only project without a `.UnitLibrary` suffix, because it is an
executable rather than a library.

It is also the only project that references every library: composing the services
is its job. `CurlComposition` is the composition root, written as plain constructor
calls - no container, no reflection, no assembly scanning, so native AOT sees every
type. `Curl.Core.UnitLibrary` dispatches through the `IProtocolHandler` instances it is
given and must never reference a protocol library directly.

`Program.Main` only opens the standard streams, builds the composition and hands the
arguments to `CurlCommandRunner`, which parses them, runs each URL and prints curl's
`curl: (N) <message>` lines. `-o` files open on the first write through
`DeferredOutputFileStream`, which is how curl's exit 23 message comes out right. The
parser's warning lines are written to standard error before anything else.
Every `Warning: ` line is wrapped by `WarningLineWrapper` as curl's `warnf` wraps it, at the
width `TerminalColumns` resolves (`COLUMNS` from 21 to 9999, else the standard-error
console, else 79).
On Windows each `-o` name is first rewritten by `WindowsOutputFileNameSanitizer`
(`"*<>?|` and control characters become `_`, as curl 8.21.0 does), and that name is the
one opened, sized for `-C -` and named in every message.

`TransferContextFactory` builds each transfer's context from the parsed options; the
context carries the parsed `-r` range (`ByteRangeParser`; text that names
no range ends the transfer with exit 33 before it is dispatched), the `-C` offset and the
`--max-filesize` limit. `-C -` resumes from the size of the URL's `-o` file, and a transfer
that resumes past byte zero opens that file for appending before it starts, as curl does.
`-D -` sends the handler's header lines to standard output; any other `-D` name is opened
(unsanitized, truncated for the first URL and appended for the rest) before the transfer,
and one that cannot be opened prints `curl: Failed to open <file>` and stops the run with
exit 23.

Under `-R`/`--remote-time` a successful transfer to an `-o` file whose result carries
`SourceLastWriteTimeUtc` stamps the closed file with it through `IFileTimeSetter`
(`PhysicalFileSystem` in production), even when no body was written, as curl does. A
failed stamp is ignored for now; curl's warning lines for it are BL-139.

After each successful transfer, standard error gets the opening of curl's progress meter
(`ProgressMeterLines`): `** Resuming transfer from byte position N` when it resumed past
byte zero, the two header lines, and the all-zero status line - every byte curl 8.21.0
writes for a `file://` transfer. It is not written under `-s`, `--no-progress-meter` or
`-#`, nor for a body on standard output when that is a terminal. Live counters, the bar
form and the meter after a failed transfer are not modelled yet (BL-130 to BL-132).

A URL with no `-o` writes through `StandardOutputFailureDeferringStream`, which
models curl's 4096-byte stdio buffer: a failed standard output is reported as
`curl: Failed writing body` (exit 23) while the body fits the buffer, and as the
handler's own `(23)` write failure once it would not.

`Program.Main` opens standard output with `StandardOutputOpener`, not
`System.Console.OpenStandardOutput()`, because the latter hides both failures that
stream models: it returns `Stream.Null` for a closed standard output and reports a write
to a pipe whose reader has gone as a success. A console is still opened the .NET way; a
redirected standard output becomes an unbuffered `FileStream` over the process's own
handle (`GetStdHandle` on Windows, descriptor 1 elsewhere), and a closed one a
`ClosedStandardOutputStream` whose writes throw.
