---
id: BL-3633
title: Fix CGR-104: SMTP --login-options is not checked: an unknown AUTH= mechanism or unknown option is accepted where curl fails with exit 3 before sending...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpProtocolHandler.cs, Curl.Protocol.Smtp.UnitTests/Cgr104Tests.cs, Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerAuthenticationTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3633 — Fix CGR-104: SMTP --login-options is not checked: an unknown AUTH= mechanism or unknown option is accepted where curl fails with exit 3 before sending...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-104 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-104: SMTP `--login-options` is not checked: an unknown `AUTH=` mechanism or unknown option is accepted where curl fails with exit 3 before sending anything

- **Severity:** Medium
- **Area:** other-protocols
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `other-protocols OP-5`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- `EV\confirm.cases` C04/C05 and `EV\smtpauth.cases` sa26/sa27; Linux the same. Commands:
  - `curl -s -S -u u:p --login-options AUTH=BOGUS smtp://127.0.0.1:21001 --mail-rcpt x`
  - `curl -s -S -u u:p --login-options FOO=BAR smtp://127.0.0.1:21001 --mail-rcpt x`

Commands (run each with REF and with PORT):

```sh
curl -s -S -u u:p --login-options AUTH=BOGUS smtp://127.0.0.1:21001 --mail-rcpt x
curl -s -S -u u:p --login-options FOO=BAR smtp://127.0.0.1:21001 --mail-rcpt x
```

**Expected (real curl)**

- connects, sends nothing and exits 3 with `curl: (3) URL using bad/illegal format or missing URL`. The POP3 and IMAP ports do match this.

**Actual (Curl)**

- `AUTH=BOGUS`: sends `EHLO ...`, then exits 67 with `curl: (67) Login denied` and no QUIT.
- `FOO=BAR`: ignores the option, authenticates with CRAM-MD5, runs VRFY and exits 0.

**Suspected location in source**

- `Curl.Protocol.Smtp.UnitLibrary\SmtpSaslAuthentication.cs:115-117`: `RequiredMechanism` only extracts `AUTH=` and never checks the option names or the mechanism.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr104Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Widened `touches` with `Curl.Protocol.Smtp.UnitLibrary/SmtpProtocolHandler.cs` (the check runs once connected, beside the EHLO-domain check, as curl's `smtp_connect` reads the options before the greeting) and `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerAuthenticationTests.cs` (two rows pinned `x=1` as accepted, which curl refuses; now `auth=*` and an empty option string). No live Doing task on origin/work/dark-factory names either file.
- Fix: `SmtpSaslAuthentication.AreLoginOptionsValid` accepts only `AUTH=` (any case) naming `*` or a mechanism curl knows (any case); otherwise the handler fails with exit 3 `URL using bad/illegal format or missing URL` before sending anything. Pipeline `feature` run directly: a two-line fix that matches measured curl, with nothing to plan and no design choice needing an ADR.
- Measured 2026-10-26 with `Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH PLAIN LOGIN CRAM-MD5'`, args `-sS -u u:p --login-options <opt> smtp://127.0.0.1:<port> --mail-rcpt x`, REF curl 8.22.0 (Git ucrt64, Schannel) and PORT `dotnet run --project Curl.Console --no-build --`:
  - `AUTH=BOGUS`: REF and PORT both exit 3, stderr `curl: (3) URL using bad/illegal format or missing URL`, stdout empty, request bytes empty (greeting read, nothing sent).
  - `FOO=BAR`: the same as above for both.
  - Variants that already matched: `AUTH=plain` both exit 0 with `EHLO`, `AUTH PLAIN`, `AHUAcA==`, `VRFY x`, `QUIT` and stdout `250 Recorder <recorder@localhost>`; `AUTH=*` both exit 0 with `AUTH CRAM-MD5`. REF also exits 3 for `AUTH=` (pinned in Cgr104Tests).
- `Cgr104Tests.ExecuteAsync_LoginOptionsCurlRefuses_ExitsThreeHavingSentNothing` fails before the fix (Curl sent `EHLO`, as the report recorded) and passes after it. Build clean, fast tests green on Windows; the change has no path, OS or certificate dependence, so it is platform-neutral. Measure-CodeQuality for Curl.Protocol.Smtp.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created from CGR-104 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. SMTP refuses unknown --login-options with exit 3 before sending a byte, as curl does
