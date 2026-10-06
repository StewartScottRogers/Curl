---
id: BL-977
title: Number file:// transfers from the same connection count as networked ones
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-936]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-02
---
# BL-977 — Number file:// transfers from the same connection count as networked ones

## Goal

In a run that mixes `file://` with a networked scheme, `* shutting down connection #N` for a `file://` transfer carries the number curl 8.21.0 gives it: one count shared with every connection the run opens.

## Context

- BL-936 made `FileProtocolHandler` write `* shutting down connection #N` / `* closing connection #N`, numbering from its own counter (`lastConnectionNumber`), because the shared count lives in `Curl.Networking.UnitLibrary/PoolingConnector.cs` (`_nextConnectionNumber`) and no abstraction hands a number out without connecting.
- curl numbers `file://` transfers as connections in the one count (measured in BL-936: `curl -v a a nope a` over `file://` prints #0, #1, nothing for the failed open, #2). Measure a mix first, e.g. `curl -v http://127.0.0.1:<port>/ file:///<dir>/a.txt`, with `Record-CurlExchange.ps1`, and pin its answer.
- Likely shape: an abstraction (say `IConnectionNumbers` in `Curl.Protocol.Abstractions.UnitLibrary`) that `PoolingConnector` implements and `CurlComposition` passes to `FileProtocolHandler`.

## Acceptance criteria

- [x] The measured stderr for a mixed `http://` + `file://` run is copied into Notes.
- [x] A test pins that a `file://` transfer after a networked connection reports the shared next number.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] Coverage for each touched library stays at 100% line and branch (`Measure-CodeQuality.ps1`).

## Notes

Measured with curl 8.21.0 (Windows, Schannel), `Record-CurlExchange.ps1`, blank lines dropped:

`curl -v -s -o NUL -o NUL http://127.0.0.1:48983/ file:///C:/Windows/win.ini`
```
*   Trying 127.0.0.1:48983...
* Established connection to 127.0.0.1 (127.0.0.1 port 48983) from 127.0.0.1 port 54141
* using HTTP/1.x
> GET / HTTP/1.1
> Host: 127.0.0.1:48983
> User-Agent: curl/8.21.0
> Accept: */*
>
* Request completely sent off
< HTTP/1.1 200 OK
< Content-Length: 0
<
* Connection #0 to host 127.0.0.1:48983 left intact
{ [92 bytes data]
* shutting down connection #1
```

`curl -v -s -o NUL -o NUL -o NUL file:///C:/Windows/win.ini http://127.0.0.1:48984/ file:///C:/Windows/win.ini`
```
{ [92 bytes data]
* shutting down connection #0
*   Trying 127.0.0.1:48984...
...
* Connection #1 to host 127.0.0.1:48984 left intact
{ [92 bytes data]
* shutting down connection #2
```

So one count, both ways. (A first attempt used a temp path with a space and a `Z:` path curl
could not open; both gave exit 37 and are not the answer.)

Design (ADR-0347): `IConnectionNumbers` and `ConnectionNumberSequence` in the abstractions;
`ConnectionCache` implements it and `PoolingConnector.ConnectionNumbers` hands it out;
`FileProtocolHandler(IFileSystem, IConnectionNumbers)`, the one-argument constructor keeping a
count of its own; `CurlComposition.ConnectionNumbersOf` passes the pool's count. Tests:
`CurlCommandRunnerFileConnectionNumberTests` (both orders, through the composition),
`FileProtocolHandlerTransferEventTests` (download and upload take the shared next number),
`PoolingConnectorSharedCacheTests.ConnectionNumbers_*`, `ConnectionNumberSequenceTests`.

Coverage (`Measure-CodeQuality.ps1` for the four libraries): Curl.Console, Abstractions and
File at 100/100; Networking at 99.99 lines only for `UdpChannelOpener.OpenFrom` line 53, code
this task did not touch, already filed as BL-1154.

`touches` gained `Curl.Console.UnitTests`: the composition test lives there, and no other task
in Doing on `origin/work/dark-factory` named it.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. file:// transfers take their connection number from the run's shared count, as curl 8.21.0 does
