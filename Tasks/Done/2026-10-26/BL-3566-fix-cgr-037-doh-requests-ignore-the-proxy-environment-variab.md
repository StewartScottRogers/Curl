---
id: BL-3566
title: Fix CGR-037: DoH requests ignore the proxy environment variables (http_proxy / https_proxy / ALL_PROXY)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitTests/Cgr037Tests.cs, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs, Curl.Networking.UnitLibrary/DohDnsResolver.RequestEncoding.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3566 — Fix CGR-037: DoH requests ignore the proxy environment variables (http_proxy / https_proxy / ALL_PROXY)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-037 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-037: DoH requests ignore the proxy environment variables (http_proxy / https_proxy / ALL_PROXY)

- **Severity:** High
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed. The verifier reproduced it on Windows and on Linux.
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-1`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv doh=21800 web=21802 web2=21803 rr=foo.test:A:127.0.0.1`, env `http_proxy=http://127.0.0.1:21803 no_proxy=foo.test`, then `curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x -w "%{remote_ip} %{num_connects}\n"`. HTTPS variant: `srv dohs=21801 selfsigned=1 web=21802 web2=21803 rr=foo.test:A:127.0.0.1`, env `https_proxy=http://127.0.0.1:21803 no_proxy=foo.test` (or `ALL_PROXY` / `NO_PROXY`), then `curl -sS --doh-insecure --doh-url https://127.0.0.1:21801/dns-query http://foo.test:21802/x`. Cases: `cases\v_f1_*.txt`, `linux\cases\vl_f1_*.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802 web2=21803 rr=foo.test:A:127.0.0.1
export http_proxy=http://127.0.0.1:21803 no_proxy=foo.test
curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x -w "%{remote_ip} %{num_connects}\n"
srv dohs=21801 selfsigned=1 web=21802 web2=21803 rr=foo.test:A:127.0.0.1
export https_proxy=http://127.0.0.1:21803 no_proxy=foo.test
curl -sS --doh-insecure --doh-url https://127.0.0.1:21801/dns-query http://foo.test:21802/x
```

**Expected (real curl)**

- the DoH sub-transfers use the proxy from the environment. The proxy receives `POST http://127.0.0.1:21800/dns-query HTTP/1.1` twice, or `CONNECT 127.0.0.1:21801` twice for HTTPS DoH. The test proxy answers CONNECT with 403, so REF exits 6 with `Could not resolve host: foo.test`.

**Actual (Curl)**

- the DoH POSTs go straight to the DoH server and the proxy sees nothing. In the HTTPS/CONNECT case PORT exits 0 and fetches the page, while REF exits 6.

**Suspected location in source**

- `Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:133-155` (CreateDohConnector: "No --resolve, --connect-to, proxy or -4/-6 applies to it", ADR-0152).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr037Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Added Curl.Networking.UnitLibrary/DohDnsResolver.cs, .Exchange.cs and .RequestEncoding.cs to touches: the DoH POST and its connect target live there (no task in Doing on origin/work/dark-factory named them).
- Decision (ADR-0818): the DoH requests take the proxy the environment names for the DoH URL (ProxySelector, never -x/--noproxy, as lib/doh.c sets neither); http DoH through an HTTP/HTTPS proxy is POSTed in absolute form with Proxy-Connection: Keep-Alive after Accept; https DoH (or any through SOCKS) is tunnelled, the CONNECT carrying no User-Agent (EngineComposition.DohProxyTunnelOptions). Unusable proxy text resolves nothing (exit 6).
- Measured with Record-CurlExchange.ps1 as the proxy answering 403 (curl 8.22.0 Schannel, then Curl.Console's curl.exe):
  - env http_proxy=http://127.0.0.1:21803 no_proxy=foo.test; curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x -w "%{remote_ip} %{num_connects}\n": both exit 6, stderr `curl: (6) Could not resolve host: foo.test`, stdout ` 0`, proxy gets two `POST http://127.0.0.1:21800/dns-query HTTP/1.1` (Host, Accept, Proxy-Connection: Keep-Alive, Content-Type, Content-Length: 26) - identical bytes per connection; only the order of the two parallel A/AAAA connections differs (racy in both, pre-existing ADR-0152 behaviour).
  - env https_proxy=http://127.0.0.1:21803 no_proxy=foo.test; curl -sS --doh-insecure --doh-url https://127.0.0.1:21801/dns-query http://foo.test:21802/x: both exit 6 with the same stderr and empty stdout; proxy gets two identical `CONNECT 127.0.0.1:21801 HTTP/1.1` / Host / Proxy-Connection: Keep-Alive requests - byte-identical.
- Cgr037Tests (Curl.Engine.UnitTests) pins the forward POST, the tunnel targets, the SOCKS and HTTPS-proxy cases, no proxy, unusable proxy text and the CONNECT bytes; before the fix the DoH target never carried a proxy, so the first test fails without it.
- No repro variant matched before the fix, so none to keep matching; the existing DoH tests (EngineCompositionDohTests, Networking DoH tests) still pass.
- Platform check: the tests use loopback literals and no paths or OS text, so they are platform-neutral; CI on Linux/macOS is met by that and the green fast run here.
- Measure-CodeQuality: Curl.Engine.UnitLibrary and Curl.Networking.UnitLibrary 100% line and branch, 0 failing members (CreateDohResolver split into NewDohResolver for complexity).

## Log

- 2026-10-26: Created from CGR-037 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. DoH requests go through the proxy from http_proxy/https_proxy/ALL_PROXY as curl 8.22.0's do
