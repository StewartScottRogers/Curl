---
id: BL-1199
title: Write the --trace-config ftp lines for PRET, SITE NAMEFMT and MKD
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1199 — Write the --trace-config ftp lines for PRET, SITE NAMEFMT and MKD

## Goal

Under `-v --trace-config ftp`, `--ftp-pret` (`PRET`), the `SITE NAMEFMT 1` curl sends after `SYST` answered `215 OS/400`, and `--ftp-create-dirs` (`MKD`) write curl 8.21.0's `[FTP]` state lines.

## Context

- Follow-up of BL-1197, whose Notes hold the rules measured so far. `FtpStateTrace.StateOf` (`Curl.Protocol.Ftp.UnitLibrary`) maps these three commands to no state, so they write no state change.
- Also unmeasured there: a `-r` range whose `-` post-quote is refused (Curl writes `done, result=0` before the quotes).
- Measure with `Record-CurlExchange.ps1 -Ftp -FtpReply` (e.g. `'SYST=215 OS/400'`, `'CWD=550 no'` with `--ftp-create-dirs`) before pinning.

## Acceptance criteria

- [x] Tests in `FtpProtocolHandlerStateTraceTests` pin the measured `[FTP]` lines for `--ftp-pret`, `SITE NAMEFMT 1` and `--ftp-create-dirs`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Measured 2026-10-02, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Ftp -FtpData 'hello\n' -CurlArgs '-sS','--trace-config','ftp','-v',...`:

- **`--ftp-pret`** (`PRET=200 OK`): `> PRET RETR a.txt`, `[STOP] -> [PRET]`, `[PRET] perform, awaiting DATA connect`, then `EPSV` writes `[PRET] -> [PASV]`.
- **OS/400** (`PWD=257 "QSYS.LIB"`, `SYST=215 OS/400 V7R4`, `SITE=250 OK`, second `PWD=257 "/QSYS.LIB"`): `SITE NAMEFMT 1` enters `NAMEFMT`, the second `PWD` writes `[NAMEFMT] -> [PWD]`, then `[PWD] -> [STOP]`.
- **`SYST=215 OS/400` alone sends no `SITE NAMEFMT 1`.** curl's `ftp_state_syst_resp` reads the system word up to a space or the end of the receive buffer, which still holds the CRLF, so the word is `OS/400\r\n` and never matches. Curl matched it (BL-782's unmeasured "no commentary" row); `FtpServerSystem.IsOs400` now needs a space after the word, and `FtpProtocolHandlerServerSystemTests` pins `215 OS/400` as measured. Same library, inside `touches`.
- **`--ftp-create-dirs`** (`CWD=550 no` then `250 OK`, `MKD=257 created`): `MKD` enters `MKD`; after its reply curl writes `[MKD] -> [CWD]` *before* `> CWD d` (it sets the state before sending), and the second `CWD` writes nothing more. New `FtpStateTrace.ChangingDirectoryAgain`, called from `TryChangeDirectoryAsync`.

Delivered: `FtpStateTrace.StateOf` maps `PRET`, `MKD` and `SITE` (only `SITE NAMEFMT 1` goes through `ExchangeAsync`; `-Q` quotes use `QuoteSent`). 3 new trace tests, 1 new and 2 changed server-system rows; the test `MutableContext` gained `FtpSendPret`. FTP tests 562 green.

Not done here: the `-r` range with a refused `-` post-quote (Context's last bullet) is not in this task's criteria; filed as BL-1201.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config ftp writes curl's [FTP] lines for PRET, SITE NAMEFMT 1 and MKD; 215 OS/400 alone no longer sends SITE NAMEFMT
