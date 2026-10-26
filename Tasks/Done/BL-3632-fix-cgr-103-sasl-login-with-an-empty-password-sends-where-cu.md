---
id: BL-3632
title: Fix CGR-103: SASL LOGIN with an empty password sends = where curl 8.22 sends an empty line (SMTP, POP3, IMAP)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Protocol.Imap.UnitTests/Cgr103Tests.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3Login.cs, Curl.Protocol.Pop3.UnitTests/Cgr103Tests.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs, Curl.Protocol.Smtp.UnitTests/Cgr103Tests.cs, Curl.Authentication.UnitLibrary/ScriptedSaslExchange.cs, Curl.Authentication.UnitLibrary/SaslAuthenticator.cs, Curl.Authentication.UnitTests/Cgr103Tests.cs, Curl.Authentication.UnitTests/SaslAuthenticatorTests.cs, Curl.Protocol.Abstractions.UnitLibrary/ISaslExchange.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr103Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 3): The fix landed in `Curl.Authentication.UnitLibrary`, not the handlers. LOGIN is a `ScriptedSaslExchange`, which now reports `SendsLastAnswerAsEmptyLine` as `true`. LOGIN's password is the only empty answer it can give. The SMTP, POP3 and IMAP handlers already honour that flag (ADR-0754), so their files in `touches` are unchanged and need no Cgr103Tests of their own. Added to `touches`: `ScriptedSaslExchange.cs`, `SaslAuthenticator.cs` (doc comment), `ISaslExchange.cs` (doc comment that said LOGIN sends `=`), `Curl.Authentication.UnitTests/Cgr103Tests.cs` (the ADR-0482 test) and `SaslAuthenticatorTests.cs`, whose `Begin_Login_SendsAnEmptyAnswerAsEquals` pinned 8.21's `=` and was removed. No live task on origin/work/dark-factory names any of them. Decision recorded as ADR-0826. An empty initial response (empty user) still sends `=`.
- Measured with `Record-CurlExchange.ps1` (-Smtp/-Pop3/-Imap) against curl 8.22.0 Schannel and against Curl. Commands: `-s -S -u user: --login-options AUTH=LOGIN` with `smtp://127.0.0.1:P --mail-rcpt x`, `pop3://127.0.0.1:P/1` and `imap://127.0.0.1:P/INBOX;UID=1`. REF: exit 0, empty stdout and stderr, wire `AUTH LOGIN`, `dXNlcg==`, then an empty line (IMAP `A002 AUTHENTICATE LOGIN`, `dXNlcg==`, then an empty line). With SASL-IR offered, IMAP sends `A002 AUTHENTICATE LOGIN dXNlcg==` and then an empty line. After the fix Curl's request.bin, stdout.bin, stderr.txt and exitcode.txt are byte-identical to REF's for all four runs. Before the fix Curl sent `=`.
- `Cgr103Tests.Begin_LoginWithAnEmptyPassword_SendsTheEmptyPasswordAsAnEmptyLine` fails before the fix, because the default member returns false. It passes after.
- Build clean. Fast tests: 46 test projects passed, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary`: 0 failing members. `Curl.Protocol.Abstractions.UnitLibrary` changed only in an XML doc comment, so it was not measured. The change is platform-neutral: no paths, OS errors or certificates. The Linux REF (8.18) still sends `=`; Curl follows 8.22.0 on every platform (ADR-0826).

## Log

- 2026-10-26: Created from CGR-103 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. SASL LOGIN sends an empty password as an empty line, as curl 8.22.0 does over SMTP, POP3 and IMAP
