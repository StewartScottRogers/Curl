---
id: BL-3634
title: Fix CGR-105: Gopher with --path-as-is: PORT still resolves dot segments in the selector
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Gopher.UnitLibrary/GopherSelector.cs, Curl.Protocol.Gopher.UnitTests/Cgr105Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3634 — Fix CGR-105: Gopher with --path-as-is: PORT still resolves dot segments in the selector

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-105 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-105: Gopher with `--path-as-is`: PORT still resolves dot segments in the selector

- **Severity:** Medium
- **Area:** other-protocols
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `other-protocols OP-6`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- `EV\dg2.cases` g19* (and C07). Commands:
  - `curl -s -S --path-as-is "gopher://127.0.0.1:21001/0/a/../x"`
  - the same with the paths `/0/../x`, `/0/./x`, `/1/../../a` and `/../x`

Commands (run each with REF and with PORT):

```sh
curl -s -S --path-as-is "gopher://127.0.0.1:21001/0/a/../x"
```

**Expected (real curl)**

- selectors `/a/../x`, `/../x`, `/./x`, `/../../a`, `./x`, each followed by CRLF.

**Actual (Curl)**

- selectors `/x`, `` (empty), `/x`, ``, ``, each followed by CRLF.

**Suspected location in source**

- `Curl.Protocol.Gopher.UnitLibrary\GopherSelector.cs:44` (`FromUrl` uses the URL path after dot-segment normalisation).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr105Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-105 of the third-party gap report.
