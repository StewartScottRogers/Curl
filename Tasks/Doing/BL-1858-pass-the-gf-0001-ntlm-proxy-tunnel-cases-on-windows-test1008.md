---
id: BL-1858
title: Pass the GF-0001 NTLM proxy-tunnel cases on Windows (test1008, 1021, 209, 213, 265) and settle test2043
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1858 — Pass the GF-0001 NTLM proxy-tunnel cases on Windows (test1008, 1021, 209, 213, 265) and settle test2043

## Goal

Upstream test1008, test1021, test209, test213 and test265 pass through the in-process harness on Windows and Linux and are listed in `PassingUpstreamCases.txt`; test2043 passes and is listed, or the harness skips it with a stated reason.

## Context

Split from BL-1856. Measured 2026-10-08 on Windows:
- All five NTLM cases fail at the CONNECT's `Proxy-Authorization`: expected curl's hand-built type-1 `TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=` (flags 0x00088206, no version), got SSPI's `TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==`. The cases need `!SSPI`, and `UpstreamCurlPlatform` (`Curl.Conformance.UnitLibrary`) deliberately leaves `SSPI` off because Curl's `curl -V` lacks it, so the cases run, but on Windows `CurlComposition.CreateSecurityContextFactory` routes NTLM to SSPI. The tunnel's contexts are bound inside `CurlComposition.CreateTransports` (`proxyContexts.Bind(...)`), which the harness cannot inject. No origin-NTLM upstream case (test67, 68, 69, 81, 90) is listed either, so the same cause likely holds there.
- Options: let `CurlComposition.CreateRunner` take an `ISecurityContextFactory` (the hand-built NTLM context) that the harness passes for both origin and tunnel; or list `SSPI` as a Windows feature (then the cases are skipped on Windows and cannot be listed while `UpstreamCaseRatchet` fails a listed skip). The first keeps the cases listed on every platform.
- test2043: `--ssl-no-revoke -I https://revoked.badssl.com/` expects exit 0, got 6: it needs the internet. Decide whether the harness screens cases whose URL is not on the harness's server, with a stated reason.

## Acceptance criteria

- [ ] Upstream test1008, test1021, test209, test213 and test265 pass and are listed in `PassingUpstreamCases.txt`.
- [ ] Upstream test2043 passes and is listed, or is skipped by the harness with a stated reason.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
