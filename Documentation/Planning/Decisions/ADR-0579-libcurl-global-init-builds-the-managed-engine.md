# ADR-0579 — `CurlGlobal.Init(flags)` builds a `ManagedCurlEngine`, so `Libcurl` references `Curl.Engine.UnitLibrary`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2457.

## Context

`curl_global_init` takes only flags. Until BL-2457, `Libcurl`'s `CurlGlobal.Init` and `InitMemory`
required an `ICurlEngine` (ADR-0556), so a caller had to know the engine to start, and `Libcurl`
referenced `Curl.Api.UnitLibrary` alone. Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`
calls for an init that builds the managed engine itself.

## Decision

1. `Libcurl.csproj` gains a project reference to `Curl.Engine.UnitLibrary` (no package).
2. `CurlGlobal.Init(CurlGlobalFlags flags = CurlGlobalFlags.Default)` creates a library over
   `new ManagedCurlEngine()`, the engine's public constructor: the system clock, the real network, and
   no easy error text, since `curl_easy_strerror` stays in `Curl.Console` until BL-2450. It counts on
   the process-wide init counter, as `Init(ICurlEngine, ...)` does.
3. `Init(ICurlEngine, ...)` and `InitMemory(ICurlEngine, ...)` stay, so tests inject fakes of the seam
   and never reach the network.
4. `CurlLibrary` exposes its engine internally (`Engine`), so a test can show `Init()`'s library is
   backed by `ManagedCurlEngine` without performing a transfer.

## Consequences

- `curl_global_init_mem` without an engine is not added here; it is one more overload when wanted.
- A library from `Init()` performs real transfers; its tests only create and dispose handles.
