---
id: BL-1394
title: Refuse to resolve a .onion host with curl's exit 6 'Not resolving .onion address (RFC 7686)'
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1394 — Refuse to resolve a .onion host with curl's exit 6 'Not resolving .onion address (RFC 7686)'

## Goal

A transfer whose host Curl itself would resolve and whose name ends in `.onion` or `.onion.` (any letter case) fails before any lookup, `--resolve` entry included, with exit 6 and curl 8.21.0's message `Not resolving .onion address (RFC 7686)`, followed by the usual `Could not resolve:` `-v` lines.

## Context

- Today nothing in the solution mentions `.onion` (`git grep -i onion -- '*.cs'` finds no production code): `TcpConnector` (`Curl.Networking.UnitLibrary/TcpConnector.cs`, the resolve failures around lines 654, 908 and 1045 through `NameResolutionFailure.Describe`) and `UdpDatagramConnector` (line 127) hand such a name to the resolver like any other, so it is looked up in DNS (an RFC 7686 leak) and `--resolve` can map it.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/hostip.c` lines 690-698 (`Curl_resolv`, before the DNS cache is consulted): `if(hostname_len >= 7 && (curl_strequal(&hostname[hostname_len - 6], ".onion") || curl_strequal(&hostname[hostname_len - 7], ".onion."))) { failf(data, "Not resolving .onion address (RFC 7686)"); goto out; }`, and the resolve then fails as an unresolved name does.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel):
  - `curl -sv http://example.onion/`: stderr `* Not resolving .onion address (RFC 7686)`, `* Could not resolve: example.onion:80`, `* Could not resolve: example.onion`, `* closing connection #0`; exit 6.
  - `curl -v http://example.onion./x` ends `curl: (6) Not resolving .onion address (RFC 7686)` (the first failure text wins over `Could not resolve host`).
  - `curl -sv ftp://a.ONION/`: the same lines for port 21; exit 6.
  - `curl -sv --resolve x.onion:80:127.0.0.1 http://x.onion:80/`: `* Added x.onion:80:127.0.0.1 to DNS cache`, then the refusal lines; exit 6.
  - `curl -sv --proxy http://127.0.0.1:1 http://example.onion/`: no refusal (the proxy, not curl, resolves the name); fails on the proxy connect with exit 7.
- `onion` alone and `x.onion.example` are ordinary names (the suffix test needs the dot and at least 7 characters).
- `Curl.Networking.UnitLibrary` is shared by many tasks; keep the change to one check at the start of host resolution and its tests.

## Acceptance criteria

- [x] Tests in `Curl.Networking.UnitTests` connect to `example.onion:80`, `example.onion.:80` and `a.ONION:21` through the connector with a fake resolver that fails the test if it is called, and assert exit 6 (`CurlExitCode.CouldntResolveHost`), message `Not resolving .onion address (RFC 7686)`, and the `-v` lines in the measured order.
- [x] A test pins that a `--resolve` mapping for `x.onion:80` is not used: the refusal comes after the `Added ... to DNS cache` line and no dial is made.
- [x] Tests pin that `onion`, `x.onion.example` and a connect through an HTTP proxy to an `.onion` target are not refused.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- If the `Added ... to DNS cache` line is written outside `Curl.Networking.UnitLibrary`, pin only what this library writes and say so in the Notes when finishing.
- Done: `OnionAddress.IsRefused` (curl's `Curl_resolv` suffix test: at least 7 characters, ending `.onion` or `.onion.`, any case) is checked in `TcpConnector`'s direct connect and QUIC resolve, after the `--resolve` entries load (so `Added ... to DNS cache` is written by this library and pinned) and `--connect-to` maps the host, before the DNS cache or resolver. The refusal writes `Not resolving .onion address (RFC 7686)`, `Could not resolve: H:P` and `Could not resolve: H`, exit 6. A tunnelling proxy's connect never reaches the check, so the proxy resolves the name, as measured.
- Also refused in `UdpDatagramConnector.OpenAsync` (TFTP), which writes no `-v` lines of its own: same message and exit 6, logged at `dns` error.
- `closing connection #0` is written by the protocol layer, not this library, so it is not pinned here.
- The checks sit in non-async wrappers (`ConnectDirectlyTracedAsync`, `ResolveForQuicAsync`) so the async bodies stay within complexity 10. Measure-CodeQuality: 100% line and branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. A .onion host fails with exit 6 'Not resolving .onion address (RFC 7686)' before any lookup or --resolve entry
