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
completed: 2026-09-27
---
# BL-337 — Carry the selected proxy on ITransferContext.Proxy for every scheme

## Goal

`ITransferContext` and `TransferContext` carry `Proxy` (`ProxyEndpoint?`), the proxy selected for the transfer, readable by every protocol handler.

## Context

- ADR-0056, rule 1. Today the selected proxy reaches only `HttpRequestOptions.ForwardProxy`, which only `HttpProtocolHandler` reads, so non-HTTP handlers connect directly.
- `Proxy` is scheme-neutral; `HttpRequestOptions.ForwardProxy` stays as it is (ADR-0056, consequences).
- Changes the shared contract, so it runs apart from the protocol tasks, by design.

## Acceptance criteria

- [x] `ITransferContext.Proxy` exists with an XML doc comment citing ADR-0056, and `TransferContext.Proxy` is an `init` property defaulting to `null`.
- [x] A test in `Curl.Protocol.Abstractions.UnitTests` asserts the default is `null` and an assigned `ProxyEndpoint` is returned.
- [x] Every other `ITransferContext` implementation in the solution (test fakes included) compiles; `dotnet build` clean, fast tests green.

## Notes

- Pipeline `feature`, delivered in-session: the change is one init property on a contract and its round-trip test, so the architect, implementer and reviewer stages were collapsed into one direct edit (sensible default for a two-file contract change; ADR-0056 already holds the design).
- `TransferContext` is the only `ITransferContext` implementation in the solution; every test fake constructs it, so no other project needed an edit and `touches` did not widen.
- Pinned in the two existing tests in `TransferContextTests`: `TransferContext_OnlyRequiredMembersSet_ReportsNotGivenForEveryOption` asserts `Proxy` is `null`, and `TransferContext_EveryMemberSet_RoundTripsEveryValue` asserts an assigned `ProxyEndpoint` reads back by reference.
- Nothing sets or reads `Proxy` yet; `Curl.Console` setting it and each TCP handler passing it to `ConnectTarget` are ADR-0056 rules 1-2, left to their own tasks.
- Verified 2026-09-27: `dotnet build` 0 warnings 0 errors; fast tests all green (Curl.Protocol.Abstractions.UnitTests 371 passed); `dotnet format --verify-no-changes` clean on both projects.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ITransferContext.Proxy carries the selected ProxyEndpoint for every scheme; TransferContext.Proxy is init, default null
