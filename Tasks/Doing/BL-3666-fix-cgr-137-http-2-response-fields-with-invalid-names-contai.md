---
id: BL-3666
title: Fix CGR-137: HTTP/2 response fields with invalid names (containing a space or a colon, or empty) are printed and counted by PORT; REF drops them witho...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2ResponseHead.cs, Curl.Protocol.Http.UnitTests/Cgr137Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3666 — Fix CGR-137: HTTP/2 response fields with invalid names (containing a space or a colon, or empty) are printed and counted by PORT; REF drops them witho...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-137 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-137: HTTP/2 response fields with invalid names (containing a space or a colon, or empty) are printed and counted by PORT; REF drops them without a message

- **Severity:** Medium
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\r_spacename`)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-4`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scen `spacename` (`x bad: v`), `colonname` (`x:y`) or `emptyname` (empty name).
- Command: `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x -w '[%{size_header} %{num_headers}]'`
- Cases: `r_spacename`, `r2_colonname`, `r_emptyname`, `x_spacename_tls`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x -w '[%{size_header} %{num_headers}]'
```

**Expected (real curl)**

- exit 0. The field is not shown, and `size_header` is 60 with `num_headers` 2.

**Actual (Curl)**

- exit 0. It prints `x bad: v`, `x:y: v` or `: v` as a header line. `size_header` is 70, 68 or 65 respectively, and `num_headers` is 3.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2ResponseHead.cs:52`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr137Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-137 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
