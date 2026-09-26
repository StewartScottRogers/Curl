---
id: BL-211
title: Measure connect timings and endpoints in TcpConnector and SslStreamTlsProvider
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-160]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-211 — Measure connect timings and endpoints in TcpConnector and SslStreamTlsProvider

## Goal

`TcpConnector` and `SslStreamTlsProvider` fill `ConnectTimings` (lookup, connect, TLS handshake) from `TimeProvider` and the local and remote endpoints.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `Curl.Networking.UnitLibrary/TcpConnector.cs`, `SslStreamTlsProvider.cs`; `ConnectTimings` from BL-160.

## Acceptance criteria

- [ ] Tests on `FakeTimeProvider` with the fake dialer show each timestamp and both endpoints.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Plan item: N1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
