---
id: BL-2519
title: Engine: a library handle's perform chooses its proxy from CURLOPT_PROXY and the proxy environment
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2472]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2519 — Engine: a library handle's perform chooses its proxy from CURLOPT_PROXY and the proxy environment

## Goal

A perform from the options of a managed easy handle with no `ToolInputs` goes through the proxy `CURLOPT_PROXY` (`EasyProxyOptions`) and, as libcurl does, the proxy environment variables (`http_proxy`, `HTTPS_PROXY`, `ALL_PROXY`, `NO_PROXY`) choose, connect-only performs included.

## Context

`ManagedEasyTransfer.OwnInputsOf` gives the context no proxy, so a library handle's normal perform ignores `CURLOPT_PROXY` entirely; only the curl tool's runner chooses one (`TransferPerformer.TrySelectProxyAndCredentials`, through `ToolInputs.Proxy`). BL-2472 made the connect-only perform choose one with `TransferProxySelection.TrySelect` over a `ProxySelector` that reads no environment (`ManagedEasyTransfer.ConnectOnlyProxySelector`, ADR-0608 rule 6). Choose the proxy for every own-inputs perform the same way, and give both an environment reader injected through the engine (`Environment.GetEnvironmentVariable` in production, a dictionary in tests), as libcurl reads the variables for every handle.

## Acceptance criteria

- [x] A `ManagedEasyTransferTests` test shows a perform from the options with `EasyProxyOptions.Proxy` set puts that proxy on the context (`ITransferContext.Proxy`), and one with `NoProxy` naming the host puts none.
- [x] A `ManagedEasyTransferTests` test shows an `http_proxy` value from the engine's injected environment reader is used when `EasyProxyOptions.Proxy` is not set, for a normal and for a connect-only perform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [x] ADR-0608 and `Curl.Engine.UnitLibrary/CLAUDE.md` say the environment is read.

## Notes

- Delivered directly (no sub-agents, to keep the run cheap): `ManagedEngineSharedState.ProxySelector` is a `ProxySelector` over an injected environment reader (`ManagedCurlEngine`'s internal constructor's `readEnvironmentVariable`, `Environment.GetEnvironmentVariable` when not given); a share handle's state keeps the engine's selector.
- `ManagedEasyTransfer.TrySelectProxy` chooses the proxy once per perform, before the connect-only branch, replacing the environment-less `ConnectOnlyProxySelector`; `OwnInputsOf` puts it on the context. With `ToolInputs` set, the runner's `Proxy` is used, connect-only included (the runner chose it from the same options and environment). Decision recorded as an amendment to ADR-0608 rule 6.
- Default taken: the existing `ManagedEasyTransferTests` engine helpers now pass a reader that returns `null`, so a developer's own `http_proxy` cannot change their results.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` first found `PerformFromOptionsOverCachesAsync` at complexity 12; the proxy and connect-only step moved to `PerformThroughProxyAsync`, after which 0 failing members, 100% line and branch.
- Tests: `ManagedEasyTransferTests.Proxy.cs` (6 tests). Fast tests all green.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 270bb9ac 8950ec45 4272a6b9 c4aff572 f2bbf5bd.
- 2026-10-16: Doing -> Done. A library handle's perform uses CURLOPT_PROXY and the proxy environment variables, connect-only included
