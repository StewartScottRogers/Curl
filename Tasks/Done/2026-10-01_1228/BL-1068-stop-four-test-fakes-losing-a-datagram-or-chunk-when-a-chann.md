---
id: BL-1068
title: Stop four test fakes losing a datagram or chunk when a Channel read is cancelled
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitTests, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1068 — Stop four test fakes losing a datagram or chunk when a Channel read is cancelled

## Goal

No test fake reads a `System.Threading.Channels` channel with a cancellable token, so no fast test can lose an item to a cancelled read and wait forever.

## Context

- BL-1067 found that on .NET 10.0.12 an unbounded `Channel<T>` can lose an item written just after a waiting `ReadAsync` is cancelled: about 11 in 300,000 races in a standalone repro (a linked token cancelled, then `TryWrite`, under `DOTNET_PROCESSOR_COUNT=2` with CPU noise). The item is neither returned to the cancelled read nor left in the queue. `Curl.Quic.UnitTests`'s `QuicTestLiveChannel` hung on it and now queues in a `ConcurrentQueue` counted by a `SemaphoreSlim`, whose cancelled `WaitAsync` never takes a count; copy that pattern.
- Fakes still built on `Channel.CreateUnbounded`:
  - `Curl.Console.UnitTests/Http2ServerConnection.cs` (`toClient`)
  - `Curl.Networking.UnitTests/Fakes/InMemoryDuplexStream.cs` (`clientToServer`, `serverToClient`)
  - `Curl.Protocol.Http.UnitTests/Fakes/PushedBytesConnection.cs` (`chunks`)
  - `Curl.Protocol.Ssh.UnitTests/Fakes/InMemoryDuplexConnection.cs` (`toServer`, `toClient`)
- A fake whose reads are never cancelled while a write may race is safe; say so in Notes for each one kept, with the reason.

## Acceptance criteria

- [x] Each of the four fakes either no longer reads a `Channel<T>` with a cancellable token, or Notes says why its reads cannot be cancelled while a write races.
- [x] `dotnet test` on each of the four projects with `--filter "TestCategory!=Integration"` passes 20 consecutive times under `DOTNET_PROCESSOR_COUNT=2`.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- All four fakes replaced, none kept: each one's reads take the caller's token, and the tests
  cancel reads (timeouts, `--max-time`, abandoned streams) while the other end may still write,
  so no fake could honestly be called race-free.
- The three duplex fakes share one shape - byte chunks, a completion that ends reads with zero -
  so each gets a `CancelSafeChunkQueue` (a `ConcurrentQueue` counted by a `SemaphoreSlim`, the
  BL-1067 pattern) with `TryEnqueue`, `Complete` and `DequeueAsync`. Completion queues a `null`
  end marker that a reader puts back, so every later read sees the end, matching the channel's
  `WaitToReadAsync` returning false. The class is copied into each test project rather than
  shared: test projects do not reference each other, and a shared test-support project is more
  than three 70-line files are worth.
- `PushedBytesConnection` queues `Func<byte[]>` answers (bytes, close, throw) and has no
  completion, so it uses the queue and semaphore inline instead of the byte-chunk class.
- Verified: each of the four projects' fast tests passed 20 consecutive times under
  `DOTNET_PROCESSOR_COUNT=2`.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Four fakes queue in a ConcurrentQueue counted by a SemaphoreSlim; 20/20 stress runs green
