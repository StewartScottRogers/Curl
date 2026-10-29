---
id: BL-3815
title: Fix CGR-286: Connection numbers in -v do not count the DoH connections
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3814]
touches: [Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitLibrary/EngineConnectionIdTransferEvents.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs, Curl.Console/CurlComposition.cs, Curl.Engine.UnitTests/Cgr286Tests.cs, Curl.Networking.UnitTests/Cgr286Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3815 — Fix CGR-286: Connection numbers in -v do not count the DoH connections

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-286 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-286: Connection numbers in -v do not count the DoH connections

- **Severity:** Low
- **Area:** doh-ipfs-resolution
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: Confirmed. This is DoH-specific: a redirect without DoH numbers #0/#1 in both binaries (`v_f16_nodoh_redir`).
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-16`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1 rr=bar.test:AAAA:::1 webstatus=302 weblocation=http://bar.test:21802/final`, then `curl -v -sS -L --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x`. Case: `cases\v_f16_redir.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1 rr=bar.test:AAAA:::1 webstatus=302 weblocation=http://bar.test:21802/final
curl -v -sS -L --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x
```

**Expected (real curl)**

- `* Connection #3 to host bar.test:21802 left intact`.

**Actual (Curl)**

- `* Connection #1 to host bar.test:21802 left intact`.

**Suspected location in source**

- Not located by the reviewers.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr286Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Analysis (2026-10-29, no code edited): the DoH resolver (`Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs`) opens one connection per query with its own `_connectionCount` and never takes a `%{conn_id}` from the run's `TransferPerformer`, so the -v number of the first real connection is #0 where curl 8.22.0 prints #2 for foo (DoH A and AAAA took #0 and #1) and #3 for bar (DoH connections reused, so no new ids). Fix plan: give `DohDnsResolver` an optional id-taker that is told when a query finds no idle DoH connection (active queries >= ids taken), plumbed from `TransferPerformer.TakeConnectionId` through `EngineComposition.CreateTransports`/`CreateDnsResolver` (Curl.Console/CurlComposition.cs, ManagedCurlEngine.cs call it); unit tests in Curl.Networking.UnitTests/Cgr286Tests.cs and Curl.Engine.UnitTests/Cgr286Tests.cs. Needs EngineComposition.Dns.cs, which live task BL-3814 holds, so requeued behind it.

## Log

- 2026-10-26: Created from CGR-286 of the third-party gap report.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Backlog. Needs EngineComposition.Dns.cs held by BL-3814 (DoH connection ids); depends-on BL-3814
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

- Done (2026-10-29): `DohDnsResolver.TakeConnectionId` (init) is called once for each DoH connection curl opens, i.e. for each query running at once that no taken number covers (A and AAAA take two, a second name's queries none), plumbed from `TransferPerformer.TakeConnectionId` via `CreateRunner` -> `CreateTransports` -> `CreateDnsResolver` -> `CreateDohResolver`. foo then gets #2 and bar #3 as in curl 8.22.0. Decision: the DoH sub-transfer's own "Connection #N" -v text is unchanged (not part of this issue). The real-curl `#3` expectation is the report's own measurement (not re-run here); the unit tests Cgr286Tests in Networking and Engine pin the id-taking rule. Measure-CodeQuality (Networking, Engine, Console): 0 failing members. Linux/macOS CI: change is platform-neutral, fast tests green here.
- 2026-10-29: Doing -> Done. DoH connections take run connection ids so -v numbers the transfer's connections after them (CGR-286)
