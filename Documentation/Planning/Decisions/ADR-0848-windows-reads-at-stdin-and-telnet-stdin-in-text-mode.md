# ADR-0848: On Windows, `@-` text consumers and telnet's standard input are read in text mode

- **Status:** Accepted
- **Date:** 2026-10-26
- **Decided by Claude under Stewart's delegation** (BL-3641, CGR-112)

## Context

curl's Windows build reads standard input through the C runtime, whose default for `stdin` is text mode: a Ctrl-Z
byte (0x1A) ends the input and each CR LF pair reads as LF. curl switches `stdin` to binary only where it means to
send bytes unchanged. The third-party gap report (CGR-112) found Curl reading every `@-` consumer and telnet's
standard input in binary. Measured with the local curl 8.22.0 (Git for Windows, `x86_64-w64-mingw32`) on
2026-10-26 through `Record-CurlExchange.ps1`:

| Command | Standard input | curl 8.22.0 sends |
| --- | --- | --- |
| `-d @-`, `--data-ascii @-` | `ab` Ctrl-Z `cd` CR LF `ef` | `ab` |
| `--json @-` | `{` CR LF `"a":1` CR LF `}` | `{` LF `"a":1` LF `}` (Content-Length 9) |
| `--variable v@- --expand-data {{v}}` | `a` CR LF `b` CR `c` | `a` LF `b` CR `c` (a lone CR stays) |
| `-H @-` | `X-A: 1` CR LF `X-B: 2` Ctrl-Z CR LF `X-C: 3` | `X-A` and `X-B` only |
| `-w @-` | `ab` Ctrl-Z `cd` CR LF `ef` LF | prints `ab` |
| `--data-binary @-` | `ab` Ctrl-Z `cd` CR LF `ef` | every byte |
| `--data-urlencode @-` | `ab` Ctrl-Z `cd` CR LF `ef` | `ab%1Acd%0D%0Aef` (binary) |
| `telnet://` | `a` CR LF `b` CR LF Ctrl-Z CR LF `c` LF | `a` LF `b` LF |
| `-T - telnet://` | the same | every byte |

## Decision

`Curl.Cli`'s `WindowsTextModeInput.Contents` gives standard input's bytes as the text-mode read gives them when the
command line acts as the Windows build (`CommandLineOptions.ActsAsWindowsSchannelBuild`), and unchanged otherwise.
`CommandLineOptionTable` applies it to `-d`, `--data-ascii`, `--json`, `-H`, `--proxy-header` and `-w` `@-`
(`ReadAtFile`'s `readsStandardInputAsText`), and `VariableDefinition` to `--variable name@-`; `--data-binary` and
`--data-urlencode` keep the raw bytes. Named files are untouched: this decision is about standard input only.

`Curl.Engine`'s `TransferContextFactory` gives a `telnet` transfer without `-T` its standard input through a
`WindowsTextModeReadStream` while `ReadsTelnetStandardInputInTextMode` is set, which it is by default on Windows
(`OperatingSystem.IsWindows()`); the stream decodes as it reads, holding a CR until the next byte shows whether an LF
follows, so an interactive telnet session still sends each line as it is typed. A `-T` source is never wrapped. The
library handle's factory (`ManagedEasyTransfer`) has no standard input (`Stream.Null`) and turns the setting off.

## Consequences

- Each repro in CGR-112 gives curl 8.22.0's bytes on Windows; on Linux and macOS nothing changes, as curl's builds
  there read every byte.
- The telnet default is read from the platform in `TransferContextFactory` rather than passed in by
  `CurlCommandRunner`, so the change does not touch the runner; tests set the property to pin either platform.
