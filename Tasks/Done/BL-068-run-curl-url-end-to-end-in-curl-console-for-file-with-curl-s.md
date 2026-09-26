---
id: BL-068
title: Run curl <url> end to end in Curl.Console for file:// with curl's error lines and exit codes
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-074]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-068 — Run curl <url> end to end in Curl.Console for file:// with curl's error lines and exit codes

## Goal

`curl file:///C:/Windows/win.ini` run from the built `Curl.Console` writes the file's
bytes to standard output and exits 0, and every failure path prints curl 8.21.0's
`curl: (N) <message>` line and returns its exit code: the command line is parsed by
`CommandLineParser`, each URL becomes a `TransferContext`, and `ProtocolDispatcher` runs
it through handlers registered by an explicit, hand-written composition root.

## Context

- Today `Curl.Console/Program.cs` prints `curl: not implemented yet` and returns 2.
  `Curl.Console/CLAUDE.md`: composing the container is this project's job, and
  `Curl.Core.UnitLibrary` never references a protocol library.
- Parts that exist: `Curl.Cli.UnitLibrary/CommandLineParser.cs` (BL-037, BL-038) returns
  `CommandLineParseResult` with `Options` or a `Refusal` whose `StandardErrorLines` carry
  no terminator and whose `ExitCode` is 2; `Curl.Core.UnitLibrary/ProtocolDispatcher.cs`
  (BL-036) takes `IEnumerable<IProtocolHandler>` and returns exit 1
  `Protocol "<scheme>" not supported` for an unregistered scheme;
  `TransferContext` (ADR-0006) in `Curl.Protocol.Abstractions.UnitLibrary`;
  `FileProtocolHandler(IFileSystem)` (BL-008) and `PhysicalFileSystem` (BL-009, in
  `Curl.Core.UnitLibrary/FileSystem`).
- Composition is plain constructor calls in one internal class (for example
  `CurlComposition`): no reflection, no assembly scanning, and no
  `Microsoft.Extensions.DependencyInjection`, which is a NuGet package rather than part
  of the BCL and so would need Stewart's approval (root `CLAUDE.md`). Native AOT
  (`PublishAot`) must keep working. This task registers only the `file` handler;
  BL-069 and BL-070 add the connectors and the network handlers.
- Standard output is the raw stream from `Console.OpenStandardOutput()` (bytes, no text
  encoding); standard error lines end with `Environment.NewLine`. Inject both streams
  into the runner so tests use `MemoryStream`s.
