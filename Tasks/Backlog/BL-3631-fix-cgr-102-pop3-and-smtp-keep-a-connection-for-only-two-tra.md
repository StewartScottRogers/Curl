---
id: BL-3631
title: Fix CGR-102: POP3 and SMTP keep a connection for only two transfers; from the third URL on, PORT sends QUIT, reconnects and logs in again
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary/Pop3KeptConnection.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3Session.cs, Curl.Protocol.Pop3.UnitTests/Cgr102Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3631 — Fix CGR-102: POP3 and SMTP keep a connection for only two transfers; from the third URL on, PORT sends QUIT, reconnects and logs in again

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-102 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-102: POP3 and SMTP keep a connection for only two transfers; from the third URL on, PORT sends QUIT, reconnects and logs in again

- **Severity:** Medium
- **Area:** other-protocols
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** GF-0051, GF-0057
- **Source findings:** `other-protocols OP-3`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- `EV\reuse.cases`, `EV\confirm.cases` C08/C09 and `EV/verify-linux.cases`. Commands:
  - `curl -s -S -u u:p pop3://127.0.0.1:21001/1 pop3://127.0.0.1:21001/2 pop3://127.0.0.1:21001/3`
  - `curl -s -S -u u:p "pop3://127.0.0.1:21001/[1-5]"`
  - `curl -s -S --mail-rcpt x smtp://127.0.0.1:21001/1 smtp://127.0.0.1:21001/2 smtp://127.0.0.1:21001/3`

Commands (run each with REF and with PORT):

```sh
curl -s -S -u u:p pop3://127.0.0.1:21001/1 pop3://127.0.0.1:21001/2 pop3://127.0.0.1:21001/3
curl -s -S -u u:p "pop3://127.0.0.1:21001/[1-5]"
curl -s -S --mail-rcpt x smtp://127.0.0.1:21001/1 smtp://127.0.0.1:21001/2 smtp://127.0.0.1:21001/3
```

**Expected (real curl)**

- POP3 uses one connection: `CAPA, AUTH CRAM-MD5, RETR 1, RETR 2, RETR 3, QUIT`. `[1-5]` also uses one connection.
- SMTP: `EHLO 1, VRFY x` three times, then `QUIT`.

**Actual (Curl)**

- POP3: conn 0 sends `CAPA, AUTH, RETR 1, RETR 2, QUIT`, then conn 1 sends `CAPA, AUTH, RETR 3, QUIT`. `[1-5]` uses 3 connections (2 + 2 + 1).
- SMTP: conn 1 starts again with `EHLO 3`.
- Two URLs match REF. IMAP, DICT and RTSP with 3 URLs match REF.

**Suspected location in source**

- `Curl.Protocol.Pop3.UnitLibrary\Pop3Session.cs:360-366` (`QuitOrKeepAsync`).
- `Pop3KeptConnection.cs:53-63`: `TryKeep` returns false when `connection.TryHoldSession(this)` fails on a connection that was already reused.
- SMTP follows the same pattern.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr102Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-102 of the third-party gap report.
