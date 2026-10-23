---
id: BL-3169
title: Wrap long curl: error messages at 79 columns as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitLibrary/WrappedMessage.cs, Curl.Cli.UnitTests/CommandLineRefusalWrappingTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3169 — Wrap long curl: error messages at 79 columns as curl 8.22.0 does

## Goal

Long `curl:` error messages are wrapped across lines exactly as curl 8.22.0 wraps them, byte for byte.

## Context

Stewart reported on 2026-10-22 that curl wraps long `curl:` messages at spaces. Measured the same day on Windows with System32 curl 8.21.0 Schannel: `curl -sS --cacert /some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate-authority-bundle.pem https://x/` prints three lines, `curl: The file `, `curl: '/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate` (79 chars) and `curl: -authority-bundle.pem' provided to --cacert does not exist`. So curl wraps at 79 columns and breaks inside a long token, not only at spaces (the report said "at spaces"; the measurement says otherwise). Curl prints one 146-character line. Curl already wraps `Warning:` lines and, for some `curl:` lines, via `Curl.Cli.UnitLibrary/WrappedMessage.cs` (a port of `voutf` in tool_msgs.c). The message in question is built in `Curl.Cli.UnitLibrary/CommandLineRefusal.cs` near line 377. Measure curl 8.22.0 (not 8.21.0) with `Record-CurlExchange.ps1` for ten long-path cases and find upstream's wrapping rule in tool_msgs.c; apply `WrappedMessage` to every `curl:` message upstream wraps. If the wrapping must be applied where `CurlCommandRunner` prints the line, only add the narrowest change needed there (it is a very large file other tasks edit; add it to `touches` before editing it).

Also note: on the same run real Windows curl 8.21 printed `curl: try 'curl --help' for more information` (no `curl --manual`), while Curl prints `curl: try 'curl --help' or 'curl --manual' for more information` (`CommandLineRefusal.TryHelpLine`). Measure 8.22.0's Windows build. If its try-help line differs by platform, do not change the constant in this task: file that as its own task. It touches the same constant as BL-3145 (`Curl.Cli.UnitLibrary/CommandLineRefusal.cs`, whose no-arguments line the "small conformance fixes" task reverses), so that follow-up must depend on the conformance-fixes task so one task owns the constant.

## Acceptance criteria

