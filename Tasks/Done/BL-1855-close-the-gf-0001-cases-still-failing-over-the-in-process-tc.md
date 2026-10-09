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
completed: 2026-10-08
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

- [x] Upstream test1293 passes in `UpstreamConformanceTests` and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test213 sends `CONNECT test.remote.example.com.213:8990 HTTP/1.0` (`--proxy1.0 http://A`); its NTLM line moves to BL-1856.
- [x] The rest (test1008, test1021, test209, test265, test213's NTLM, test217, test750, test1715, test2043) are filed as BL-1856 with their measured first differences.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Split (2026-10-08): the run's budget did not reach all ten cases, so this task closes the two it fixed and BL-1856 holds the rest, its acceptance criteria copied from this task's original ones.
- test213: `ProxyUrlParser` mapped an explicit `http://` scheme to `ProxyKind.Http`, dropping `--proxy1.0`. curl's `parse_proxy` leaves the type HTTP or HTTP/1.0 for `http://`, so `TransferProxySelection.WithHttp10Kind` turns `Http` back into `Http10` when the option was `--proxy1.0` (an environment proxy too, as curl's `CURLOPT_PROXYTYPE` applies to it). The case now fails on its NTLM type-1, like test1008.
- test1293: the harness's `InMemoryServerTcpDialer` routed every end point to the case's server, so `http://0` (0.0.0.0:80) reached it. It now refuses the unspecified address with `SocketError.AddressNotAvailable`, as no upstream server listens there.
- NTLM cases: we send flags 0xA2088207 with a version block; the cases (`!SSPI`) expect curl's hand-built type-1 with flags 0x00088206. That needs `Curl.Ntlm.UnitLibrary` or `Curl.Authentication.UnitLibrary`, outside this task's `touches`.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. test1293 passes and test213 sends HTTP/1.0 CONNECT; the rest split to BL-1856
