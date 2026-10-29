---
id: BL-4211
title: Give an option-activated transfer hook a connector and listener so an extension can make its own requests before the transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.Abstractions.UnitTests/Transfer, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionTransferHookTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4211 — Give an option-activated transfer hook a connector and listener so an extension can make its own requests before the transfer

## Goal

An option-activated `ITransferHook` can, before curl's handler runs, open its own connections through curl's connector (TLS by `ConnectTarget.UseTls`, through curl's proxy) and listen on a loopback port, and add request headers it worked out asynchronously; without an extension option every transfer is unchanged.

## Context

Found by BL-4161's first pass (OAuth 2.0 extension). `ITransferHook.RequestHeadersFor` is synchronous and runs before `ExecuteAsync`, and the activators in `ExtensionTransferHookCatalogue` (`Func<ExtensionOptionValues, ITransferHook?>`) give a hook no `IConnector` or `IConnectionListener`, so an extension cannot fetch an OAuth token from a token endpoint and send it as `Authorization: Bearer`. Start at `Curl.Extension.Abstractions.UnitLibrary/Transfer/ITransferHook.cs`, `TransferHookingProtocolHandler.cs`, `TransferContextOutputs.cs` and `Curl.Console/CurlComposition.cs` (`ExtensionTransferHooksOf`). One shape: a `TransferContextOutputs`-style helper that gives `next` a context with request headers added, plus the connector and listener reaching the hook through `ExecuteAsync` or a hook-services argument (decide by ADR; keep existing hooks and catalogue lines compiling). ADR-1007 and ADR-1019 describe the hooks today.

## Acceptance criteria

- [ ] A transfer hook's `ExecuteAsync` can call `next` with request headers added after curl's own and the hooks' own; unit tests.
- [ ] A transfer hook can reach curl's `IConnector` and an `IConnectionListener` for a loopback port while the transfer runs, given by `Curl.Console`'s composition; unit tests with fake connectors.
- [ ] With no extension option given every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the shape and why.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4161's lane 2 run, which needs it for the OAuth 2.0 flows (BL-4210).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 071e417e 036f0713 7f06cda6 242a931f 478f867b 3d836d5e.
