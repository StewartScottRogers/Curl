---
id: BL-635
title: Send FTP ACCT, the alternative USER command and PRET, failing PRET with exit 84
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-634, BL-914]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-635 — Send FTP ACCT, the alternative USER command and PRET, failing PRET with exit 84

## Goal

The FTP handler sends `ACCT <account>` when the server answers `332` and `--ftp-account` is given, retries a refused `USER` with the `--ftp-alternative-to-user` command, and sends `PRET <command>` before `PASV`/`EPSV` with `--ftp-pret`, failing a refused `PRET` with exit 84 (`CURLE_FTP_PRET_FAILED`), each as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 25 (Major). Options: BL-634.
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`; context mapping in `Curl.Console/TransferContextFactory.cs`. New `ITransferContext` members belong in Abstractions: if needed, file an Abstractions task, depend on it, and keep this task to FTP and Console.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp -FtpReply`: `PASS` answered `332` with and without `--ftp-account`, `USER` answered `530` with `--ftp-alternative-to-user "USER alt"`, `--ftp-pret` with `PRET` answered `200` and `500`, for a download and a listing; commands, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin commands and outcome for each case.
- [x] `ACCT` answered with anything but `230` ends with exit 11 (`CurlExitCode.FtpWeirdPassReply`) and `ACCT rejected by server: <code>`, pinned for `202` and `530` (measured in BL-662: `curl: (11) ACCT rejected by server: 530`; ADR-0216).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29 (lane 3): `ITransferContext` has no member for `--ftp-account`, `--ftp-alternative-to-user` or `--ftp-pret`, and every other FTP option reaches the handler that way. As the Context says, the contract change is filed separately as BL-914 (Abstractions), which this task now depends on. Abstractions is also in BL-819's `touches` (Doing), so it cannot be done here.
- 2026-09-29 (lane 4): Measured with curl 8.21.0 (x86_64-w64-mingw32, Schannel), `Record-CurlExchange.ps1 -Ftp -FtpData 'hello\n' -FtpReply ...`, `curl -sS [-u u:p] ... ftp://127.0.0.1:<port>/f.txt` (control commands only; `PWD`, `TYPE`, `SIZE`, `RETR`, `QUIT` as usual after login):

