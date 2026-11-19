---
id: BL-5549
title: Use a stored alt-svc h3 entry: a later --alt-svc request to the same origin goes over HTTP/3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/AltSvcTransferCache.cs, Curl.Original.Protocol.Http.UnitLibrary/AltSvcApplicationProtocols.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5549 — Use a stored alt-svc h3 entry: a later --alt-svc request to the same origin goes over HTTP/3

## Goal

With `--alt-svc`, once a server has advertised `h3` and the entry is stored, a later request to the same origin goes over HTTP/3, as curl's alt-svc documentation describes for a build listing HTTP3 in `curl -V`.

## Context

From bug report `2026-11-19T0642-Curl-bug-report.md`, finding 1 (Medium), found while writing Part 4 of the *Surly* cookbook and reproduced just before the report. Build tested: commit `0afbd10e3b`, `curl 8.22.0`, Windows 11, .NET SDK 11.0.100; server Surl at `993aa7072`.

**Repro:** `surl --directory site --self-signed --http3 https://127.0.0.1:8443/` (answers with `Alt-Svc: h3=...`), then:

```
curl -s -k --alt-svc altsvc.txt -o NUL -w "%{http_version}" https://127.0.0.1:8443/hello.txt   # 2
type altsvc.txt          # h2 127.0.0.1 8443 h3 127.0.0.1 8443 "20261120 13:42:59" 0 0
curl -s -k --alt-svc altsvc.txt -o NUL -w "%{http_version}" https://127.0.0.1:8443/hello.txt   # 2 again
```

**Expected:** the second request goes over HTTP/3. The same happens with a second URL in one command, and with `--http2` or `--http1.1` given. This build lists HTTP3 in `curl -V`, and `--http3-only` against the same server works. Upstream could not be compared on that machine: Windows' own `curl.exe` has no HTTP/3.

**Where to start:** `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs`. Its doc comment says an `h3` entry is used with no version option "only when the build acts as a QUIC build, ADR-1268", and its constructor takes `actsAsQuicBuild` ("off, an h3 entry is ignored when no version option is given", BL-5099). ADR-1282 keeps `--http3` on for every platform, and `curl -V` lists HTTP3. Decide whether a build that lists HTTP3 must act as a QUIC build for alt-svc, so that what `-V` claims and what alt-svc does agree. Measure the exact switching rules, including with `--http2` and `--http1.1` given, against curl's HTTP/3-capable build on Linux (`Record-CurlExchange.ps1`, the gap office's reference curl). Record the decision as an ADR (decided by Claude under Stewart's delegation), amending or superseding ADR-1268 where needed. See also ADR-0144 and ADR-0175.

## Acceptance criteria

- [ ] Against a server advertising `h3`, the repro's second request reports `%{http_version}` `3`; or an ADR states, with the measured reference behaviour, why this build does not switch, and `curl -V`'s feature list agrees with it.
- [ ] Behaviour with a second URL in one command, and with `--http2` and `--http1.1`, matches the measured reference curl.
- [ ] Focused unit tests in `Curl.Engine.UnitTests` pin when a stored `h3` entry switches the transfer to HTTP/3.
- [ ] `AltSvcTransferCache`'s doc comment is true of the code.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-19: Created from bug report 2026-11-19T0642-Curl-bug-report.md, finding 1.
