---
id: BL-1855
title: Close the GF-0001 cases still failing over the in-process TcpConnector: NTLM proxy auth, HTTP/1.0 CONNECT, CONNECT reply errors, bad first URL, Schannel revocation
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1855 — Close the GF-0001 cases still failing over the in-process TcpConnector: NTLM proxy auth, HTTP/1.0 CONNECT, CONNECT reply errors, bad first URL, Schannel revocation

## Goal

The ten GF-0001 upstream cases that still fail now that `UpstreamConformanceTests` runs over the production `TcpConnector` (BL-1794) pass, and are listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

- Finding: GF-0001 (gap analysis office, ADR-0433). BL-1794 switched the ratchet to `InProcessCurl`'s TcpConnector path (BL-1831); 18 of its 28 cases then passed. These are what is left, each with its first difference from that run:
  - test1008, test1021, test209, test265: `<protocol>` expected `Proxy-Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=` in the CONNECT request (NTLM proxy authentication through the tunnel).
  - test213: expected `CONNECT test.remote.example.com.213:8990 HTTP/1.0` (`--proxy1.0`), the version differs.
  - test217: `--write-out` expected `000 405`, got `000 000` (`%{http_connect}` after a refused CONNECT).
  - test750: expected `curl: (43) Invalid response header`, got `curl: (56) Proxy CONNECT aborted`.
  - test1715: expected exit 56, got 7.
  - test2043: `--ssl-no-revoke -I https://revoked.badssl.com/` expects exit 0, got 6: the case needs the network; decide whether the harness should skip it as upstream's `<features>` would.
  - test1293: expected `POST /1293`, got `POST /` (the first URL `http://0` must fail without reaching the server).
- Reproduce: `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` and read each case's Inconclusive message.

## Acceptance criteria

- [ ] Upstream test1008 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] Upstream test1021 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] Upstream test209 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] Upstream test265 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] Upstream test213 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] Upstream test217 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] Upstream test750 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] Upstream test1715 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] Upstream test2043 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] Upstream test1293 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt` (or, for test2043, is skipped with a stated reason).
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
