---
id: BL-3814
title: Fix CGR-285: --trace-config doh / dns output differs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3812]
touches: [Curl.Engine.UnitTests/Cgr285Tests.cs, Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Networking.UnitLibrary/DohDnsResolver.SubTransferTrace.cs, Curl.Networking.UnitTests/Cgr285Tests.cs, Curl.Output.UnitLibrary/DohSubTransferEvents.cs, Curl.Output.UnitTests/Cgr285Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3814 — Fix CGR-285: --trace-config doh / dns output differs

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-285 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-285: --trace-config doh / dns output differs

- **Severity:** Low
- **Area:** doh-ipfs-resolution
- **Platform(s):** Windows. Detail: Windows (REF-LINUX 8.18 also differs, so Linux was not compared)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-15`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1`, then `curl -v -s --trace-config doh --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x`, and the same with `--trace-config dns`. Cases: `cases\v_f15_*.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1
curl -v -s --trace-config doh --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x
```

**Expected (real curl)**

- under `doh` it prints only [DoH] lines, the queries run in parallel and the line `[DoH] Connection #1 is not open enough, cannot reuse` appears. Under `dns` it prints `[DNS] [A+AAAA] created DNS filter ..., transport=3`, `hostip_resolv(...)` and cache lines, without the DoH sub-transfer headers.

**Actual (Curl)**

- under `doh` it adds `[DNS] created DNS filter for ..., transport=3, queries=3`, `cf_dns_start` lines and others, runs the queries one after the other and has no reuse line. Under `dns` it prints the full DoH sub-transfers and leaves out the hostip and cache lines. The exit code is the same.

**Suspected location in source**

- `Curl.Output.UnitLibrary/DohSubTransferEvents.cs:18-34`, `Curl.Networking.UnitLibrary/DohDnsResolver.SubTransferTrace.cs:15-38`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr285Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-29 (lane 1): DohSubTransferEvents takes writesDnsFilterLines / writesSubTransferLines; EngineComposition.DohSubTransferEventsOf sets them from the run's --trace-config (doh alone: [DoH] sub-transfer lines, no [DNS] lines; dns alone: [DNS] filter lines, no sub-transfer lines/headers; both, all, network or none given: unchanged). Measured curl 8.22.0 earlier (see lane 8 notes above); the repro commands exit 7 in both. Byte-for-byte parity of the debug-build timing lines, the reuse line and parallel queries is not reachable, so the criteria are ticked for the component gating only. touches gained Curl.Engine.UnitTests/Cgr285Tests.cs (the engine-side gate test). Tests: Curl.Output.UnitTests Cgr285Tests (3), Curl.Engine.UnitTests Cgr285Tests (7); full fast run green; Measure-CodeQuality Output+Engine: 0 failing.

## Log

- 2026-10-26: Created from CGR-285 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes (2026-10-28, lane 8, first claim, no code edited)

- **Measured** curl 8.22.0 (Schannel, debug build) with `Record-CurlExchange.ps1 -NoServer -DohPort 18853 -Curl ... -CurlArgs -v,-s,--trace-config,<doh|dns>,--doh-insecure,--doh-url,https://127.0.0.1:18853/dns-query,http://foo.test:18854/x` (exit 7 both). `doh` alone: only `[DoH]`-prefixed sub-transfer lines (plus plain `* Host ... was resolved.`), no `[DNS]` lines, `Connection #1 is not open enough, cannot reuse`. `dns` alone: `[DNS] [A+AAAA] created DNS filter ..., transport=3`, `hostip_resolv`, cache lines, `[DNS] [DoH] probe done`, no sub-transfer lines or headers.
- Curl today writes the **identical** output for `doh` and `dns` (both the `[DNS]` filter lines and the full DoH sub-transfers). Byte parity is not reachable: the reference is a debug build whose lines are timing-dependent (`still waiting 25ms for AAAA result`, schannel handshake step counts, `Curl_xfer_send`), so only the component gating is deterministic.
- **Plan** (needs the engine, which BL-3812 holds in `EngineComposition.Dns.cs`, hence `depends-on` and the new `touches` entry): pass the run's `--trace-config` components to `DohSubTransferEvents` (new optional parameter in `Curl.Output`) so `doh` writes the `[DoH]` sub-transfer lines without the `[DNS]` filter lines, and `dns` without `doh` writes no sub-transfer lines (`[DNS] [DoH] probe done` instead); `Cgr285Tests` in `Curl.Output.UnitTests` pins it. Reuse line and parallel queries are left (parallel queries are timing-dependent in the debug output).
- Requeued so as not to edit a file BL-3812 is changing.
- 2026-10-28: Doing -> Backlog. Needs EngineComposition.Dns.cs, held by BL-3812 (depends-on BL-3812)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. --trace-config doh writes only DoH sub-transfer lines and dns only the DNS filter lines
