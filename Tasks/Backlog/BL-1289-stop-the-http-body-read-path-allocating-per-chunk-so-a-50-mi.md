---
id: BL-1289
title: Stop the HTTP body read path allocating per chunk so a 50 MiB GET's heap stays flat
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1290]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1289 — Stop the HTTP body read path allocating per chunk so a 50 MiB GET's heap stays flat

## Goal

A plain `http://` GET of a 50 MiB `Content-Length` body with `-s -o <file>` allocates under 256 KiB of managed memory between the response head and the end of the body, so the native `curl.exe`'s peak working set no longer grows with the body (BL-1288).

## Context

Found in BL-1288 (its Notes) on 2026-10-02. In the native AOT build the GC never runs during a 50 MiB
GET (`GC.CollectionCount(0)` is 0 at exit): gen0's budget is larger than everything the run allocates,
so every byte allocated is a fresh page that stays in the working set. Its private memory grows
linearly with the body: 5.32 MB with no body bytes received, 5.99 MB after 10 MiB, 7.72 MB after 40 MiB.
`GC.GetTotalAllocatedBytes(true)` at exit, with BL-1288's changes (piped standard output written
synchronously): 1.85 MB for `-o`, 1.56 MB for piped standard output, against 0.38 MB for `--version`;
about 450 bytes per 16 KiB chunk. GC tuning cannot cap it: `DOTNET_GCgen0size`, `DOTNET_GCgen0MaxBudget`
and a `RuntimeHostConfigurationOption` for `GCgen0size` all left the collection count at 0.

The cause is on the read side shared by both runs, since the `-o` and stdout writes now allocate
little: start at `HttpResponseBodyReader.CopyFramedAsync` (its 16 KiB buffer is allocated once, BL-1274)
and follow each chunk through the connection's stream (`IConnection`, `Curl.Networking`'s socket
connection) and the `Curl.Core` wrappers the runner puts around the output and progress sink
(`LowSpeedWatchdog`, `MaxTimeWatchdog`, the progress recorder): async `Task` methods that complete
asynchronously allocate a state-machine box per call, as does a closure, a boxed struct or a `string`
built per chunk. Prefer `ValueTask` and pooled `IValueTaskSource`s as `Socket`'s own
`ReceiveAsync(Memory<byte>)` does.

Measure as BL-1288's Context says (a C# file-based app, loopback `TcpListener`, `K32GetProcessMemoryInfo`
after exit); a temporary `System.Console.Error.WriteLine(GC.GetTotalAllocatedBytes(true))` at the end of
`Program.Main` shows the allocation, and must not be committed.

## Acceptance criteria

- [ ] Measured with a temporary line at the end of `Program.Main`, a 50 MiB `-s -o <file>` GET from a loopback server allocates at most 256 KiB more than `curl --version` does, with the numbers recorded under Notes.
- [ ] The same for `-s` to a piped standard output.
- [ ] No output byte, exit code or `-v` line changes: `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

### Run of 2026-10-02 (lane 1): back to Backlog behind BL-1290

What allocates, found with `dotnet-dump` (installed as a global tool): since the GC never runs in
the transfer, a heap dump taken just before exit still holds every object the run allocated, so
`dotnet-dump analyze <dmp> -c "dumpheap -stat"` lists them by type. Measured on the JIT build
(`dotnet build Curl.Console -c Release`, run as `dotnet curl.dll`), with a temporary
`GC.GetTotalAllocatedBytes(true)` line and a `Thread.Sleep` at the end of `Program.Main` so the harness
can collect the dump (`dotnet-dump collect -p <pid> --type Heap`). EventSource listeners get no
`GCAllocationTick` events in this app, so do not bother with them. The harness was a C# file-based app
outside the repository (loopback `TcpListener`, one `200` with `Content-Length: 52428800`).

| Build | `--version` | `-s -o file` | `-s` to a pipe |
| --- | --- | --- | --- |
| Before | 293,608 | 1,978,864 | 3,351,912 |
| `HttpResponseBodyReader` pooled builders | 284,984 | 1,012,224 | 2,386,856 |

1. **Done, left uncommitted for the shift to stash (rule 6):** `HttpResponseBodyReader`'s per-chunk
   `ReadAsync`, `WriteAsync`, `WriteWithinLimitAsync` and `WriteChunkDataAsync` carry
   `[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]` (or the non-generic one), as the
   BCL's own socket and stream paths do. Their state-machine boxes were boxed afresh per chunk, about
   300 bytes each; it saved about 970 KB on both runs. If the stash is gone, it is four attributes and a
   `using System.Runtime.CompilerServices;`.
2. The same attribute on `PooledConnection`, `TcpIoTraceConnection`, `ConnectionStream`,
   `SslStreamConnection`, `HttpFirstByteTimingConnection`, `HttpContinueWaitConnection`,
   `LowSpeedWatchdog`'s stream and `RateLimitedStream` changed nothing measurable (no box of theirs in the
   dump), so it was reverted. `PhysicalFileSystem`'s 4096-byte `FileStream` buffer set to 0 also changed
   nothing.
3. **What is left is all in `Curl.Console`,** which BL-1290 (in Doing on lane 2) holds, so `Curl.Console`
   and `Curl.Console.UnitTests` were added to `touches` and the task waits on BL-1290:
   - `-o`: 3,187 `AsyncStateMachineBox<DeferredOutputFileStream.<WriteAsync>>`, 510 KB: one per chunk,
     because its `await target.WriteAsync` completes asynchronously. Pool its builder (or return the
     inner `ValueTask` directly once the file is open).
   - pipe: per chunk, a `StandardOutputFailureDeferringStream.<WriteAsync>` box (435 KB), a
     `WriteGate.<RunExclusiveAsync>` box (384 KB), a `WriteGateStream` closure and its `Func<Task>`
     (360 KB), and two `ExecutionContext`s with a `TwoElementAsyncLocalValueMap` each (564 KB: an
     `AsyncLocal` set per write). BL-1288's uncommitted `SynchronousWriteStream` (its Notes) removes the
     thread-pool part of the stdout write on top of this. Without `-Z` the gate is not needed per chunk.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Backlog. Needs Curl.Console (its DeferredOutputFileStream, WriteGate and StandardOutputFailureDeferringStream allocate per chunk), which BL-1290 in Doing touches; waits on BL-1290
