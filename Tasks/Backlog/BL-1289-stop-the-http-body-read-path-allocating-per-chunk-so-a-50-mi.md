---
id: BL-1289
title: Stop the HTTP body read path allocating per chunk so a 50 MiB GET's heap stays flat
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
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

## Log

- 2026-10-02: Created.
