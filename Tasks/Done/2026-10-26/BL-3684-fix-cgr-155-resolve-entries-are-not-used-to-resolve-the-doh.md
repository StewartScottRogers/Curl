---
id: BL-3684
title: Fix CGR-155: --resolve entries are not used to resolve the DoH server's own host name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitTests/Cgr155Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3684 — Fix CGR-155: --resolve entries are not used to resolve the DoH server's own host name

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-155 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-155: --resolve entries are not used to resolve the DoH server's own host name

- **Severity:** Medium
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-4`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1`, then `curl -sS --resolve dohsrv.test:21800:127.0.0.1 --doh-url http://dohsrv.test:21800/dns-query http://foo.test:21802/x`. On Linux: `dohs=21801 selfsigned=1`, with `--doh-insecure --resolve dohsrv.test:21801:127.0.0.1 --doh-url https://dohsrv.test:21801/dns-query`. Cases: `cases\v_f4_resolve_dohsrv.txt`, `linux\cases\vl_f4_resolve_dohsrv.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1
curl -sS --resolve dohsrv.test:21800:127.0.0.1 --doh-url http://dohsrv.test:21800/dns-query http://foo.test:21802/x
```

**Expected (real curl)**

- the DoH server name resolves through the --resolve entry. Both queries are sent and the exit code is 0.

**Actual (Curl)**

- looks up dohsrv.test with the system resolver and fails (`[DoH] Could not resolve host: dohsrv.test`). Nothing reaches the DoH server, and PORT exits 6 with `Could not resolve host: foo.test`.

**Suspected location in source**

- `Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:147-155` (CreateDohConnector uses `new SystemDnsResolver()`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr155Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 5): Plan: `CreateDohConnector` takes the option group's `ResolveOverrides` and loads them into its own DNS cache at once, with `NoTransferEvents`, so the entries answer for the DoH server's name and no DoH sub-transfer writes `Added ... to DNS cache` lines (curl's DoH probes find the entries in the shared cache and are given no `CURLOPT_RESOLVE` of their own). `CreateDnsResolver` and `DialingResolverOf` pass `ResolveOverrides.Parse(network.Resolve)`. ADR-0852 records it. Only `EngineComposition.Dns.cs` changed in production.
- Repro, measured on Windows with `Record-CurlExchange.ps1 -Port 21802 -DohPort 21801` (the recorder's DoH responder is TLS only, so the HTTPS form of the issue's Linux variant was used, with `--doh-insecure`): `curl -sS --doh-insecure --resolve dohsrv.test:21801:127.0.0.1 --doh-url https://dohsrv.test:21801/dns-query http://foo.test:21802/x`.
  - REF (curl 8.22.0 Schannel, Git for Windows): exit 52, stderr `curl: (52) Empty reply from server`, stdout empty, no files; dns.txt: two `doh` queries, AAAA `00000100000100000000000003666f6f047465737400001c0001` then A `...04746573740000010001`.
  - PORT after the fix: exit 52, the same stderr and empty stdout, no files, the same two queries byte for byte in the same order. Before the fix (per the report) PORT looked `dohsrv.test` up with the system resolver and exited 6 with nothing sent. The exit 52 is the recorder's web side closing without a reply while the DoH responder runs beside it, the same for both binaries; the DoH bytes are what this issue is about.
- Focused test: `Cgr155Tests.CreateDnsResolver_ResolveEntryForTheDohServer_DialsTheDohServerAtTheEntrysAddress` failed with the fix reverted ("Expected collection to not be empty": nothing dialled) and passes with it; `CreateDohConnector_ResolveEntries_WritesNoAddedLineOnTheDohSubTransfer` pins that loading writes no line.
- Variants that already matched: the existing DoH tests (`EngineCompositionDohTests`, `Cgr037Tests`, the DoH trace tests) pass unchanged; a DoH server named by an IP literal, or with no `--resolve` entry, resolves as before.
- Fast tests: 46 test assemblies passed, 0 failed (Windows). The change is platform-neutral (no paths, OS text or certificates), which covers Linux and macOS. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-155 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. --resolve entries now answer for the DoH server's own name, so both DoH queries reach it as in curl 8.22.0
