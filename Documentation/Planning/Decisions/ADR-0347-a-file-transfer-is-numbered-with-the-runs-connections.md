# ADR-0347 — A file:// transfer is numbered with the run's connections

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-977.

## Context

BL-936 made `FileProtocolHandler` write `* shutting down connection #N` and
`* closing connection #N`, numbering from a counter of its own, because the run's count lives
in `ConnectionCache` (ADR-0109, ADR-0285) and nothing handed a number out without connecting.
curl 8.21.0 (Schannel build) shares one count, measured with `Record-CurlExchange.ps1`:
`curl -v http://127.0.0.1:<port>/ file:///C:/Windows/win.ini` leaves the HTTP connection
intact as `#0` and shuts down `#1` for the file; `curl -v file:///... http://... file:///...`
shuts down `#0`, leaves the HTTP connection intact as `#1`, and shuts down `#2`. A file
transfer whose open fails still takes no number (BL-936).

## Decision

1. `Curl.Protocol.Abstractions` gains `IConnectionNumbers` (`NumberNextConnection()`) and
   `ConnectionNumberSequence`, a count of its own from `0`.
2. `ConnectionCache` implements `IConnectionNumbers` with the method the pool and the TFTP
   numbering (ADR-0346) already take numbers from, and `PoolingConnector.ConnectionNumbers`
   hands it out.
3. `FileProtocolHandler(IFileSystem, IConnectionNumbers)` takes each number from the given
   count; `FileProtocolHandler(IFileSystem)` keeps a `ConnectionNumberSequence` of its own.
4. `CurlComposition.ConnectionNumbersOf` passes the pool's count when the run connects
   through a `PoolingConnector` (production), else a fresh sequence, as
   `NumberedDatagramsOf` does for TFTP.

## Consequences

- A run that mixes `file://` with networked schemes numbers every connection as curl does;
  `CurlCommandRunnerFileConnectionNumberTests` pins both orders.
- The file library still references only the abstractions; the shared count reaches it
  through the interface.

## Alternatives considered

- `PoolingConnector` implementing `IConnectionNumbers` itself: puts a number-taking method
  on the connector's public surface beside `ConnectAsync`, where a caller could mistake it
  for part of connecting. The cache already owns the count.
- A fake connect through `IConnector` for a file transfer: the pool would log a connect and
  key an entry for something that is not a connection.
