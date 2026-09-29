# ADR-0222 — Curl's own diagnostic log is `--log-level` and `--log-file`, off by default with zero extra bytes

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-937.
Built by BL-938 (the contract in `Curl.Protocol.Abstractions.UnitLibrary`) and BL-917 to
BL-929 (the writer, the option parsing, the composition root and the log calls in each
area).

## Context

Stewart asked on 2026-09-29 for "logging to the console output from --none to --verbose",
and clarified it as two separate things:

1. A diagnostic log of what the C# implementation itself is doing - which component made
   which decision, and why a transfer failed underneath its `CurlExitCode`. This ADR.
2. Closing the gaps in curl's own `-v`/`--trace` output. That is not this ADR: it is
   BL-930 to BL-936 (and the earlier BL-649, BL-850, BL-578, BL-598, BL-660, BL-734,
   BL-773), and it stays governed by ADR-0046.

`-v`, `--trace`, `--trace-ascii`, `--trace-time`, `--trace-ids` and `--trace-config`
already write, byte for byte, what curl 8.21.0 writes, through the transfer event sink
`ITransferEvents` (ADR-0046). They cannot carry anything curl does not print without
breaking the drop-in promise, so the implementation's own story needs a channel of its own
that is invisible unless asked for.

Real curl has no such option. The manpage at https://curl.se/docs/manpage.html
(curl 8.23.0, checked 2026-09-29) documents neither `--log-level` nor `--log-file`, and
curl 8.21.0 refuses them: `curl: option --log-level: is unknown`, curl's
`try 'curl --help'` line, exit 2.

## Decision

1. **Options.** `--log-level <level>` takes `none`, `error`, `warning`, `info` or
   `verbose`, matched ignoring case; the default is `none`. `--log-file <path>` sends the
   log to that file, created or truncated, instead of standard error. `--log-file` without
   `--log-level` means `info`. Both are global options: they apply to the whole run across
   `-:`/`--next`, like `--stderr`, and the last occurrence wins. Any other level value is
   refused with `curl: option --log-level: is badly used here`, curl's
   `curl: try 'curl --help' or 'curl --manual' for more information` line and exit 2
   (`CurlExitCode.FailedInit`, the code every other badly used option already returns).
   Both work in a `-K` config file like every other long option.
2. **Levels are cumulative:** `error` ⊂ `warning` ⊂ `info` ⊂ `verbose`.
   - `error`: the failure that ends a transfer, with the `CurlExitCode` name and the
     underlying .NET exception type and message.
   - `warning`: something recovered from - a retry, a fallback, input ignored.
   - `info`: milestones - resolved, connected, TLS negotiated, request sent, reply status,
     transfer done with byte counts and elapsed milliseconds.
   - `verbose`: every decision and state-machine step.
3. **`none` means zero extra bytes anywhere.** No writer is constructed, no file is created
   or opened, every component receives `NoDiagnosticLog.Instance`, and standard output,
   standard error, `--trace` files, `-o` files and exit codes are byte-identical to a run
   without the option.
4. **Where it writes.** Standard error by default, which means wherever `--stderr` has
   pointed standard error. `--log-file` writes only to the file instead, as UTF-8 without a
   byte order mark. `-s`/`--silent` does not silence it: the user asked for it explicitly.
   A `--log-file` that cannot be opened writes
   `Warning: Failed to open the --log-file <path>` to standard error once; the run
   continues with no diagnostic log and its exit code is unchanged.
5. **Line format:** `[<UTC timestamp>] [<level>] [<component>] <message>`, then the line
   end the runner uses for its own standard-error text, for example
   `[2026-09-29T14:03:07.123Z] [info] [http] reply HTTP/1.1 200 after 12 ms`. The
   timestamp is the injected `TimeProvider.GetUtcNow()` (never `DateTime.Now`), formatted
   `yyyy-MM-ddTHH:mm:ss.fffZ` with the invariant culture. The level is lower case. One
   call writes one whole line, and lines from `-Z` parallel transfers never interleave
   inside a line.
