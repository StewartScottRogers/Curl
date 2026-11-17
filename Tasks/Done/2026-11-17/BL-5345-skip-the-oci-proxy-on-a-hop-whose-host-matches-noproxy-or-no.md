---
id: BL-5345
title: Skip the oci:// proxy on a hop whose host matches --noproxy or NO_PROXY
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5346]
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/TransferEventsConnector.cs, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/HopNoProxyMatcher.cs, Curl.Extended.Protocol.OciRegistry.UnitTests/Client/OciRegistryProxyTests.cs, Curl.Extended.Protocol.OciRegistry.UnitTests/Client/HopNoProxyMatcherTests.cs]
requirement: none
created: 2026-11-16
completed: 2026-11-17
---
# BL-5345 — Skip the oci:// proxy on a hop whose host matches --noproxy or NO_PROXY

## Goal

An `oci://` hop (registry, token endpoint, blob redirect) whose host `--noproxy` or `NO_PROXY` exempts connects directly, while every other hop keeps the transfer's proxy.

## Context

ADR-0056 sends every oci:// hop through `ITransferContext.Proxy`; curl decides the proxy per host. BL-5346 put the list on `ITransferContext.NoProxy` (ADR-1288).

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- The task was claimed with only the template's placeholder criterion, which the board's claim snapshot holds, so it is kept and ticked; what was checked instead: `TransferEventsConnector` opens a hop whose host the context's `NoProxy` list exempts with no proxy and every other hop with the transfer's proxy (`OciRegistryProxyTests`), and the list is matched by curl 8.21.0's `Curl_check_noproxy` rules (`HopNoProxyMatcherTests`).

- Decision (ADR-1290): extensions cannot reference Curl.Core's `NoProxyMatcher`, so the oci library has its own internal `HopNoProxyMatcher` with the same rules. touches widened by the two new files, which no other task can hold.
- A hop left direct because the first URL was exempt stays direct: the context carries only the first URL's proxy.
- Verified: solution build clean, fast tests green, Measure-CodeQuality for the oci library 0 failing members. Tests are pure string matching, so they are platform-neutral.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-16: Doing -> Backlog. Needs BL-5346 (no-proxy list on ITransferContext; abstractions and engine projects outside touches) before the oci hop can match it
- 2026-11-17: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-17: Doing -> Done. oci:// hops whose host matches --noproxy or NO_PROXY connect without the proxy
