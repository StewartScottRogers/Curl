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
completed:
---
# BL-2519 — Engine: a library handle's perform chooses its proxy from CURLOPT_PROXY and the proxy environment

## Goal

A perform from the options of a managed easy handle with no `ToolInputs` goes through the proxy `CURLOPT_PROXY` (`EasyProxyOptions`) and, as libcurl does, the proxy environment variables (`http_proxy`, `HTTPS_PROXY`, `ALL_PROXY`, `NO_PROXY`) choose, connect-only performs included.

## Context

`ManagedEasyTransfer.OwnInputsOf` gives the context no proxy, so a library handle's normal perform ignores `CURLOPT_PROXY` entirely; only the curl tool's runner chooses one (`TransferPerformer.TrySelectProxyAndCredentials`, through `ToolInputs.Proxy`). BL-2472 made the connect-only perform choose one with `TransferProxySelection.TrySelect` over a `ProxySelector` that reads no environment (`ManagedEasyTransfer.ConnectOnlyProxySelector`, ADR-0608 rule 6). Choose the proxy for every own-inputs perform the same way, and give both an environment reader injected through the engine (`Environment.GetEnvironmentVariable` in production, a dictionary in tests), as libcurl reads the variables for every handle.

## Acceptance criteria

- [ ] A `ManagedEasyTransferTests` test shows a perform from the options with `EasyProxyOptions.Proxy` set puts that proxy on the context (`ITransferContext.Proxy`), and one with `NoProxy` naming the host puts none.
- [ ] A `ManagedEasyTransferTests` test shows an `http_proxy` value from the engine's injected environment reader is used when `EasyProxyOptions.Proxy` is not set, for a normal and for a connect-only perform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [ ] ADR-0608 and `Curl.Engine.UnitLibrary/CLAUDE.md` say the environment is read.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 270bb9ac 8950ec45 4272a6b9 c4aff572 f2bbf5bd.
