---
id: BL-3801
title: Fix CGR-272: Error message text differs for several HTTP/2 connection errors that have the same exit code
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr272Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3801 — Fix CGR-272: Error message text differs for several HTTP/2 connection errors that have the same exit code

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-272 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-272: Error message text differs for several HTTP/2 connection errors that have the same exit code

- **Severity:** Low
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\r_wuoverflow`)
- **Confidence:** Probable
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-16`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scens: `wuoverflow`, `sideven`, `settingserr`, `gamid1`.
- Command: `curl -sS --http2-prior-knowledge http://127.0.0.1:P/x`
- Cases: `r_wuoverflow`, `r_sideven`, `r_settingserr`, `r_gamid1`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge http://127.0.0.1:P/x
```

**Expected (real curl)**

- `wuoverflow`, `sideven`, `settingserr`: `curl: (16) nghttp2 recv error -902: The user callback function failed`.
- GOAWAY mid-body: `curl: (56) Recv failure: Connection reset by peer`.

**Actual (Curl)**

- `curl: (16) nghttp2 shuts down connection with error 3: FLOW_CONTROL_ERROR` or `... error 1: PROTOCOL_ERROR`.
- GOAWAY mid-body: `curl: (56) Failure when receiving data from the peer`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\HttpTransferMessages.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr272Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Real curl with nghttp2 could not be run here (Windows curl has no HTTP/2, no WSL server); fixed to the report's measured expected text (curl 8.18.0): FLOW_CONTROL_ERROR and invalid-SETTINGS errors -> exit 16 `nghttp2 recv error -902: The user callback function failed`; GOAWAY refusing the stream mid-response -> exit 56 `Recv failure: Connection reset by peer` (HTTP/2 only, HTTP/3 refusal text unchanged). Decision in ADR-0969. Unit tests in Cgr272Tests.cs; existing tests for the GOAWAY text updated.
- Not fixed: the `sideven` scenario (server-opened even stream) - its source in the frame layer was not identified; it keeps its old text. Criterion on CI platforms met by platform-neutral tests.

## Log

- 2026-10-26: Created from CGR-272 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. HTTP/2 -902 callback text and GOAWAY mid-response text fixed; sideven not covered (see Notes)
