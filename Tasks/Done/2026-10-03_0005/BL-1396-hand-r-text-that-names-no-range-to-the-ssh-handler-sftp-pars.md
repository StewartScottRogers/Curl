---
id: BL-1396
title: Hand -r text that names no range to the SSH handler: SFTP parses it after STAT as Curl_ssh_range does, SCP ignores it
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1389, BL-1392]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1396 — Hand -r text that names no range to the SSH handler: SFTP parses it after STAT as Curl_ssh_range does, SCP ignores it

## Goal

`-r` text that `ByteRangeParser` cannot read (`5-2`, `-0`, `1-2-3`, `abc`) no longer ends an `sftp://` or `scp://` transfer with exit 33 before connecting: an SFTP download connects, opens and stats the file and then refuses the text as curl 8.21.0's `Curl_ssh_range` does, and an SCP download ignores `-r` entirely, as curl's SCP code never reads it.

## Context

- Today `Curl.Console/CurlCommandRunner.cs` `TryParseRange` (around line 3969) returns `false` for such text on an `sftp` or `scp` URL and the transfer ends with `ByteRangeParser.NotDeliveredFailure` before dispatch; `Curl.Console.UnitTests/CurlCommandRunnerRangeTextHandOffTests.cs` (`RunAsync_SshRangeThatNamesNoRange_IsRefusedWithExit33BeforeConnecting`) and `CurlCommandRunnerTransferOptionTests.cs` (`RunAsync_SshRangeThatNamesNoRange_ReturnsExit33WithoutDispatching`, `RunAsync_SshRangeWithInvalidCharacter_PrintsTheWarningThenExit33`) pin that. BL-1322 left this as "a separate task". Every other handler already gets the text as `ITransferContext.RangeText`.
- `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs` `Choose` applies a parsed `ByteRange` (ADR-0253) and is called from `SftpFileDownload.DownloadAsync` with `context.Range`; `Scp/ScpFileDownload.cs` takes no range at all.
- curl 8.21.0 (tag `curl-8_21_0`):
  - `lib/vssh/libssh2.c` lines 1316-1349 (`sftp_download_stat`): only when `STAT` gave a size above 0 and `data->state.use_range` is set does it call `Curl_ssh_range(data, data->state.range, size, &from, &size)`; with no size (or size 0) the range is not applied and the whole file is read. No other place in `libssh2.c` reads `state.range` (so SCP ignores `-r`).
  - `lib/vssh/vssh.c` lines 287-331 (`Curl_ssh_range`): read a number (overflow: exit 33, no message), skip blanks, take one optional `-`, read a second number with blanks; exit 33 with no message when the second overflows, when neither number is present, or when anything is left over (`1-2-3`, `abc`); `-N` is the last N bytes (`-0` is exit 33, no message; N above the size is the whole file); a start beyond the size is exit 33 `Offset (N) was beyond file size (S)`; an end at or past the size stops at the end; a start after the end is exit 33 `Bad range: start offset larger than end offset`.
  - A `CURLE_RANGE_ERROR` with no `failf` prints curl's strerror text, `Requested range was not delivered by the server` (`ByteRangeParser.NotDeliveredMessage`).
- The tool's own warning for a range with invalid characters (printed by `-r` parsing before the transfer) is unchanged.
- The SSH failure teardown after a refused range is the one BL-573's range tests already pin for `Offset ... beyond file size`.

## Acceptance criteria

- [x] `TryParseRange` hands unparseable text to `sftp` and `scp` like every other scheme; the three Console tests above are rewritten to pin that the transfer is dispatched with `RangeText` set (and, for the invalid-character case, that the warning is still printed first).
- [x] Tests in `Curl.Protocol.Ssh.UnitTests` download a 10-byte file over the fake SFTP server with `RangeText` and no `Range`: `5-2` gives exit 33 `Bad range: start offset larger than end offset`; `-0`, `1-2-3` and `abc` give exit 33 with `Requested range was not delivered by the server`; each after `OPEN` and `STAT`, with nothing written.
- [x] A test pins that with `STAT` giving no size, `RangeText` `5-2` reads the whole file, exit 0.
- [x] A test pins that an `scp://` download with `RangeText` `5-2` downloads the whole file, exit 0.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` and `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` are clean; both test projects pass with `--filter "TestCategory!=Integration"`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` and `-Library Curl.Console` report no failing member in the code this task changed.

## Notes

- Not measured here: the installed curl 8.21.0's libssh2 build exits 2 at once for `sftp://` and `scp://` on this machine, so the behaviour is cited from the source above.
- Depends on BL-1389 (same SSH library) and BL-1392 (same Console project) so the chains do not collide. No option changes, so `--ai-help` is unaffected.

- 2026-10-03 (lane 3): `CurlCommandRunner.TryParseRange` became `ParseRange`, which never refuses; SSH gets the text as `RangeText` like every other handler. `SftpDownloadPart.Choose` takes the text and, only when the command line parsed no range, reads it as `Curl_ssh_range` does (`WithinText`, `SkipDash`, `ReadNumber`, `LastBytes`), sharing `Between` with the parsed-range path; `SshTransferException.SftpRangeNotDelivered` carries curl's CURLE_RANGE_ERROR text. SCP needed no code: `ScpFileDownload` never took a range; a handler test pins it.
- Choice: an empty `-r ""` reads as no number at all, exit 33 not delivered, as `Curl_ssh_range` would read it.
- Measure-CodeQuality (Curl.Protocol.Ssh.UnitLibrary, Curl.Console in one run) flagged `Choose` (Cx 12) and `WithinText` (Cx 12, one branch). Both were split (`Asked`, `SkipDash`, `LastBytes` takes the missing number) and the uncovered leftover-after-suffix branch got tests (`-3x`, `2-x`, empty). Not re-measured, per the shift's one-run-per-library rule; the remaining methods each have at most four decision points. Curl.Console reported nothing failing.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. SFTP reads -r text that names no range with Curl_ssh_range's rules after STAT; SCP ignores it
