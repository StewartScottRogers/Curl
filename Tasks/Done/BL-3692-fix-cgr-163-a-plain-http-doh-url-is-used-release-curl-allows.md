---
id: BL-3692
title: Fix CGR-163: A plain http:// --doh-url is used; release curl allows only https for DoH
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitTests/Cgr163Tests.cs, Curl.Engine.UnitTests/Cgr155Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3692 — Fix CGR-163: A plain http:// --doh-url is used; release curl allows only https for DoH

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-163 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-163: A plain http:// --doh-url is used; release curl allows only https for DoH

- **Severity:** Medium
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux). Detail: Both (against a release build; the Windows REF is a Debug build, which allows http for DoH)
- **Confidence:** Confirmed. Detail: Confirmed. The difference shows on Linux, and curl's 8.22 docs (`docs/cmdline-opts/doh-url.md:21`) say "The URL must be HTTPS."
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-12`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- *(WSL):* `srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1`, then `curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x`. Case: `linux\cases\vl_f12_http_doh.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1
curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x
```

**Expected (real curl)**

- *REF-LINUX (release):* exits 6 with `Could not resolve host: foo.test` and never contacts the server (trace: `Protocol "http" disabled`).

**Actual (Curl)**

- exits 0 on both platforms and sends the DNS queries over unencrypted HTTP.

**Suspected location in source**

- `Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:124-131` (DohUrlOf accepts http).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr163Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Measured 2026-10-26. REF is curl 8.21.0's Windows release build (`C:\Windows\System32\curl.exe`, Schannel); the lane's mingw curl 8.22.0 is a Debug build, which allows http DoH, so it cannot serve as REF here (the report's REF-LINUX was a release build). No server needed: release curl never connects.
  - `curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x`: REF exit 6, stdout empty, stderr `curl: (6) Could not resolve host: foo.test`, no file, nothing on the wire. PORT (after the fix): identical. Before the fix PORT sent the DoH POSTs over plain HTTP.
  - Scheme-less `--doh-url 127.0.0.1:21800/dns-query`: REF and PORT the same as above (release curl guesses http, then refuses it).
  - Under `-v` PORT writes `* Could not resolve host: foo.test` twice where REF writes it once. `--doh-url bogus` already did this before the fix, so it is a separate gap, filed as BL-3916.
- Decision (ADR-0854): `EngineComposition.CreateDnsResolver`, the resolver behind the shipped curl and every library handle, takes `allowsPlainHttpDohUrl` (off by default). Off, any DoH URL that is not https (`IsHttpsDohUrl`) gets the `UnusableDohUrlResolver`, the same resolver `--doh-url bogus` gets. The harness's dialing paths keep allowing http, standing in for the debug build that upstream cases 2100, 2101 and 2117 run on. `Cgr155Tests` passes `allowsPlainHttpDohUrl: true` for its plain-HTTP DoH server. Touched outside `touches`: `Curl.Engine.UnitTests/Cgr155Tests.cs`, a one-argument change. No task in Doing on origin/work/dark-factory names that file.
- `Cgr163Tests`: with the guard disabled, 3 of its 10 cases failed (the http, HTTP and scheme-less URLs reached the DoH server). With the guard in place all pass. The full fast run passed, 0 failed (Curl.Engine.UnitTests 2667 passed). Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.
- CI on Linux and macOS: the change is platform-neutral (no paths, OS error text or certificates), and the fast tests are green here.

## Log

- 2026-10-26: Created from CGR-163 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A plain http or scheme-less --doh-url resolves nothing (exit 6) and never reaches the server, as release curl
