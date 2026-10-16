# ADR-0629 — `Curl.Console` reaches the engine through `Libcurl`'s reference

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2454.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` ends by removing `Curl.Console`'s direct
`ProjectReference` to `Curl.Engine.UnitLibrary`, as curl's tool links libcurl and not libcurl's internals.
Measured against the code as it is now: serial transfers run through a `CurlEasy` (ADR-0624) and `-Z` transfers
through a `CurlMulti` (ADR-0628), both from the `CurlLibrary` that `ToolCurlEngine` gets from `CurlGlobal.Init`.
The tool's `ICurlEngine` (`ToolCurlEngine`, `ToolEasyHandle`) is still assembled from engine types -
`EngineComposition`'s factories, `TransferPerformer`, `CurlErrorBuffer` - in 15 `Curl.Console` files, and
`Libcurl.csproj` references `Curl.Engine.UnitLibrary` without `PrivateAssets`, so the engine flows to every
project that references `Libcurl`.

## Decision

1. `Curl.Console.csproj` drops its `Curl.Engine.UnitLibrary` reference and keeps `Libcurl`'s; the engine reaches
   it transitively through `Libcurl`. `InProcessCurl.RunAsync` and every other public signature is unchanged.
2. The engine types the tool still names stay where they are. Rewriting the tool's `ICurlEngine` without them
   would mean moving the engine's composition behind `Libcurl`'s public surface, which is not a behaviour change
   and is not needed for the tool to be a `Libcurl` client.
3. `Libcurl.csproj` keeps the engine reference public (no `PrivateAssets`): hiding it would break the build of
   every consumer that names an engine type, and ADR-0506 does not ask for it.

## Consequences

- The dependency graph says what the plan says: the tool depends on `Libcurl`, `Libcurl` on the engine.
- No output, exit code or test changes; the conformance ratchet is untouched.
