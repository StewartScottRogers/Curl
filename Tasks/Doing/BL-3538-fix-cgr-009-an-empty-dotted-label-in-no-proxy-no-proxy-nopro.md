---
id: BL-3538
title: Fix CGR-009: An empty dotted label in no_proxy / NO_PROXY / --noproxy (e.g. .1, .0.0.1, foo,.x) crashes the port with exit 2 when the URL host is an I...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/NoProxyMatcher.cs, Curl.Core.UnitTests/Cgr009Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3538 — Fix CGR-009: An empty dotted label in no_proxy / NO_PROXY / --noproxy (e.g. .1, .0.0.1, foo,.x) crashes the port with exit 2 when the URL host is an I...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-009 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-009: An empty dotted label in no_proxy / NO_PROXY / --noproxy (e.g. `.1`, `.0.0.1`, `foo,.x`) crashes the port with exit 2 when the URL host is an IPv4 literal

- **Severity:** Critical (crash; one environment variable breaks every transfer to an IP host)
- **Area:** environment
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (reproduced by the verifier on Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `environment ENV-01`

**Reproduction**

- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- *(Windows):* 
  - `set http_proxy=http://127.0.0.1:21201` then `set no_proxy=.0.0.1` then `curl -s http://127.0.0.1:21200/echo`
  - No proxy is needed to trigger it: `curl -s --noproxy .1 http://127.0.0.1:21200/echo` and `curl -s --noproxy foo,.x http://127.0.0.1:21200/echo`
  - Verifier cases: `verify_windows.ps1` v-np-dot, v-np-opt and v-np-opt2.
- *(Linux):* 
  - `no_proxy=.1 ~/curlnet/curl -s http://127.0.0.1:21210/echo`
  - `http_proxy=http://127.0.0.1:21211 no_proxy=.0.0.1 ~/curlnet/curl -s http://127.0.0.1:21210/echo`
  - `linux/verify.sh` covers these as v-np-dot, v-np-dot2 and v-np-opt.

Commands (run each with REF and with PORT):

```sh
set http_proxy=http://127.0.0.1:21201
set no_proxy=.0.0.1
curl -s http://127.0.0.1:21200/echo
curl -s --noproxy .1 http://127.0.0.1:21200/echo
curl -s --noproxy foo,.x http://127.0.0.1:21200/echo
no_proxy=.1 ~/curlnet/curl -s http://127.0.0.1:21210/echo
http_proxy=http://127.0.0.1:21211 no_proxy=.0.0.1 ~/curlnet/curl -s http://127.0.0.1:21210/echo
```

**Expected (real curl)**

- The entry does not match. The request is sent through the proxy, or direct with `--noproxy`. Exit 0 and the body is written.

**Actual (Curl)**

- Prints `curl: (2) Index was outside the bounds of the array.` with a bare LF line ending even on Windows. Exits 2 and sends nothing.

**Notes**

- *Not affected:* `--noproxy .1` with a `localhost` URL (v-np-loc gives the same result as REF).

**Suspected location in source**

- `Curl.Core.UnitLibrary\NoProxyMatcher.cs:242-247`. In `TryParseOctet`, `digits.Length <= 3 && (digits.Length == 1 || digits[0] != '0')` reads `digits[0]` when the span is empty. The empty span is a label produced by `text.Split('.')` in `TryParseIPv4` (line 227).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr009Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-009 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
