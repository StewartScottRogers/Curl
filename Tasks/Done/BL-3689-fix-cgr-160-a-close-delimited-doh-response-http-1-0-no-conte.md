---
id: BL-3689
title: Fix CGR-160: A close-delimited DoH response (HTTP/1.0, no Content-Length) is treated as a receive failure
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohResponseReader.cs, Curl.Networking.UnitTests/Cgr160Tests.cs, Curl.Networking.UnitTests/DohResponseReaderTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.ExchangeFailures.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3689 — Fix CGR-160: A close-delimited DoH response (HTTP/1.0, no Content-Length) is treated as a receive failure

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-160 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-160: A close-delimited DoH response (HTTP/1.0, no Content-Length) is treated as a receive failure

- **Severity:** Medium
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** AF-0059
- **Source findings:** `doh-ipfs-resolution DOH-9`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1 mangle=http10`, then `curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x`. TLS variant: `dohs=21801 selfsigned=1 ... mangle=http10` with `--doh-insecure --doh-url https://127.0.0.1:21801/dns-query`. Cases: `cases\v_f9_*.txt`, `linux\cases\vl_f9_http10.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1 mangle=http10
curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x
```

**Expected (real curl)**

- reads the body until the connection closes, decodes it and exits 0.

**Actual (Curl)**

- exits 6 with `curl: (6) Could not resolve host: foo.test`. It drops the connection before the server has finished writing.

**Suspected location in source**

- `Curl.Networking.UnitLibrary/DohResponseReader.cs:14-16`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr160Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Added to touches: `DohResponseReaderTests.cs` and `DohDnsResolverTraceTests.ExchangeFailures.cs` pinned a close-delimited body as a receive failure (the old behaviour), and `Curl.Networking.UnitLibrary/CLAUDE.md` described it; no task in Doing on origin/work/dark-factory names any of them.
- Measured (curl 8.22.0 Schannel, `C:\Program Files\Git\ucrt64\bin\curl.exe`, against a C# loopback DoH server on 21800/21801 and web server on 21802 answering `hello`): `curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x` with an `HTTP/1.0 200 OK` close-delimited answer: REF exit 0, stdout `hello`, stderr empty; PORT before the fix exit 6 `curl: (6) Could not resolve host: foo.test` (the reader returned null for this framing, as the removed `close-delimited` DataRow pinned), after the fix exit 0, stdout `hello`, stderr empty. Same for an `HTTP/1.1 200 OK` close-delimited answer. Variant already matching: with `Content-Length`, REF and PORT exit 0 `hello`.
- TLS variant (`--doh-insecure --doh-url https://127.0.0.1:21801/dns-query`, HTTP/1.0, no Content-Length): server sends close_notify then closes: REF and PORT exit 0 `hello`; server closes without close_notify: REF and PORT both exit 6 `Could not resolve host: foo.test`. That truncated-TLS close is what ADR-0152's BL-641 row measured, so ADR-0850 supersedes that row.
- The fix only changes how the response is read; the request bytes on the wire (DoH POSTs and the GET) are unchanged and no files are written.
- Focused test: `Cgr160Tests` (HTTP/1.0 and HTTP/1.1 close-delimited bodies returned, 3000 bytes returned, 3001 bytes null). Reverting the production file to run it pre-fix was refused by the sandbox; the old reader returned null for exactly this input (its own `close-delimited` DataRow asserted null), so the two close-delimited cases fail before the fix.
- Fast tests green (Curl.Networking.UnitTests 3649 passed); Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members. The change is platform-neutral (byte parsing over a scripted connection), so CI on Linux and macOS is covered by the fast tests.

## Log

- 2026-10-26: Created from CGR-160 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A close-delimited DoH response (HTTP/1.0 or 1.1, no Content-Length) is read to the close and resolves, as curl 8.22.0 does
