---
id: BL-2367
title: Fill CURLINFO_PROXYAUTH_USED and the four CURLINFO_RTSP_* values from the transfer report
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2361]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2367 — Fill CURLINFO_PROXYAUTH_USED and the four CURLINFO_RTSP_* values from the transfer report

## Goal

After `ManagedEasyTransfer.PerformAsync`, `Info.ProxyAuthUsed` holds the `CURLAUTH_*` bit of the scheme the proxy authentication used, and an `rtsp` transfer fills `Info.RtspSessionId`, `RtspClientCSeq`, `RtspServerCSeq` and `RtspCSeqReceived`.

## Context

Left by BL-2361 (ADR-0506 plan phase 2): `TransferReport` has `AuthSchemePicked` for the origin but no proxy-side pick, and no RTSP session ID or CSeq numbers. Add them where the HTTP handler (proxy `Proxy-Authorization` pick) and the RTSP handler (`Session:` header, the CSeq it sent and received) measure them, and read them in `Curl.Engine`'s `TransferInfoFromReport`. Protocol libraries never reference each other; both report through `Curl.Protocol.Abstractions`' `TransferReport`.

## Acceptance criteria

- [ ] `TransferInfoFromReportTests` has one assertion for each of the five values filled from a report that carries it.
- [ ] The HTTP and RTSP handlers' own tests show each value put on the report.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library <every library changed>` reports 0 failing members.

## Notes

## Log

- 2026-10-15: Created.
