---
id: BL-5346
title: Carry the transfer's --noproxy or NO_PROXY list on ITransferContext
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs, Curl.Protocol.Abstractions.UnitLibrary/TransferContext.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/HookedTransferContext.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/RetargetedTransferContext.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5346 — Carry the transfer's --noproxy or NO_PROXY list on ITransferContext

## Goal

A handler that opens more than one connection can ask whether a host is exempt from the transfer's proxy, because the context carries the transfer's `--noproxy` list or the `NO_PROXY` variable.

## Context

BL-5345 (oci:// hops matching --noproxy/NO_PROXY must skip the proxy) needs it: `ITransferContext` only has the proxy chosen for the first URL (`Proxy`), no exemption list. Add `string? NoProxy { get; }` to `ITransferContext` (the effective list: `--noproxy` text, else `NO_PROXY`/`no_proxy`, as `Curl.Core.UnitLibrary/ProxySelector.cs` reads it), a settable property on `TransferContext`, forward it in the wrapper contexts in Curl.Extension.Abstractions (and any other ITransferContext wrapper the build flags), and set it in `TransferContextFactory`. Extensions cannot reference Curl.Core, so BL-5345 matches with its own matcher; consider exposing a `Func<string,bool>` exemption test instead if simpler, and record the choice in an ADR.

## Acceptance criteria

- [ ] `ITransferContext` exposes the transfer's no-proxy list (or an exemption test), set by `TransferContextFactory` from `--noproxy` and the environment.
- [ ] Every wrapper context forwards it; build clean, fast tests green, new code covered 100%.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 26834e90 341127ee.
