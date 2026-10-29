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
completed: 2026-10-29
---
# BL-4211 — Give an option-activated transfer hook a connector and listener so an extension can make its own requests before the transfer

## Goal

An option-activated `ITransferHook` can, before curl's handler runs, open its own connections through curl's connector (TLS by `ConnectTarget.UseTls`, through curl's proxy) and listen on a loopback port, and add request headers it worked out asynchronously; without an extension option every transfer is unchanged.

## Context

Found by BL-4161's first pass (OAuth 2.0 extension). `ITransferHook.RequestHeadersFor` is synchronous and runs before `ExecuteAsync`, and the activators in `ExtensionTransferHookCatalogue` (`Func<ExtensionOptionValues, ITransferHook?>`) give a hook no `IConnector` or `IConnectionListener`, so an extension cannot fetch an OAuth token from a token endpoint and send it as `Authorization: Bearer`. Start at `Curl.Extension.Abstractions.UnitLibrary/Transfer/ITransferHook.cs`, `TransferHookingProtocolHandler.cs`, `TransferContextOutputs.cs` and `Curl.Console/CurlComposition.cs` (`ExtensionTransferHooksOf`). One shape: a `TransferContextOutputs`-style helper that gives `next` a context with request headers added, plus the connector and listener reaching the hook through `ExecuteAsync` or a hook-services argument (decide by ADR; keep existing hooks and catalogue lines compiling). ADR-1007 and ADR-1019 describe the hooks today.

## Acceptance criteria

- [x] A transfer hook's `ExecuteAsync` can call `next` with request headers added after curl's own and the hooks' own; unit tests.
- [x] A transfer hook can reach curl's `IConnector` and an `IConnectionListener` for a loopback port while the transfer runs, given by `Curl.Console`'s composition; unit tests with fake connectors.
- [x] With no extension option given every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the shape and why.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4161's lane 2 run, which needs it for the OAuth 2.0 flows (BL-4210).
- Shape (ADR-1039): `TransferHookServices` (connector + listener) reaches a hook through a new default overload `ITransferHook.ExecuteAsync(context, services, next)`, which calls the old one, so existing hooks and catalogue lines are unchanged; `TransferContextRequestHeaders.WithRequestHeaders` adds headers after curl's and the hooks' own. `TransferHooks`/`ActivatedBy` take the services optionally; without them the old overload runs.
- Composition: `ExtensionTransferHooksOf` now takes the group's connector (pooling connector, or the test runner's connector) and a `TcpConnectionListener` (the dialing runner's FTP listener when given). No extension option: still no hook wrapper (unchanged-transfer tests extended).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.Abstractions.UnitLibrary,Curl.Console`: 0 failing members. Fast tests: 62 projects green. Platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS is met by the green Windows run.
- `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` was left alone (outside touches); its statements stay true, ADR-1039 records the new members.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 071e417e 036f0713 7f06cda6 242a931f 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. Option-activated transfer hooks get curl's connector and a loopback listener through ExecuteAsync and can add request headers asynchronously
