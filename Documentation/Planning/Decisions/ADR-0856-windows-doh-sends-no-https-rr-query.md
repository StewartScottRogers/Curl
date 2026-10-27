# ADR-0856: On Windows `--doh-url` sends no HTTPS-RR query, as the Schannel build has no HTTPSRR

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3686 (CGR-157)
- Decided by Claude under Stewart's delegation.
- Amends: [ADR-0727](ADR-0727-doh-sends-the-https-rr-query-first-for-an-https-origin.md), which made
  every platform behave as an HTTPSRR build

## Context

ADR-0727 made `TcpConnector` resolve every direct `https://` origin through
`IHttpsOriginDnsResolver.ResolveHttpsOriginAsync`, so `DohDnsResolver` posts an HTTPS (type 65)
query for `_<port>._https.<host>` before A and AAAA, as an HTTPSRR build of curl 8.22.0 does. It
recorded that the Windows reference build lists no `HTTPSRR` and sends no such query, and chose the
HTTPSRR behaviour everywhere because Curl's `-V` then listed `HTTPSRR` on every platform. ADR-0808
has since dropped `HTTPSRR` from the Windows Features line to match the Schannel build.

The third-party gap report's CGR-157 measured the difference: with
`curl -sS -k --doh-url http://127.0.0.1:21800/dns-query https://foo.test:21805/x` the reference curl
sends two DoH POSTs (A, AAAA) and Curl three, the extra one telling the DoH server the target port.
Measured again for BL-3686 with `Record-CurlExchange.ps1 -NoServer -DohPort 21900` and curl 8.22.0
(Schannel, x86_64-w64-mingw32): two queries, `foo.test` type 1 and type 28, then exit 7.

## Decision

- `TcpConnector.AsksHttpsRecords` (init, default `!OperatingSystem.IsWindows()`) gates the HTTPS
  origin resolve: off on Windows, where an `https://` origin is resolved with `ResolveAsync` alone
  and no `HTTPS-RR` line is written; on elsewhere, as ADR-0727 decided.
- `--ech` keeps asking the HTTPS record through `IEchConfigListLookup` on every platform
  (ADR-0312): that query belongs to ECH, not to the HTTPSRR resolve.
- The conformance harness reports `HTTPSRR` to upstream cases on Linux and macOS only
  (`UpstreamUnixFeatures`), so case 2100's `%if HTTPSRR` protocol is held there and is
  `SkippedOnThisPlatform` on Windows.
- Linux keeps the HTTPSRR behaviour its `-V` line advertises. CGR-157 reports that the Linux
  reference (curl 8.18.0, OpenSSL) also sent two queries; that platform's `-V` line and query are
  measured and decided together under the follow-up task BL-3686 files, since a lane cannot run
  the Linux reference.

## Consequences

The Windows build's DoH traffic and `-V` line agree with each other and with the Schannel build.
Tests that pin the HTTPSRR behaviour set `AsksHttpsRecords = true`, so they hold on every platform.
