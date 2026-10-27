---
id: BL-3748
title: Fix CGR-219: %{response_code} not set for an SMTP reply; 220 instead of 000 after an unparsable FTP reply
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests/Cgr219Tests.cs, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests/Cgr219Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3748 — Fix CGR-219: %{response_code} not set for an SMTP reply; 220 instead of 000 after an unparsable FTP reply

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-219 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-219: %{response_code} not set for an SMTP reply; 220 instead of 000 after an unparsable FTP reply

- **Severity:** Low
- **Area:** writeout-verbose
- **Platform(s):** Windows. Detail: Windows (tested there only)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `writeout-verbose WV-15`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- Mini FTP server on 20701.
  - `curl -s -w '%{response_code}\n' smtp://127.0.0.1:20701/ -m 3`. The server answers EHLO with 500.
  - `curl -s -w '%{response_code}\n' -u weird:x ftp://127.0.0.1:20701/file.txt`. The server answers USER with `hello?`.

Commands (run each with REF and with PORT):

```sh
curl -s -w '%{response_code}\n' smtp://127.0.0.1:20701/ -m 3
curl -s -w '%{response_code}\n' -u weird:x ftp://127.0.0.1:20701/file.txt
```

**Expected (real curl)**

- SMTP: exit 9, response_code `500`.
- FTP: exit 56, `000`.

**Actual (Curl)**

- SMTP: exit 9, `000`.
- FTP: exit 56, `220` (the greeting's code is kept).

**Suspected location in source**

- Curl.Protocol.Smtp.UnitLibrary and Curl.Protocol.Ftp.UnitLibrary (how the response code is reported).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr219Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-219 of the third-party gap report.