- [x] Notes record curl 8.22.0's exact bytes for ten long-path cases (different options and path lengths, a path with and without spaces, `COLUMNS` unset, 40 and 200), and the wrapping rule found in tool_msgs.c.
- [x] `Curl.Cli.UnitTests/CommandLineRefusalWrappingTests.cs` has the ten cases, each comparing Curl's standard-error lines byte for byte with the recorded ones.
- [x] The long `--cacert` case produces the three lines quoted above (or 8.22.0's measured equivalent) with each line at most 79 bytes.
- [x] Short messages and messages that are wrapped today (`Warning:` lines) are unchanged: the existing `WrappedMessage` and refusal tests still pass.
- [x] Notes say whether curl 8.22.0's try-help line differs on Windows from Linux/macOS, and name the follow-up task filed if it does.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-10-22 on Windows with Git for Windows' ucrt64 curl 8.22.0 (Schannel), run straight from Git Bash with `MSYS_NO_PATHCONV=1` (stderr only, no server needed, so Record-CurlExchange's `-NoServer` added nothing; PowerShell 5's redirection mangled native stderr). Each run was `-sS <option> <path> <url>`, exit 2 (26 for `-d`), and every case ended with `curl: option <option>: is badly used here` (or `-d`'s reading-error reason) and the try-help line. Lines are shown without terminators (curl ends each with CRLF on Windows) and separated by ` | `. L = `/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate-authority-bundle.pem`, S = `/some dir/with spaces in it/and more words here/to make it/long enough/to wrap/bundle.pem`, N = L + `/extra/segments/to/make/it/much/longer/than/one/whole/line/of/text`.
  1. `--cacert L`, COLUMNS unset: `curl: The file ` | `curl: '/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate` (79) | `curl: -authority-bundle.pem' provided to --cacert does not exist`. Same bytes as 8.21.0's measurement in Context.
  2. `--cacert L`, COLUMNS=40: `curl: The file ` | `curl: '/some/very/long/missing/path/that` | `curl: /goes/on/and/on/for/a/while/certif` | `curl: icate-authority-bundle.pem' ` | `curl: provided to --cacert does not ` | `curl: exist`.
  3. `--cacert L`, COLUMNS=200: one 146-byte line, unwrapped.
  4. `--cacert S`, unset: `curl: The file '/some dir/with spaces in it/and more words here/to make ` | `curl: it/long enough/to wrap/bundle.pem' provided to --cacert does not exist`.
  5. `--cacert S`, COLUMNS=40: `curl: The file '/some dir/with spaces ` | `curl: in it/and more words here/to make ` | `curl: it/long enough/to ` | `curl: wrap/bundle.pem' provided to ` | `curl: --cacert does not exist`.
  6. `--proxy-cacert L`, unset: as 1 with `--proxy-cacert`.
  7. `--proxy-cacert S`, unset: as 4 up to `to make `, then `curl: it/long enough/to wrap/bundle.pem' provided to --proxy-cacert does not ` | `curl: exist`.
  8. `--knownhosts L`, unset: as 1 with `--knownhosts`.
  9. `--netrc-file N`, unset: as 1's first two lines, then `curl: -authority-bundle.pem/extra/segments/to/make/it/much/longer/than/one/whol` | `curl: e/line/of/text' provided to --netrc-file does not exist`.
  10. `--netrc-file N/and/then/even/more/text/beyond/that/and/more/words/to/pass/two/hundred/bytes`, COLUMNS=200: `curl: The file ` | a 200-byte line cut inside the path after `beyond/th` | `curl: at/and/more/words/to/pass/two/hundred/bytes' provided to --netrc-file does not exist`.
  11. `--cacert /a/b/c/xxxxxxxxxxxxxxx.pem`: exactly 79 bytes, one line (a message equal to the width is not cut).
  12. `-d @L`: `curl: Failed to open ` | `curl: /some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate-` (79) | `curl: authority-bundle.pem`, exit 26: `Failed to open` wraps too.
  All twelve are pinned in `Curl.Cli.UnitTests/CommandLineRefusalWrappingTests.cs`.
- Upstream rule (`voutf` in `src/tool_msgs.c`): width = terminal columns (`COLUMNS` 21..10000, else 79) minus the prefix length; while the rest is longer than width, cut after the last blank at or before index width-1 (index 0 not considered), or at width when there is none, keeping the blank at the line's end. `WrappedMessage` already ported it, and every measured case matched it with no change, so the fix was to send `FileDoesNotExist` and `DataFileUnreadable` (both `errorf` upstream) through it. The `option ...: <reason>` and try-help lines come from `helpf`, which does not wrap, so they stay unwrapped.
- Decision (sensible default, matching curl): both methods take an optional `int? terminalColumns`; `null` reads this process's `COLUMNS`, as `ErrorMessageLines` in the same file already does. Overloads were tried first but made existing crefs in `CommandLineOptionTable.cs` (outside touches) ambiguous. Passing the parse's own environment from the callers is filed as BL-3179.
- Try-help line: curl 8.22.0's Windows build (Git for Windows ucrt64) prints `curl: try 'curl --help' or 'curl --manual' for more information`, the same as `CommandLineRefusal.TryHelpLine` and upstream's Linux/macOS builds; System32's 8.21.0 leaves out `--manual` because it is built without the manual, not because of the platform. So it does not differ by platform, no follow-up task was filed and the constant is unchanged.
- The new tests are pure string work with POSIX-style paths and no file system, so they are platform-neutral. Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary: 0 failing members. Curl.Cli.UnitTests: 4128 passed, 18 skipped, 0 failed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 6af107f0 d3cdbed2 0471c273 d1212222 61cabad7 ae25be8d.
- 2026-10-22: Doing -> Done. Long missing-file and @file curl: errors wrap at COLUMNS (79 default) byte for byte as curl 8.22.0 does
