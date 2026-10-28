---
id: BL-3567
title: Fix CGR-038: A --doh-url with no scheme is sent as plain http:// where curl assumes https://
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3813]
touches: [Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs, Curl.Engine.UnitTests/Cgr038Tests.cs, Curl.Engine.UnitTests/Cgr163Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3567 — Fix CGR-038: A --doh-url with no scheme is sent as plain http:// where curl assumes https://

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-038 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-038: A --doh-url with no scheme is sent as plain http:// where curl assumes https://

- **Severity:** High
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-2`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv dohs=21801 selfsigned=1 web=21802 rr=foo.test:A:127.0.0.1`, then `curl -sS --doh-insecure --doh-url 127.0.0.1:21801/dns-query http://foo.test:21802/x`. Reverse check: `srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1`, then `curl -sS -m 5 --doh-url 127.0.0.1:21800/dns-query http://foo.test:21802/x`. Cases: `cases\v_f2_*.txt`, `linux\cases\vl_f2_noscheme.txt`.

Commands (run each with REF and with PORT):

```sh
srv dohs=21801 selfsigned=1 web=21802 rr=foo.test:A:127.0.0.1
curl -sS --doh-insecure --doh-url 127.0.0.1:21801/dns-query http://foo.test:21802/x
srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1
curl -sS -m 5 --doh-url 127.0.0.1:21800/dns-query http://foo.test:21802/x
```

**Expected (real curl)**

- treats the value as https. Against the TLS server it exits 0. Against the plain server it tries TLS and exits 28 with `Resolving timed out after 5011 milliseconds`.

**Actual (Curl)**

- adds `http://`. Against the TLS server it exits 6 with `curl: (6) Could not resolve host: foo.test`. Against the plain server it exits 0, and the DNS queries go out unencrypted.

**Suspected location in source**

- `Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:124-131` (DohUrlOf prefixes "http://").

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr038Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 3): Added `Curl.Engine.UnitTests/EngineCompositionDohTests.cs` to `touches`: its `DohUrlOf` rows and the `--doh-url bogus` connect test pinned the old `http://` guess (no other Doing task names it).
- 2026-10-26 (lane 3): Measured curl 8.22.0 Schannel: a TCP listener given as `--doh-url 127.0.0.1:21840/dns-query` received a TLS ClientHello (`16 03 01 01 b1 ...`), curl exit 28 `curl: (28) Resolving timed out after 3031 milliseconds` (`-m 3`). BL-642's old "plain HTTP" note was wrong.
- 2026-10-26 (lane 3): Fix (left uncommitted, stashed by the shift): `EngineComposition.DohUrlOf` prefixes `https://` instead of `http://`. ADR-0906 is new (file plus README row). `Cgr038Tests.cs` has `DohUrlOf_NoScheme_AssumesHttps` and `CreateDohResolver_NoScheme_ConnectsToTheDohServerOverTls`. `EngineCompositionDohTests` is updated (rows to `https://`, bogus target `bogus:443` TLS). Full build clean; fast tests green (46 projects passed, 8 integration-only projects had no match). Measure-CodeQuality has not been run yet: run it for Curl.Engine.UnitLibrary before committing. The change adds no branch.
- 2026-10-26 (lane 3): Repro with a C# DoH server (TLS on 21801, self-signed, answers foo.test A 127.0.0.1; web on 21802 replies `hello`). `curl -sS --doh-insecure --doh-url 127.0.0.1:21801/dns-query http://foo.test:21802/x`: REF exit 0, stdout `hello\n`, stderr empty. PORT (with the fix) gives the same. Plain-server variant `-m 3 --doh-url 127.0.0.1:21841/dns-query`: PORT now sends a TLS ClientHello (`16 03 01 01 a8 ...`) and exits 28, but stderr says `curl: (28) Connection timed out after 2987 milliseconds` where REF says `Resolving timed out after N milliseconds`. That message is CGR-284, which BL-3813 covers (TcpConnector/MaxTimeWatchdog). So BL-3813 goes in `depends-on`. Once it is Done, re-apply the stash, run Measure-CodeQuality for Curl.Engine.UnitLibrary, rerun the plain-server repro to check the message, then tick the criteria and finish.

- 2026-10-28 (lane 9): Added `Curl.Engine.UnitTests/Cgr163Tests.cs` to `touches`: its rows pinned a scheme-less DoH URL as not https (unusable, never dialled), the old `http://` guess; no Doing task names it. Its scheme-less row of the never-dials test is gone and `IsHttpsDohUrl` rows for `127.0.0.1/dns-query` and `bogus` are now true. The stash's ADR-0819 is renumbered ADR-0956 (next free), amending ADR-0859; the stash's three stale ADR-0818 README rows (already renumbered on the branch) are dropped.
- 2026-10-28 (lane 9): Re-measured with BL-3813 Done, a TCP listener on 21841 that accepts and never answers: `curl -sS -m 3 --doh-url 127.0.0.1:21841/dns-query http://foo.test:21802/x`. REF (curl 8.22.0): first bytes `16 03 01`, exit 28, stderr `curl: (28) Resolving timed out after 3010 milliseconds`, stdout empty. PORT: first bytes `16 03 01`, exit 28, stderr `curl: (28) Resolving timed out after 2932 milliseconds`, stdout empty. The TLS-server repro matched in lane 3's run (exit 0, `hello`). Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members. Full build clean, fast tests green (46 projects). The change is platform-neutral (no path, OS error text or certificate), so Linux and macOS CI is expected to match.

## Log

- 2026-10-26: Created from CGR-038 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits on BL-3813 (CGR-284): with the https fix applied, the plain-server repro still says 'Connection timed out' where curl says 'Resolving timed out'
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. A scheme-less --doh-url is https as curl 8.22.0 sends it; both repros match
