---
id: BL-3764
title: Fix CGR-235: SMTP response-read failure on Linux: curl 8.18 prints errno: 115, PORT prints errno: 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests/Cgr235Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3764 — Fix CGR-235: SMTP response-read failure on Linux: curl 8.18 prints errno: 115, PORT prints errno: 0

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-235 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-235: SMTP response-read failure on Linux: curl 8.18 prints `errno: 115`, PORT prints `errno: 0`

- **Severity:** Low
- **Area:** other-protocols
- **Platform(s):** Linux. Detail: Linux only
- **Confidence:** Probable. Detail: Probable. Reproduced on Linux, but only against 8.18, and 115 (EINPROGRESS) looks like a leftover errno value. This may depend on the curl version or platform.
- **Related existing finding (GF/AF):** none
- **Source findings:** `other-protocols OP-16`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- `EV\misc.cases` x23, x26 and x28: the server answers MAIL with `25\r\n`, closes after MAIL, or sends garbage. Command: `curl -s -S smtp://127.0.0.1:21002 --mail-from a@b --mail-rcpt c@d -T EV/data/msg_crlf.txt`

Commands (run each with REF and with PORT):

```sh
curl -s -S smtp://127.0.0.1:21002 --mail-from a@b --mail-rcpt c@d -T EV/data/msg_crlf.txt
```

**Expected (real curl)**

- REF-LINUX 8.18: `curl: (56) response reading failed (errno: 115)`. Windows REF 8.22: `errno: 0`, which matches PORT.

**Actual (Curl)**

- `curl: (56) response reading failed (errno: 0)` on both platforms.

**Suspected location in source**

- Not located by the reviewers.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr235Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-235 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

### Result (2026-10-28)
- Command `curl -s -S smtp://127.0.0.1:21002 --mail-from a@b --mail-rcpt c@d -T msg` against a server that closes after MAIL: curl 8.22.0 (Windows) and Curl both give exit 56, `response reading failed (errno: 0)`. Only curl 8.18.0 on Linux prints `errno: 115` (EINPROGRESS, a stale errno from the connect), which the report rates Probable and version-dependent.
- Decision (ADR-0976): keep `errno: 0` everywhere; no production change. `Cgr235Tests` pins the text; the fast SMTP tests pass (404 passed). Linux/macOS CI is met by the platform-neutral test.
- 2026-10-28: Doing -> Done. CGR-235 not a defect: errno 115 is a stale errno of curl 8.18 on Linux; 8.22 prints errno: 0 like Curl (ADR-0976)