- Behaviour, measured 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32,
  Release-Date 2026-06-24); each stderr line ended in CRLF on Windows:
  - `curl foo://x/`: exit 1, stderr `curl: (1) Protocol "foo" not supported`.
  - `curl -s foo://x/`: exit 1, stderr empty. `curl -sS foo://x/`: exit 1, the line is
    printed.
  - `curl -sS "dict://exa mple.com/d:x"`: exit 3,
    `curl: (3) URL rejected: Malformed input to a URL function`. Other malformed URLs
    give other texts (`dict://` gives `URL rejected: No host part in the URL`); only this
    one is pinned here, for any URL `Uri.TryCreate(…, UriKind.Absolute, …)` rejects. The
    rest belongs with BL-010.
  - Several URLs: each is transferred in order, a failure does not stop the rest, and
    the exit code is the last transfer's. `curl foo://x/ file:///C:/Windows/win.ini`
    exits 0 after printing the exit 1 line; `curl file:///C:/Windows/win.ini foo://x/`
    exits 1; `curl file:///C:/nonexist/a foo://x/` prints
    `curl: (37) Could not open file C:/nonexist/a` then the exit 1 line, and exits 1.
  - `curl -sS -o a.txt -o b.txt <url1> <url2>`: the first `-o` receives the first URL,
    the second the second; nothing on stdout. A URL with no matching `-o` goes to stdout.
  - `curl -sS -o C:/nonexist/dir/x file:///C:/Windows/win.ini`: exit 23, stderr
    `curl: (23) client returned ERROR on write of 92 bytes` (92 is the file's size).
- A refusal from the parser is printed line by line and its exit code returned; no URL
  is attempted. BL-074 (a dependency) makes a command line with no URL a refusal, so the
  runner never sees an accepted, empty `Urls`.
- Map every `CommandLineOptions` member that has a `TransferContext` counterpart:
  `PostData`, `Credentials`, `TelnetOptions`, `TftpBlockSize`, `TftpNoOptions`.
- Out of scope, and not printed: the progress meter (curl 8.21.0 prints it on stderr
  when output is not a terminal and `-s` is not given; it belongs to
  `Curl.Output.UnitLibrary`, and no task covers it yet), scheme guessing for a URL
  without `scheme://`, Ctrl+C handling, and `-R` (BL-079).

## Acceptance criteria

- [x] `Curl.Console.csproj` grants `InternalsVisibleTo` to `Curl.Console.UnitTests`, and
      `Program.Main` only builds the composition and delegates to the runner.
- [x] The composition root is explicit constructor calls; a test asserts the production
      dispatcher serves `file` through `FileProtocolHandler`, and a search of
      `Curl.Console` finds no `Activator`, `Assembly.`, `GetType(` or `Type.GetType`.
- [x] A test runs the production composition on a `file://` URL naming a temporary file and
      asserts stdout holds its bytes exactly, stderr is empty and the result is 0.
- [x] Named tests, using a fake `IProtocolHandler` and in-memory streams, assert each
      measured case above: the exit 1 line, `-s` silence, `-sS`, the exit 3 line, the
      last-transfer exit code in both orders, both error lines for two failures, and the
      `-o` pairing.
- [x] A named test asserts exit 23 when the `-o` file cannot be created; the stderr
      line it produces is compared with the measured
      `curl: (23) client returned ERROR on write of 92 bytes`, and if it differs a
      follow-up task is filed and named in `Notes`.
- [x] A named test asserts a parser refusal's lines reach stderr, each followed by
      `Environment.NewLine`, with its exit code, and that no handler ran.
- [x] A named test asserts the context a fake handler receives carries `PostData`,
      `Credentials`, `TelnetOptions`, `TftpBlockSize` and `TftpNoOptions` from the
      command line.
- [x] No test is tagged `Integration` and no test opens a socket.
- [x] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

- Plan: `Program.Main` opens the raw standard streams and calls
  `CurlComposition.CreateRunner(...).RunAsync(args)`. `CurlComposition` is two methods of
  plain constructor calls (`FileProtocolHandler(new PhysicalFileSystem())` into a
  `ProtocolDispatcher`). `CurlCommandRunner` parses, builds one `TransferContext` per URL,
  dispatches, and prints `curl: (N) <message>` + `Environment.NewLine` as UTF-8 unless `-s`
  without `-S`. `-o` files go through `DeferredOutputFileStream`, which opens the file
  through the injected `IFileSystem` on the first write, so tests need no disk.
- Exit 23 matches upstream: `FileProtocolHandler` would report
  `Failure writing output to destination, passed 92 returned 0`, so the deferred stream
  records the size of the write whose open failed and `CompleteAsync` replaces the result
  with `client returned ERROR on write of 92 bytes`. No follow-up needed for the text.
- Measured 2026-09-26 with the local curl 8.21.0 and matched: a successful empty transfer
  still creates its `-o` file (0 bytes); when that file cannot be created curl exits 23 and
  prints nothing under `-sS`. A failed transfer does not create its `-o` file.
- Checked the built `curl.exe` against `/mingw64/bin/curl` 8.21.0 for win.ini, `foo://x/`,
  `-s`, the malformed dict URL, both multi-URL orders, two failures, and `-o` into a
  missing directory: exit codes, stdout bytes and stderr lines all identical.
- Choices (no upstream measurement needed or possible here):
  - `CreateFileMode` is mapped too (`--create-file-mode`, default 0644), because it has a
    `TransferContext` counterpart; the task listed five members, this is the sixth.
  - A failure whose `TransferResult.ErrorMessage` is null prints no line. Only the empty
    `-o` case produces one today.
  - An accepted command line with no URL (only the empty command line, until BL-082)
    transfers nothing and returns 0.
  - `-o` files are opened with `FileWriteMode.Truncate` and create mode 0666, what curl's
    `fopen` uses (the umask still applies); `--create-file-mode` does not apply to `-o`,
    as `ITransferContext.CreateFileMode` documents.
  - `DeferredOutputFileStream.Write` (synchronous) throws `NotSupportedException`:
    `IFileSystem` opens asynchronously and blocking on it would break the no-`.Result`
    rule; every handler writes with `WriteAsync`.
- `CurlCompositionTests.CreateRunner_FileUrlOfTemporaryFile_WritesItsBytesToStandardOutput`
  writes a temporary file, which `.claude/rules/testing.md` would tag `Integration`; the
  acceptance criteria require it untagged, so the task wins. It opens no socket.
- A search of `Curl.Console` for `Activator`, `Assembly.`, `GetType(` and `Type.GetType`
  finds nothing. Coverage of `Curl.Console`: 100% lines, 100% branches (34 tests).
- Review (code-reviewer): fixed - a repeated failed `-o` write now keeps the first write's
  size; `DeferredOutputFileStream` overrides `FlushAsync` and `DisposeAsync` and the runner
  closes it with `await using`; `Curl.Console/CLAUDE.md` no longer claims a DI container.
  Declined - applying `--create-file-mode` to `-o` (upstream does not). Left as the task
  scoped them out - scheme guessing for a URL with no `scheme://` (today exit 3), and
  Ctrl+C cancellation. A flush or close failure on the `-o` file at the end is not mapped to
  exit 23; nothing measured it yet.
- Follow-up filed: BL-083 (curl's `Warning: Failed to open the file <path>: <reason>`
  line when `-s` is not given).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. curl.exe runs file:// URLs end to end with curl 8.21.0's error lines, -o pairing and exit codes
