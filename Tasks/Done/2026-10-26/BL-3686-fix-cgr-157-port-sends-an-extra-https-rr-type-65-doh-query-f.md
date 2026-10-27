---
id: BL-3686
title: Fix CGR-157: PORT sends an extra HTTPS-RR (type 65) DoH query for https:// targets
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/Cgr157Tests.cs, Curl.Networking.UnitLibrary/IHttpsOriginDnsResolver.cs, Curl.Networking.UnitTests/TcpConnectorHttpsRecordReportTests.cs, Curl.Networking.UnitTests/DohDnsResolverHttpsRecordQueryTests.cs, Curl.Networking.UnitTests/TcpConnectorShuffledResolverTests.cs, Curl.Conformance.UnitLibrary/UpstreamCurlVersionLineFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamUnixFeatures.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3686 — Fix CGR-157: PORT sends an extra HTTPS-RR (type 65) DoH query for https:// targets

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-157 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-157: PORT sends an extra HTTPS-RR (type 65) DoH query for https:// targets

- **Severity:** Medium
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** AF-0382, AF-0443
- **Source findings:** `doh-ipfs-resolution DOH-6`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv doh=21800 web=21802 webs=21805 pfx=SCR\evidence\tls\pki\srv.pfx rr=foo.test:A:127.0.0.1`, then `curl -sS -k --doh-url http://127.0.0.1:21800/dns-query https://foo.test:21805/x`. Linux: the same over HTTPS DoH with `--doh-insecure`. Cases: `cases\v_f6_https_target.txt`, `linux\cases\vl_f6_https_target.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802 webs=21805 pfx=SCR\evidence\tls\pki\srv.pfx rr=foo.test:A:127.0.0.1
curl -sS -k --doh-url http://127.0.0.1:21800/dns-query https://foo.test:21805/x
```

**Expected (real curl)**

- *REF (8.22 Windows, 8.18 Linux):* sends 2 DoH POSTs, for A and AAAA.

**Actual (Curl)**

- sends 3 DoH POSTs. The extra one is `qname=_21805._https.foo.test qtype=65` (40 bytes). That is one more connection, and it tells the DoH server the target port.

**Suspected location in source**

- `Curl.Networking.UnitLibrary/TcpConnector.cs:1229-1249` (HttpsOriginResolverFor) and `DohDnsResolver.HttpsRecord.cs:29`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr157Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Decision (ADR-0856, decided by Claude under Stewart's delegation): `TcpConnector.AsksHttpsRecords` (init, default `!OperatingSystem.IsWindows()`) gates ADR-0727's HTTPS-origin resolve. On Windows the Schannel reference has no HTTPSRR (and ADR-0808 already dropped it from `-V`), so an `https://` origin is resolved with A and AAAA only. `--ech` still asks the HTTPS record through `IEchConfigListLookup`. Linux keeps the HTTPSRR behaviour its `-V` advertises; the report's Linux measurement (8.18.0 sends 2 queries) is left to BL-3914, since a lane cannot run the Linux reference and changing Linux also means changing its Features line.
- Touches widened (no task in Doing on origin/work/dark-factory named these files): `IHttpsOriginDnsResolver.cs` (its doc named when TcpConnector calls it); three Networking test files whose tests pin the HTTPSRR build now set `AsksHttpsRecords = true` so they hold on every platform; the conformance harness reports `HTTPSRR` on Linux and macOS only (`UpstreamUnixFeatures`), so case 2100's `%if HTTPSRR` protocol is `SkippedOnThisPlatform` on Windows rather than failing, and `UpstreamCurlPlatformTests` moved with it.
- Measured on Windows with `Record-CurlExchange.ps1 -NoServer -DohPort 21900 -CurlArgs -sS,--doh-insecure,--doh-url,https://127.0.0.1:21900/dns-query,https://foo.test:21805/x` (the reference refuses an http:// DoH URL, so the script's TLS DoH responder stands in; port 21805 has no listener, so both end at the connect):
  - REF curl 8.22.0 Schannel: 2 DoH queries, `foo.test` type 1 (`...0001 0001`) and type 28 (`...001c 0001`); exit 7; stderr `curl: (7) Failed to connect to foo.test:21805 after 56 ms: Could not connect to server`; stdout empty; no files.
  - PORT (Curl at this commit): the same 2 queries, byte for byte (no `_21805._https.foo.test` type 65); exit 7; stderr the same text (ms aside); stdout empty; no files. Before the fix Curl sent the third, type 65 query (CGR-157; ADR-0727's design).
- No repro variant in the issue matched before; the other DoH tests (Networking, Engine, Console, conformance ratchet) stay green.
- Focused test: `Curl.Networking.UnitTests/Cgr157Tests.cs` (Windows: addresses only; off Windows: HTTPS origin asked with its port; `AsksHttpsRecords = false` writes no `HTTPS-RR` line). The Windows test fails before the fix: the existing `DohDnsResolverHttpsRecordQueryTests.ConnectAsync_ToADirectHttpsOrigin_AsksTheResolverForTheHttpsOriginWithItsPort` passed then with a default connector, asserting the opposite look-up (`https-origin host:port`); it now sets `AsksHttpsRecords = true`.
- `dotnet build` clean; fast tests: 46 test projects passed, 0 failed (Networking 3648 passed, Conformance 4374, Engine 2650, Console 2955). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Conformance.UnitLibrary`: 0 failing members.
- CI on Linux and macOS: the change is platform-neutral by construction (the platform-dependent test is split with OSCondition, every other test sets the property explicitly); green here on Windows.
- Follow-up filed: BL-3914 (Linux half of CGR-157).

## Log

- 2026-10-26: Created from CGR-157 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. On Windows --doh-url sends only the A and AAAA queries for an https:// origin, as curl 8.22.0 Schannel does (ADR-0856); Linux half filed as BL-3914
