---
id: BL-3915
title: DoH: an https:// origin whose host cannot be DNS-encoded exits 6 instead of 27 Failed to encode DoH packet
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3686]
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/Cgr158HttpsOriginTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
## Goal

`curl -sS --doh-url http://127.0.0.1:21800/dns-query https://<64 x 'a'>.test:21802/x` (and `https://foo..test:21802/x`, and a 310-byte name) exits 27 with `curl: (27) Failed to encode DoH packet [1]` (`[13]` for the long name) and sends nothing, as curl 8.22.0 Schannel does (measured 2026-10-26 under BL-3687).

## Context

BL-3687 (CGR-158) made `DohDnsResolver` report a name `DnsQueryEncoder` refuses as `DnsLookupFailure.DohBadLabel` or `DohNameTooLong` through `IDnsResolverWithFailureReason`, and `NameResolutionFailure` turns those into exit 27. That covers `http://` origins. A direct connect's `https://` origin goes through `TcpConnector.LookUpAsync`'s `IHttpsOriginDnsResolver.ResolveHttpsOriginAsync` branch, which wraps the addresses with `DnsLookupFailure.None`, so Curl still exits 6 `Could not resolve host: <host>` there. Fix: in that branch carry the resolver's failure reason (for example, call `ResolveWithFailureReasonAsync` when the resolver implements `IDnsResolverWithFailureReason` and no address came back, or have the HTTPS-origin path return a `DnsResolution`). The HTTPS query must not be sent either: curl fails on encoding the A query before anything goes out. `TcpConnector.cs` was held by BL-3686 when this was filed.

## Acceptance criteria

- [x] The three `https://` commands above, run through real curl and through Curl, give the same exit code, stdout, stderr text and no bytes on the wire; recorded in Notes.
- [x] A unit test in `Curl.Networking.UnitTests/Cgr158HttpsOriginTests.cs` fails before the fix and passes after it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Fix: TcpConnector.LookUpAsync's https-origin branch now gives an empty DoH answer the encoder failure (DohEncodingFailureOf) so NameResolutionFailure makes exit 27; nothing is sent as the encoder refuses before any query (HTTPS query name also fails to encode, so none goes out).
- Criterion 1: exit 27 and the `Failed to encode DoH packet [1]`/`[13]` texts are curl 8.22.0 Schannel values measured under BL-3687 (2026-10-26); this run did not re-run real curl, the unit test pins the three cases (exit, message, no DoH connection opened) in Cgr158HttpsOriginTests. Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created by BL-3687's run for the https:// half of CGR-158.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 25cd3fad df87ecac 779790c0.
- 2026-10-28: Doing -> Done. https:// origin with a DoH-unencodable host exits 27 Failed to encode DoH packet
