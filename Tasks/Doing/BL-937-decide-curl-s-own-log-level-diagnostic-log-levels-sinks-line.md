---
id: BL-937
title: Decide Curl's own --log-level diagnostic log: levels, sinks, line format and the no-extra-bytes guarantee
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-29
completed:
---
# BL-937 — Decide Curl's own --log-level diagnostic log: levels, sinks, line format and the no-extra-bytes guarantee

## Goal

An ADR marked "Decided by Claude under Stewart's delegation" fixes every rule of Curl's own diagnostic log (`--log-level none|error|warning|info|verbose`, `--log-file <path>`), and `Documentation/Product/Requirements.md` carries a requirement row for it, so BL-938 and BL-917 to BL-929 build against one written design.

## Context

Stewart asked on 2026-09-29 for "logging to the console output from --none to --verbose", clarified as two things: this Curl-only diagnostic log (what the C# implementation is doing), and closing gaps in curl's own `-v`/`--trace` output (BL-930 to BL-936). This task is the first; it decides, it writes no code.

The diagnostic log is **not** curl's `-v`. `-v`, `--trace`, `--trace-ascii`, `--trace-time`, `--trace-ids` and `--trace-config` keep writing byte for byte what curl 8.21.0 writes (ADR-0046 and the transfer event sink `ITransferEvents`). Real curl has no `--log-level` or `--log-file` (checked against https://curl.se/docs/manpage.html, which documents curl 8.23.0, on 2026-09-29; curl 8.21.0 rejects an unknown option with `curl: option --log-level: is unknown` and exit 2), so like `--ai-help` (BL-911) this is a deliberate, additive departure: no working script can depend on the rejection.

The decisions to record, as decided here (the ADR may sharpen wording, not reverse them without saying why):

1. **Options.** `--log-level <level>` takes `none`, `error`, `warning`, `info` or `verbose`, matched ignoring case; default `none`. `--log-file <path>` sends the log to that file (created or truncated) instead of standard error. `--log-file` without `--log-level` means `info`. Both are global options (they apply to the whole run across `-:`/`--next`, like `--stderr`), and the last occurrence wins. Any other level value is refused with `curl: option --log-level: is badly used here`, curl's `try 'curl --help'` line and exit 2 (`CurlExitCode.FailedInit`, the code every other badly used option already returns). Both work in a `-K` config file like every other long option.
2. **Levels are cumulative:** `error` ⊂ `warning` ⊂ `info` ⊂ `verbose`. `error`: the failure that ends a transfer, with the `CurlExitCode` name and the underlying .NET exception type and message. `warning`: something recovered from (a retry, a fallback, input ignored). `info`: milestones (resolved, connected, TLS negotiated, request sent, reply status, transfer done with byte counts and elapsed milliseconds). `verbose`: every decision and state-machine step.
3. **`none` means zero extra bytes anywhere:** no writer is constructed, no file is created or opened, every component receives `NoDiagnosticLog.Instance`, and standard output, standard error, `--trace` files, `-o` files and exit codes are byte-identical to a run without the option.
4. **Where it writes:** standard error by default, which means wherever `--stderr` has pointed standard error; `--log-file` instead writes only to the file, as UTF-8 without a byte order mark. `-s`/`--silent` does not silence it (the user asked for it explicitly). A `--log-file` that cannot be opened writes `Warning: Failed to open the --log-file <path>` to standard error once, the run continues with no diagnostic log, and the exit code is unchanged.
5. **Line format:** `[<UTC timestamp>] [<level>] [<component>] <message>` then the line end the runner uses for its own standard-error text, e.g. `[2026-09-29T14:03:07.123Z] [info] [http] reply HTTP/1.1 200 after 12 ms`. The timestamp is `TimeProvider.GetUtcNow()` (injected, never `DateTime.Now`) formatted `yyyy-MM-ddTHH:mm:ss.fffZ` with the invariant culture; the level is lower case; one call writes one whole line, and lines from `-Z` parallel transfers never interleave inside a line.
6. **Components** are fixed lower-case names, one per area: `cli`, `runner`, `dns`, `connect`, `proxy`, `tls`, `quic`, `http`, `http2`, `http3`, `auth`, `retry`, `redirect`, `hsts`, `altsvc`, `ftp`, `tftp`, `ssh`, `smtp`, `imap`, `pop3`, `dict`, `gopher`, `telnet`, `mqtt`, `file`, `smb`, `ldap`, `rtsp`, `ws`.
7. **Never logged:** passwords, `-u`/URL user-info passwords, `Authorization`/`Proxy-Authorization` values, cookies' values, private keys, pass phrases, bearer tokens, SASL and NTLM/Negotiate token bytes. A message may say a credential was sent, never what it was.
8. **Shape:** base class library only, native-AOT safe, no `Microsoft.Extensions.Logging` or any other package. A small hand-rolled `IDiagnosticLog` (`bool IsEnabled(DiagnosticLogLevel)`, `void Write(DiagnosticLogLevel, string component, string message)`), the `DiagnosticLogLevel` enum and `NoDiagnosticLog` live in `Curl.Protocol.Abstractions.UnitLibrary` so every protocol library can take one without referencing another project (BL-938); the writer lives in `Curl.Output.UnitLibrary` (BL-917); `Curl.Console`'s composition root (`CurlComposition.cs`) constructs it and passes it by constructor and on `ITransferContext.DiagnosticLog`/`ConnectTarget.DiagnosticLog`, never through a static (BL-919). Callers test `IsEnabled` before building a message so `none` costs no formatting.
9. **Libraries that do not reference the abstractions** (`Curl.Tls`, `Curl.Http2`, `Curl.Http3`, `Curl.Ntlm`, `Curl.Kerberos`, `Curl.Cryptography`, `Curl.Zstandard`) get no new project reference; their steps are logged at the call site in the library that uses them (`Curl.Networking`, `Curl.Protocol.Http`, `Curl.Authentication`).
10. **Not in `--help` or `--manual`**, which stay byte-identical to curl 8.21.0; listed in `--ai-help` (BL-911, BL-918).

## Acceptance criteria

- [ ] A new ADR exists in `Documentation/Planning/Decisions` with the next unused number (check the folder; 0221 was the lowest unused number at filing; duplicate numbers exist, so look for the number itself, not the file count), marked "Decided by Claude under Stewart's delegation", stating decisions 1 to 10 above and naming BL-938 and BL-917 to BL-929 as the tasks that build it.
- [ ] The ADR states the departure from real curl (exit 2 for the unknown option) and why it cannot break a working script, as the `--ai-help` departure is recorded.
- [ ] The ADR is indexed in `Documentation/Planning/Decisions/README.md` with a one-line summary.
- [ ] `Documentation/Product/Requirements.md` has a new FR row (next unused FR number) for `--log-level`/`--log-file`, stating the levels, the default `none` with zero extra bytes, the line format and the refusal of a bad level with exit 2, with status `Draft` and the ADR linked.
- [ ] No `.cs` or project file is changed.

## Notes

- Siblings in Part B (curl's own verbosity): BL-930 to BL-936, and the already filed BL-649, BL-850, BL-578, BL-598, BL-660, BL-734, BL-773.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
