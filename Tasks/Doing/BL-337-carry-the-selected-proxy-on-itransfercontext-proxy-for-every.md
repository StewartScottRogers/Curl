---
id: BL-337
title: Carry the selected proxy on ITransferContext.Proxy for every scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-337 — Carry the selected proxy on ITransferContext.Proxy for every scheme

## Goal

`ITransferContext` and `TransferContext` carry `Proxy` (`ProxyEndpoint?`), the proxy selected for the transfer, readable by every protocol handler.

## Context

- ADR-0056, rule 1. Today the selected proxy reaches only `HttpRequestOptions.ForwardProxy`, which only `HttpProtocolHandler` reads, so non-HTTP handlers connect directly.
- `Proxy` is scheme-neutral; `HttpRequestOptions.ForwardProxy` stays as it is (ADR-0056, consequences).
- Changes the shared contract, so it runs apart from the protocol tasks, by design.

## Acceptance criteria

- [ ] `ITransferContext.Proxy` exists with an XML doc comment citing ADR-0056, and `TransferContext.Proxy` is an `init` property defaulting to `null`.
- [ ] A test in `Curl.Protocol.Abstractions.UnitTests` asserts the default is `null` and an assigned `ProxyEndpoint` is returned.
- [ ] Every other `ITransferContext` implementation in the solution (test fakes included) compiles; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
