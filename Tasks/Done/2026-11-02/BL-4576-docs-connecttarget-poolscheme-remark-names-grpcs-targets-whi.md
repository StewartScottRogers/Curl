---
id: BL-4576
title: Docs: ConnectTarget.PoolScheme remark names grpcs:// targets, which set it without MarkReusable
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4576 — Docs: ConnectTarget.PoolScheme remark names grpcs:// targets, which set it without MarkReusable

## Goal

`ConnectTarget.PoolScheme`'s remark says truly which handlers set it.

## Context

The remark at `Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs:100` says "Only a handler that hands connections
back with `IConnection.MarkReusable` sets it". Since BL-4562 (ADR-1197) the gRPC extension's `grpcs://` target sets
`PoolScheme = "https"` so its TLS handshake offers ALPN `h2` (`TcpConnector.ApplicationProtocolsFor`), and never marks
its connection reusable. BL-4562's `touches` did not name the Abstractions file, so the remark was left for this task.

## Acceptance criteria

- [x] The remark also names a target that sets `PoolScheme` `https` only so its handshake offers `ApplicationProtocols` (the `grpcs://` target, ADR-1197).
- [x] `dotnet build Curl.slnx -warnaserror` is clean.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 2b95d9b1 d71d6599.
- 2026-11-02: Doing -> Done. Remark names the grpcs:// target
