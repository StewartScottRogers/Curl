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
completed:
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

- [ ] Each of the four fakes either no longer reads a `Channel<T>` with a cancellable token, or Notes says why its reads cannot be cancelled while a write races.
- [ ] `dotnet test` on each of the four projects with `--filter "TestCategory!=Integration"` passes 20 consecutive times under `DOTNET_PROCESSOR_COUNT=2`.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
