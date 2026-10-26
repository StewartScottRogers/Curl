---
id: BL-3632
title: Fix CGR-103: SASL LOGIN with an empty password sends = where curl 8.22 sends an empty line (SMTP, POP3, IMAP)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Protocol.Imap.UnitTests/Cgr103Tests.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3Login.cs, Curl.Protocol.Pop3.UnitTests/Cgr103Tests.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs, Curl.Protocol.Smtp.UnitTests/Cgr103Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3632 — Fix CGR-103: SASL LOGIN with an empty password sends = where curl 8.22 sends an empty line (SMTP, POP3, IMAP)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-103 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-103: SASL LOGIN with an empty password sends `=` where curl 8.22 sends an empty line (SMTP, POP3, IMAP)

- **Severity:** Medium
- **Area:** other-protocols
- **Platform(s):** Both (Windows and Linux). Detail: Both. PORT sends `=` everywhere. The 8.22 reference is only on Windows; REF-LINUX 8.18 also sends `=`, so 8.22 changed this.
- **Confidence:** Confirmed. Detail: Confirmed (Windows, against 8.22).
- **Related existing finding (GF/AF):** none
- **Source findings:** `other-protocols OP-4`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- `EV\confirm.cases` C01-C03. The server answers `334 VXNlcm5hbWU6` / `+ VXNlcm5hbWU6`, then `UGFzc3dvcmQ6`. Commands:
  - `curl -s -S -u user: --login-options AUTH=LOGIN smtp://127.0.0.1:21001 --mail-rcpt x`
  - `curl -s -S -u user: --login-options AUTH=LOGIN pop3://127.0.0.1:21001/1`
  - `curl -s -S -u user: --login-options AUTH=LOGIN "imap://127.0.0.1:21001/INBOX;UID=1"`

Commands (run each with REF and with PORT):

```sh
curl -s -S -u user: --login-options AUTH=LOGIN smtp://127.0.0.1:21001 --mail-rcpt x
curl -s -S -u user: --login-options AUTH=LOGIN pop3://127.0.0.1:21001/1
curl -s -S -u user: --login-options AUTH=LOGIN "imap://127.0.0.1:21001/INBOX;UID=1"
```

**Expected (real curl)**

- *REF behaviour (8.22):* `AUTH LOGIN\r\n` `dXNlcg==\r\n` `\r\n`. IMAP sends `A002 AUTHENTICATE LOGIN dXNlcg==\r\n\r\n`.

**Actual (Curl)**

- `AUTH LOGIN\r\n` `dXNlcg==\r\n` `=\r\n`

**Suspected location in source**

- `Curl.Protocol.Smtp.UnitLibrary\SmtpSaslAuthentication.cs:145`: `Encode` maps an empty message to `"="`.
- The same in `Curl.Protocol.Imap.UnitLibrary\ImapAuthentication.cs:245` and `Curl.Protocol.Pop3.UnitLibrary\Pop3Login.cs:67`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr103Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-103 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
