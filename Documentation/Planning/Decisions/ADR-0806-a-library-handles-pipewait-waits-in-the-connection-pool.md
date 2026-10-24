# ADR-0806 — A library handle's CURLOPT_PIPEWAIT waits in the connection pool

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3351
- Decided by Claude under Stewart's delegation.

## Context

`ManagedCurlEngine.CreateTransports` built a library handle's `EngineToolOptions` with `Parallel` and
`ParallelImmediate` both `false`. The pool's only use of them is `EngineComposition.WaitsForMultiplexing`
(`Parallel && !ParallelImmediate`), so turning `ParallelImmediate` to `true` changed nothing a test could
see: audit finding AF-0340 reported the mutant surviving, and BL-3120's shared constant left it surviving.
Meanwhile `CURLOPT_PIPEWAIT`, which in libcurl makes a transfer wait for a connection being opened to the
same origin to show whether it multiplexes rather than opening another, reached only the multi handle's
start-up check (`ManagedMultiTransfer.WaitsForEarlierConnection`), not the pool.

## Decision

A library handle's transports take `Parallel` from `EasyHttpOptions.PipeWait` and `ParallelImmediate`
`false`, so the pool's `WaitsForMultiplexing` is `CURLOPT_PIPEWAIT`, as the curl tool's is `-Z` without
`--parallel-immediate` (the tool sets `CURLOPT_PIPEWAIT` exactly then). `ManagedCurlEngineTests.Transports.cs`
pins both answers, so either option flipping fails a test.

## Consequences

A handle without `CURLOPT_PIPEWAIT` behaves as before. One with it joins a multiplexing negotiation
already under way to its origin in the pool, as libcurl's does, instead of opening a second connection.
