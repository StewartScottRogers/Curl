---
id: BL-3532
title: Fix CGR-003: POP3/IMAP/SMTP ignore --connect-timeout while waiting for the server greeting (hang)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary, Curl.Original.Protocol.Imap.UnitTests/Cgr003Tests.cs, Curl.Original.Protocol.Pop3.UnitLibrary, Curl.Original.Protocol.Pop3.UnitTests/Cgr003Tests.cs, Curl.Original.Protocol.Smtp.UnitLibrary, Curl.Original.Protocol.Smtp.UnitTests/Cgr003Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3532 — Fix CGR-003: POP3/IMAP/SMTP ignore --connect-timeout while waiting for the server greeting (hang)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-003 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-003: POP3/IMAP/SMTP ignore --connect-timeout while waiting for the server greeting (hang)

- **Severity:** Critical (hang)
- **Area:** writeout-verbose
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (reproduced by the original tester and the verifier, on Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `writeout-verbose WV-01`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- A listener that accepts the connection and never sends anything. The HTTP port of wvsrv.cs waits for a request, so it qualifies.
  - `curl -sS -w '%{exitcode}|%{errormsg}\n' pop3://127.0.0.1:20700/ --connect-timeout 2`
  - Same with `imap://` and `smtp://`.
  - Linux: port 20710.

Commands (run each with REF and with PORT):

```sh
curl -sS -w '%{exitcode}|%{errormsg}\n' pop3://127.0.0.1:20700/ --connect-timeout 2
```

**Expected (real curl)**

- After about 2 s, exit 28 with `curl: (28) Operation timed out after 2012 milliseconds with 0 bytes received`. REF-LINUX 8.18 behaves the same.

**Actual (Curl)**

- Never exits. It was killed by the 20 s timeout (exit 124) with no output. This happens on Windows and Linux. `ftp://` in the same setup matches REF.

**Suspected location in source**

- The greeting read in Curl.Original.Protocol.Pop3/Imap/Smtp.UnitLibrary is not bounded by the connect deadline.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr003Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-003 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-3532-20261026-063535-L1.jsonl
- 2026-10-26: Backlog -> Deferred. Split over the cost cap into BL-3881 (POP3), BL-3882 (IMAP), BL-3883 (SMTP)
