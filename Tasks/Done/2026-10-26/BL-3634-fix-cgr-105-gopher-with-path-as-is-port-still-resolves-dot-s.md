---
id: BL-3634
title: Fix CGR-105: Gopher with --path-as-is: PORT still resolves dot segments in the selector
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Gopher.UnitLibrary/GopherSelector.cs, Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs, Curl.Protocol.Gopher.UnitLibrary/CLAUDE.md, Curl.Protocol.Gopher.UnitTests/Cgr105Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr105Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Fix: `GopherSelector.FromUrl` takes `pathAsIs` and keeps the written path's dot segments when it is set; `GopherProtocolHandler` passes `ITransferContext.PathAsIs`. No design choice beyond matching curl, so no ADR.
- Touches widened to `GopherProtocolHandler.cs` (the call that must pass the flag) and the library's `CLAUDE.md` (its selector description); no other task in Doing on origin/work/dark-factory names a Gopher file.
- Measured 2026-10-26 with `Record-CurlExchange.ps1` (server answers `hi\r\n`), `curl -s -S --path-as-is gopher://127.0.0.1:PORT<path>`, real curl 8.22.0 (Git ucrt64) and Curl's `curl.exe` after the fix, byte-identical in exit code, request, stderr and stdout:
  - `/0/a/../x`: exit 0, request `/a/../x\r\n`, stdout `hi\r\n`, stderr empty
  - `/0/../x`: exit 0, request `/../x\r\n`
  - `/0/./x`: exit 0, request `/./x\r\n`
  - `/1/../../a`: exit 0, request `/../../a\r\n`
  - `/../x`: exit 0, request `./x\r\n`
  - Before the fix Curl sent `/x`, empty, `/x`, empty, empty (the report's Actual). No files are written in any variant.
- `Cgr105Tests`: 6 of its 8 cases failed with the selector change reverted and all pass with it. Without `--path-as-is` dot segments are still removed (pinned in `FromUrl_NotPathAsIs_RemovesDotSegments`; the existing Gopher tests stay green).
- Linux/macOS: the tests hold ASCII-only URLs and no paths, so they are platform-neutral; the report's Linux REF gives the same selectors.
- Gates: `dotnet build` clean (0 warnings); fast tests 46 projects passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Gopher.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-105 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Gopher under --path-as-is sends the selector with its dot segments as written, byte-identical to curl 8.22.0 for all five CGR-105 repros
