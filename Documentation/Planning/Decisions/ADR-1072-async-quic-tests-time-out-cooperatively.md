# ADR-1072: Asynchronous QUIC tests time out cooperatively

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4269)

## Context

BL-1067 gave every asynchronous test in `Curl.Quic.UnitTests` a two-minute `[Timeout]` so a lost
wait fails the test instead of hanging the run. That timeout was MSTest's non-cooperative kind, which
runs the test on a thread-pool thread and blocks a second pool thread waiting for it. Tests run
method-level parallel (`MSTestSettings.cs`), so up to one such pair per worker is blocked at once. The
connection loop, the handshake and `QuicTestLiveChannel.HangGuard` all need free pool threads to run their
continuations and timer callbacks. In the full fast run on a loaded machine the pool cannot grow (it adds
threads slowly, and not at all while the CPU is nearly saturated), so ten connection tests waited until
the kernel-timed `[Timeout]` failed them at 120 s, before even the 60 s guard could fire. Capping the
pool (`DOTNET_ThreadPool_ForceMaxWorkerThreads=4`) reproduces it every time, even for tests that only
handshake and dispose.

## Decision

- Every `[Timeout(QuicTest.HangTimeoutMilliseconds)]` in the project is
  `CooperativeCancellation = true`: MSTest cancels `TestContext.CancellationToken` at the timeout and
  awaits the test, blocking no thread.
- So that the timeout still ends a hung test, every wait in those tests either passes
  `TestContext.CancellationToken` (in place of `CancellationToken.None`) or is bounded by `HangGuard`.
  Disposing a connection stops its loop, which needs no token.

## Consequences

- The project's 506 tests pass with four pool threads in under a second. Before the change, 6 or more
  of them timed out after two minutes each.
- A new asynchronous QUIC test must follow both rules. A wait that observes neither the token nor the
  guard turns a hang back into a hung run.
