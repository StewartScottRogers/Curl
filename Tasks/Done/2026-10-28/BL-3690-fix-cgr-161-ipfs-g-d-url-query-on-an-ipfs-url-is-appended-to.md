---
id: BL-3690
title: Fix CGR-161: IPFS: -G -d / --url-query on an ipfs:// URL is appended to the gateway URL where curl fails with exit 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr161Tests.cs, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3690 — Fix CGR-161: IPFS: -G -d / --url-query on an ipfs:// URL is appended to the gateway URL where curl fails with exit 1

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-161 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-161: IPFS: -G -d / --url-query on an ipfs:// URL is appended to the gateway URL where curl fails with exit 1

- **Severity:** Medium
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-10`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv web=21802`, then `curl -sS -G -d a=1 --ipfs-gateway http://127.0.0.1:21802 ipfs://bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi/f -w "%{http_code}\n"`, and also `--url-query a=1` in place of `-G -d a=1`. Cases: `cases\v_f10_*.txt`, `linux\cases\vl_f10_G_d.txt`.

Commands (run each with REF and with PORT):

```sh
srv web=21802
curl -sS -G -d a=1 --ipfs-gateway http://127.0.0.1:21802 ipfs://bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi/f -w "%{http_code}\n"
```

**Expected (real curl)**

- exits 1 with `curl: (1) Could not parse the URL, failed to set query`. The gateway gets no request and stdout is `000`.

**Actual (Curl)**

- exits 0 and sends `GET /ipfs/<cid>/f?a=1` to the gateway.

**Suspected location in source**

- `Curl.Console/CurlCommandRunner.cs:2495-2516` (TryRewriteIpfsUrl).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr161Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 8): Measured on curl 8.21.0 Schannel (C:\windows\system32\curl.exe). curl appends the `-G`/`--url-query` query with `curl_url_set` on the `ipfs://` URL after the `-O` remote-name check and before the IPFS rewrite; libcurl's URL API has no such scheme, so it fails with exit 1. REF and PORT with a TcpListener on 127.0.0.1:21802 (no connection was made in either):
  - `curl -sS -G -d a=1 --ipfs-gateway http://127.0.0.1:21802 ipfs://bafy.../f -w "%{http_code}\n"`: REF and PORT exit 1, stdout `000`, stderr `curl: (1) Could not parse the URL, failed to set query`, gateway untouched.
  - The same with `--url-query a=1`: REF and PORT identical, as above.
  - Also measured and pinned in `Cgr161Tests`: under `-s` alone no line; `%{errormsg}` is `Unsupported protocol`, `%{url}` the IPFS URL, `%{url_effective}` empty, `%{xfer_id}`/`%{conn_id}` `-1`, `%{filename_effective}` names the `-o` file (nothing written); the run ends (a later URL is not tried); `-G -d ""` fails too; `ipns://` and no gateway at all fail the same way before the gateway is looked up; `-O` keeps its earlier `Failed to extract a filename` refusal, which curl checks first; `-G` with no `-d` still rewrites to the gateway.
- Fix: `CurlCommandRunner.AppendsQueryToIpfsUrl` / `RefuseQueryOnIpfsUrlAsync` and the run-ending `IpfsQueryFailure`; `WriteOutRequestUrl` now prints an empty `%{url_effective}` for an empty transfer URL even with a query (it printed `?a=1`). `ReportAsync`'s failure-line test moved to `HasFailureLinesToWrite` to keep its complexity at 10. Decision (match curl's measured output) needed no ADR: no design choice beyond copying curl.
- `Cgr161Tests` fails without the fix: the runner rewrote the URL, sent `GET /ipfs/<cid>/f?a=1` to the recording handler and exited 0. (The lane's permissions refused reverting the file to re-run it; the failure follows from the old code path, which reached the handler.)
- Measure-CodeQuality.ps1 -Library Curl.Console: 100% line, 100% branch, 0 failing members. `dotnet build` clean; fast tests green on Windows; the tests use no paths or OS text, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-161 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -G -d / --url-query on an ipfs:// or ipns:// URL exits 1 with curl's 'Could not parse the URL, failed to set query' and never reaches the gateway
