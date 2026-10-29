# ADR-0909: Linux and macOS read argv's raw bytes and keep each invalid UTF-8 byte as a surrogate escape

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3849)
- Relates to: [ADR-0083](ADR-0083-thin-socket-adapters-are-measured-by-the-integration-run.md)

## Context

CGR-016 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`): on Linux and macOS .NET decodes argv
as UTF-8 before `Main` runs and replaces each invalid byte with U+FFFD, so `curl -d $'\xff' URL` posted
EF BF BD. curl never re-encodes its arguments. Measured with `Record-CurlExchange.ps1` against curl
8.18.0 (OpenSSL, Ubuntu in WSL): `curl -s -d $'\xff' URL` sends `Content-Length: 1` and the byte FF;
`curl -s --data-urlencode $'\xff' URL` sends `Content-Length: 3` and `%FF`.

## Decision

- `Curl.Console`'s `RawCommandLineArguments.ForCurrentPlatform` reads argv again off Windows:
  `/proc/self/cmdline` on Linux (NUL-terminated arguments), libSystem's `_NSGetArgc` and `_NSGetArgv`
  on macOS. It takes argv's last `args.Length` entries (so a `dotnet`-hosted run's own arguments are
  skipped) and decodes each with `InvalidUtf8ByteEscape.Decode`. When argv cannot be read, has too few
  entries, or an entry that is valid UTF-8 does not read as the matching argument .NET gave, the
  arguments stay as .NET gave them. Windows hands over UTF-16 arguments, so nothing changes there.
- `Curl.Cli`'s `InvalidUtf8ByteEscape` is the "surrogate escape" scheme: `Decode` reads UTF-8 and turns
  each byte b outside a valid sequence into the lone surrogate U+DC00 + b (U+DC80 to U+DCFF, since bytes
  below 0x80 are always valid); its `Encoding` writes UTF-8 and turns each such lone surrogate back into
  its byte. Any other lone surrogate encodes as U+FFFD, as `Encoding.UTF8` does, so valid text encodes
  exactly as UTF-8.
- `CommandLineOptions.CommandLineTextBytes` uses that encoding wherever it used UTF-8 (every value off
  Windows, a UTF-8 config file's lines, `--expand-` values), so `-d`, `--data-*`, `--json`,
  `--data-urlencode` and `--url-query` send an argument's bytes unchanged. A config file's and an
  expanded value's text never holds an escaping surrogate, so they still go out as UTF-8.
  `ConfigFileWireTextEncoding` stays Windows-only rather than becoming the escape encoding off Windows,
  as the task suggested: that property also switches how invalid config files and header files are
  read, which is not this task's to change.
- `ForCurrentPlatform` and the two readers are platform adapters left out of the Windows coverage
  measure (ADR-0083); the splitting and line-up logic (`SplitProcCommandLine`, `Select`) is unit tested.

Headers and cookies, URLs, `-w` and `-o` file names build on this in BL-3850 to BL-3853.

## Consequences

An argument that is not valid UTF-8 now reaches the parser as text holding lone surrogates; options that
do not yet encode through `InvalidUtf8ByteEscape.Encoding` still send U+FFFD's bytes for them, as before.