| Case | Commands | Exit | stderr |
| --- | --- | --- | --- |
| `PASS=332`, no `--ftp-account` | `USER u`, `PASS p` | 67 | `curl: (67) ACCT requested but none available` |
| `PASS=332`, `ACCT=230`, `--ftp-account acc` | `USER u`, `PASS p`, `ACCT acc`, `PWD`, `EPSV`, ... | 0 | none |
| same, `ACCT=530` / `202` / `332` | ... `ACCT acc` | 11 | `curl: (11) ACCT rejected by server: 530` (`202`, `332`), no `QUIT` |
| `PASS=230`, `--ftp-account acc` | no `ACCT` | 0 | none |
| `USER=332`, `ACCT=230`, `--ftp-account acc` | `USER u`, `ACCT acc`, `PWD` ... | 0 | none |
| `USER=332`, no account | `USER u` | 67 | `curl: (67) ACCT requested but none available` |
| `USER=530` then `331`, `--ftp-alternative-to-user "USER alt"` | `USER u`, `USER alt`, `PASS p`, ... | 0 | none (same for `USER=500`) |
| `USER=530` then `230`, alternative | `USER u`, `USER alt`, `PWD` ... | 0 | none |
| `USER=530` then `332`, alternative and account | `USER u`, `USER alt`, `ACCT acc` ... | 0 | none |
| `USER=530` twice, alternative | `USER u`, `USER alt` | 67 | `curl: (67) Access denied: 530` |
| `USER=530`, no alternative | `USER u` | 67 | `curl: (67) Access denied: 530` |
| `USER=421`, alternative | `USER u` | 28 | `curl: (28) Timeout was reached` |
| `PASS=530`, alternative | `USER u`, `PASS p`, `USER alt`, `PASS p` | 67 | `curl: (67) Access denied: 530` |
| `PASS=331` then `230`, alternative | `USER u`, `PASS p`, `USER alt`, `PASS p`, ... | 0 | none (without the alternative: 67 `Access denied: 331`) |
| no `-u`, alternative, `USER=530` | `USER anonymous`, `USER alt`, `PASS ftp@example.com` ... | 0 | none |
| `--ftp-pret`, `PRET=200`, download | `PWD`, `PRET RETR f.txt`, `EPSV`, `TYPE I`, `SIZE f.txt`, `RETR f.txt` | 0 | none |
| `--ftp-pret`, `PRET=500` / `202`, download | `PWD`, `PRET RETR f.txt` | 84 | `curl: (84) PRET command not accepted: 500` (`202`), no `QUIT`; `-v` adds `* PRET command not accepted: 500` |
| `--ftp-pret`, `PRET=421` | `PRET RETR f.txt` | 28 | `curl: (28) Timeout was reached` |
| `--ftp-pret`, `PRET=200`, listing `/` (and `-l`) | `PRET LIST` (`PRET NLST`), `EPSV`, `TYPE A`, `LIST` (`NLST`) | 0 | none |
| `--ftp-pret`, `PRET=500`, listing | `PRET LIST` | 84 | `curl: (84) PRET command not accepted: 500` |
| `--ftp-pret --ftp-method nocwd`, `/d/` | `PRET LIST`, `EPSV`, `TYPE A`, `LIST d` | 0 | none |
| `--ftp-pret`, `/d/f.txt` | `CWD d`, `PRET RETR f.txt`, `EPSV` | 0 | none |
| `--ftp-pret -R` | `MDTM f.txt`, `PRET RETR f.txt`, `EPSV` | 0 | none |
| `--ftp-pret --disable-epsv` | `PRET RETR f.txt`, `PASV` | 0 | none |
| `--ftp-pret`, `EPSV=500` | `PRET RETR f.txt`, `EPSV`, `PASV` (PRET once) | 0 | none |
| `--ftp-pret -T file` (and `-a -T file`) | `PRET STOR up.txt`, `EPSV`, `TYPE I`, `STOR up.txt` (`APPE up.txt`) | 0 | none |
| `--ftp-pret -P -` | `EPRT ...`, no `PRET` | 0 | none |
| `--ftp-pret -I` | `MDTM`, `TYPE I`, `SIZE`, `REST 0`, no `PRET` | 0 | none |

Model (no ADR: it copies curl's `ftp_state_user_resp` and `FTP_PRET` handling rather than choosing): one login answer, `AnswerLoginReplyAsync` - `331` to a user command sends `PASS`; a 2xx logs in; `332` sends `ACCT` (none: 67), whose reply must be exactly `230` (else 11); anything else sends the alternative command once, else 67 `Access denied`. `PRET` goes at the head of the passive path only, after `CWD`/`MDTM`, and must be answered exactly `200`. The `-v` line for a refused `PRET` comes from the runner's usual failure line, as for every other failure. Console: `TransferContextFactory` copies the three options. `FtpProtocolHandlerDiagnosticLogTests.ExecuteAsync_FtpAccount_NeverLogsTheAccount` now sends a real `ACCT` (answered 530, exit 11) and still finds no secret in the log. `--ai-help` needs no change: this task adds no option, and the help rows came with BL-634.

Gates: `dotnet build Curl.slnx -warnaserror` clean; every fast test project green (Curl.Protocol.Ftp.UnitTests 501, Curl.Console.UnitTests 1743 passed, 13 platform skips); `Measure-CodeQuality.ps1`: Curl.Protocol.Ftp.UnitLibrary and Curl.Console each 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waits on BL-914: ITransferContext members for --ftp-account, --ftp-alternative-to-user and --ftp-pret
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. FTP sends ACCT on 332 (exit 11 when refused), the --ftp-alternative-to-user command once after a refused login, and PRET before EPSV/PASV under --ftp-pret (exit 84 when refused), as curl 8.21.0 does
