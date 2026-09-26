# ADR-0002 — `IFileSystem` as the second protocol seam

- **Status:** Accepted
- **Date:** 2026-09-25

## Context

Rule 2 of the architecture (`Documentation/Product/Product-Overview.md`) says a
protocol handler never constructs a `Socket`, an `SslStream` or an `HttpClient`; it
receives `IConnection`, an undifferentiated duplex byte pipe: `ReadAsync`,
`WriteAsync`, `FlushAsync`, `IsSecure`, `RemoteEndPoint`. That shape fits every wire
protocol in scope. It does not fit `file`.

`IConnection` carries no file length, no last-write timestamp, no seek, no
create/truncate/append mode, and no way to distinguish "does not exist" from "is a
directory" from "permission denied". Every observable `file://` behaviour is
metadata or positioning: `Content-Length` and `Last-Modified` come from the opened
handle, `-r`/`--range` and `-C`/`--continue-at` are seeks, and curl's choice between
exit 37 (`CURLE_FILE_COULDNT_READ_FILE`) and exit 23 (`CURLE_WRITE_ERROR`) depends on
which direction the open failed in — source or destination
(<https://curl.se/libcurl/c/libcurl-errors.html>, checked against curl 8.21.0).
Routing that through `IConnection` would either move the 37-versus-23 decision out
of the only library that knows the `file` scheme, or require inventing a
header-and-body byte protocol between the connection and the handler — and then
writing tests that assert the bytes of a protocol that does not exist. `IsSecure`
and `RemoteEndPoint` are meaningless for a local file either way.

## Decision

The `file` scheme's handler takes an injected `IFileSystem`, not `IConnection`.

The rule being amended is satisfied in substance, not in letter:
`IProtocolHandler.ExecuteAsync(ITransferContext)` does not take a connection —
connections are constructor-injected per handler — and the stated purpose of Rule 2
("if a protocol needs a live server to test, the seam is in the wrong place",
Success criterion 4 in the overview) is honoured, because
`FileProtocolHandler(IFileSystem)` constructs no `Socket`, no `SslStream`, no
`HttpClient` and no `FileStream`, and every one of its tests runs without touching a
disk.

The production implementation, `PhysicalFileSystem`, lives in
`Curl.Core.UnitLibrary` under a `FileSystem\` folder — not in
`Curl.Networking.UnitLibrary`. Networking's charter is sockets, DNS, TLS and
proxies, and a file system is not a network. Core already owns local-disk concerns
(`-o`, `--output-dir`, `--create-dirs`). `PhysicalFileSystem`'s one disk-touching
test lands in `Curl.Core.UnitTests` tagged `[TestCategory("Integration")]`, which is
permitted there and forbidden in a protocol's own tests — that placement is what
keeps `Curl.Protocol.File.UnitTests` entirely in the fast run.

## Consequences

Good:

- `file://` behaviour — length, timestamp, seek, the 37/23 split — is expressible
  directly, against an interface shaped for a file rather than forced through one
  shaped for a wire.
- `Curl.Protocol.File.UnitTests` needs no recorded byte stream and no server; it
  drives `FileProtocolHandler` against a fake `IFileSystem` entirely in memory.
- Local-disk responsibility stays in one place (`Curl.Core.UnitLibrary`), alongside
  the other local-disk options it already owns.

Costs and caveats:

- Protocol handlers now have two possible transport seams rather than one, and a
  future scheme must pick the right one rather than defaulting to `IConnection` by
  habit.
- The boilerplate `IConnection` paragraph — "Never construct a `Socket`, `SslStream`
  or `HttpClient` here. Take `IConnection` so the tests in the matching
  `.UnitTests` project can drive this code from a recorded byte stream with no
  network." — is currently identical across all 16 protocol `CLAUDE.md` files. It
  remains correct for the other 15; only `Curl.Protocol.File.UnitLibrary/CLAUDE.md`
  is amended by this ADR.

## Alternatives considered

- **Force `file` through `IConnection`, inventing a byte-level protocol for it.**
  Rejected: there is no such protocol upstream, so this would mean designing one
  and testing it — machinery that exists only to satisfy an interface, not to model
  `file://`.
- **Give `IConnection` optional metadata members (length, timestamp, seek) for the
  schemes that have them.** Rejected: it would pollute the one contract every wire
  protocol implements with members that are meaningless for all of them, and the
  15 real implementations would carry `NotSupportedException` stubs forever.
- **Put `PhysicalFileSystem` in `Curl.Networking.UnitLibrary`, reasoning that it sits
  beside `IConnection`'s production implementations.** Rejected: a local file system
  is not a network concern, and Core already owns the other local-disk options this
  implementation must cooperate with.

## Amendments carried by this ADR

1. `Curl.Protocol.File.UnitLibrary/CLAUDE.md` — the boilerplate `IConnection`
   paragraph is replaced with the `IFileSystem` seam.
2. `Documentation/Product/Product-Overview.md` — Rule 2 is amended to state that the
   transport is an injected seam: `IConnection` for the wire protocols, `IFileSystem`
   for `file`.
