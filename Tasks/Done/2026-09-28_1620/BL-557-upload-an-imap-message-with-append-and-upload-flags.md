---
id: BL-557
title: Upload an IMAP message with APPEND and --upload-flags
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-553]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-557 — Upload an IMAP message with APPEND and --upload-flags

## Goal

`-T <file> imap://host/<mailbox>` sends `APPEND <mailbox> (<flags>) {<n>}`, waits for the `+` continuation, sends the message, and maps a refusal to curl 8.21.0's exit code and message, with the flag list built from `--upload-flags` as curl builds it.

## Context

- Conformance audit 2026-09-28, rows 31 and 34. `--upload-flags` is parsed by BL-535 and carried on the context (BL-534).
- Measure with `Record-CurlExchange.ps1 -Imap`: `-T mail.txt imap://h/INBOX` with no flags, `--upload-flags seen,flagged`, `--upload-flags draft,-seen` (record what curl sends for an unset flag), `-T -` (unknown length: record whether curl refuses or buffers), and `APPEND` answered `NO`.

## Acceptance criteria

- [x] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Imap.UnitTests` pin client bytes byte for byte and the outcome for each case; upload progress and `%{size_upload}` match the measured values.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28, curl 8.21.0 (Schannel), `Record-CurlExchange.ps1 -Imap`, `-sS -u u:p -T mail.txt -w "%{size_upload} %{exitcode}"`, mail.txt = `Subject: hi\r\n\r\nbody\r\n` (21 bytes). After CAPABILITY and AUTHENTICATE PLAIN:

| Case | curl sent | stderr | stdout / exit |
| --- | --- | --- | --- |
| `imap://h/INBOX`, no `--upload-flags` | `A003 APPEND INBOX (\Seen) {21}\r\n`, after `+` the 21 bytes, then `\r\n`, then `A004 LOGOUT` | none | `21 0` |
| `--upload-flags seen,flagged` | `APPEND INBOX (\Flagged \Seen) {21}` | none | `21 0` |
| `--upload-flags draft,-seen` | `APPEND INBOX (\Draft) {21}` | none | `21 0` |
| `--upload-flags seen,flagged,draft,deleted,answered` | `APPEND INBOX (\Answered \Deleted \Draft \Flagged \Seen) {21}` | none | `21 0` |
| `--upload-flags -seen` | `APPEND INBOX {21}` (no parentheses) | none | `21 0` |
| `imap://h/My%20Box` | `APPEND "My Box" (\Seen) {21}` | none | `21 0` |
| `imap://h/INBOX;UID=1` | `APPEND INBOX (\Seen) {21}` (UID ignored) | none | `21 0` |
| `imap://h/` | `APPEND mail.txt ...` (the command line appends the file name) | none | `21 0` |
| `imap://h/;UID=1` | `A003 LOGOUT` only | `curl: (3) Cannot APPEND without a mailbox.` | `0 3` |
| `-T -` | `A003 LOGOUT` only | `curl: (25) Cannot APPEND with unknown input file size` | `0 25` |
| empty file | `APPEND INBOX (\Seen) {0}`, after `+` only `\r\n` | none | `0 0` |
| `-ImapReply 'APPEND=NO [TRYCREATE] no such mailbox'` | APPEND, `+`, the 21 bytes, `\r\n`, `A004 LOGOUT` | `curl: (25) Upload failed (at start/before it took off)` | `21 25` |

Progress meter (no `-s`): final line `100     21   0      0 100     21 ...`, i.e. total 21, uploaded 21: the handler reports `ReportTransferStarted` then `ReportUploaded(21, 21)`.

`--upload-flags` with an empty or unknown name (`''`, `Seen`) is refused by curl with exit 2 `option --upload-flags: is unknown`; that is the command line's job (BL-535), not this task's.

Plan and decisions (sensible defaults, no ADR needed; each follows curl's `imap_perform_append`):
- New `ImapAppend` class keeps the upload out of the already long `ImapSession`; `ImapControlChannel.SendBytesAsync` sends the literal raw.
- The flag names arrive from `MailRequestOptions.UploadFlags` already in curl's order (`MailRequestOptionsMapping`); each is sent as `\` plus the name with its first letter capitalised.
- An upload's size is known when the stream can seek; an unseekable one (`-T -`) is exit 25 before APPEND, as measured.
- `APPEND` answered with anything but a `+` continuation (not measurable with the recorder, which always sends `+`) is exit 25 with the same curl error text and nothing uploaded, as `imap_state_append_resp` sets `CURLE_UPLOAD_FAILED` without a `failf`; LOGOUT follows, as in every measured failure.
- `%{size_upload}` comes from `TransferReport.UploadSize`, set to the bytes sent on success and on a refused completion (measured `21 25`).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -T to an imap:// mailbox sends APPEND with the --upload-flags flags and the message, with curl 8.21.0's exit codes for no mailbox, unknown size and a refusal