6. **Components** are fixed lower-case names, one per area: `cli`, `runner`, `dns`,
   `connect`, `proxy`, `tls`, `quic`, `http`, `http2`, `http3`, `auth`, `retry`,
   `redirect`, `hsts`, `altsvc`, `ftp`, `tftp`, `ssh`, `smtp`, `imap`, `pop3`, `dict`,
   `gopher`, `telnet`, `mqtt`, `file`, `smb`, `ldap`, `rtsp`, `ws`. A new area adds its
   name here first.
7. **Never logged:** passwords, `-u` and URL user-info passwords, `Authorization` and
   `Proxy-Authorization` values, cookie values, private keys, pass phrases, bearer tokens,
   and SASL, NTLM and Negotiate token bytes. A message may say a credential was sent,
   never what it was.
8. **Shape.** Base class library only and native-AOT safe: no
   `Microsoft.Extensions.Logging` or any other package. A small hand-rolled
   `IDiagnosticLog` (`bool IsEnabled(DiagnosticLogLevel level)`,
   `void Write(DiagnosticLogLevel level, string component, string message)`), the
   `DiagnosticLogLevel` enum and `NoDiagnosticLog` live in
   `Curl.Protocol.Abstractions.UnitLibrary`, so every protocol library can take one
   without referencing another project (BL-938). The writer lives in
   `Curl.Output.UnitLibrary` (BL-917). `Curl.Console`'s composition root
   (`CurlComposition.cs`) constructs it and passes it by constructor and on
   `ITransferContext.DiagnosticLog` and `ConnectTarget.DiagnosticLog`, never through a
   static (BL-919). Callers test `IsEnabled` before building a message, so `none` costs no
   formatting.
9. **Libraries that do not reference the abstractions** - `Curl.Tls`, `Curl.Http2`,
   `Curl.Http3`, `Curl.Ntlm`, `Curl.Kerberos`, `Curl.Cryptography`, `Curl.Zstandard` - get
   no new project reference. Their steps are logged at the call site in the library that
   uses them (`Curl.Networking`, `Curl.Protocol.Http`, `Curl.Authentication`).
10. **Not in `--help` or `--manual`**, which stay byte-identical to curl 8.21.0. Both
    options are listed in `--ai-help` (BL-911, BL-918).

### The departure from real curl

Like `--ai-help` (BL-911), this is a deliberate, additive departure. Real curl rejects
`--log-level` and `--log-file` as unknown options with exit 2 before any transfer, so no
working script can pass either one today: a script that did would already be failing. The
only observable change is that a command which used to fail at once now runs. Every
command line real curl accepts keeps its exact bytes and exit code, because with neither
option given the level is `none` (decision 3). The one text that could collide - the name
`--log-level` itself - is not used by any curl release up to 8.23.0; if a future curl
adds an option of that name, a new ADR decides between curl's meaning and this one, and
the drop-in rule says curl's wins.

## Consequences

- Every component can explain itself on demand without touching curl's `-v`/`--trace`
  bytes, and a failure's .NET exception is no longer lost behind its exit code.
- `none` is enforced by construction (no writer exists), so the drop-in promise does not
  rest on every call site getting a check right.
- Every library that logs takes one more constructor dependency, and each area's tests
  must show both the lines it writes and that `NoDiagnosticLog` writes nothing.
- Decision 7 is a standing review item: every new log message that touches a request must
  be read for credentials.
- The component list (decision 6) and the line format (decision 5) are a contract that
  users may grep; changing either needs a new ADR.

## Alternatives considered

- **Add the lines to `-v` or `--trace`.** Lost: those write curl's bytes exactly
  (ADR-0046); any extra line breaks scripts that parse them.
- **`Microsoft.Extensions.Logging`.** Lost: a package (needs Stewart's approval), trim risk
  under native AOT, and far more than five levels and two sinks need.
- **An environment variable (`CURL_LOG_LEVEL`) instead of options.** Lost: invisible in
  the command line a user shares, not settable per `-K` file, and curl reads several
  `CURL_*` variables, so a name could collide silently.
- **Numeric levels (`--log-level 3`).** Lost: names read unambiguously in a script; a
  number says nothing about what it turns on.
- **A static logger.** Lost: CLAUDE.md forbids static service locators, and parallel `-Z`
  tests need a log per run.
- **Silence it under `-s`.** Lost: `-s` silences curl's own progress and errors; the user
  who passes `--log-level` asked for this output in the same command.
