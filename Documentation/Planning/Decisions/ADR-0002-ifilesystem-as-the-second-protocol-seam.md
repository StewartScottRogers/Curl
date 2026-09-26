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
- **Added 2026-09-26 (BL-026):** every `IFileSystem` implementation, starting with
  `PhysicalFileSystem` (BL-009), must return `FileOpenResult.Failed` with a
  `FileAccessStatus` for every reason an open can fail, and let no exception escape
  either open member except an `OperationCanceledException` from the
  `CancellationToken`. `FileUrlPath` forwards paths the operating system may reject
  (`c|/Windows`, a literal `%`), so a `System.IO`-based implementation has to absorb
  `ArgumentException`, `NotSupportedException`, `PathTooLongException`,
  `DirectoryNotFoundException`, `FileNotFoundException`,
  `UnauthorizedAccessException` and `IOException`. Whatever the operating-system
  error, a failed read open is exit 37 and a failed write open is exit 23
  (<https://curl.se/libcurl/c/libcurl-errors.html>, curl 8.21.0); an escaped
  exception would turn either into a crash and break `FileProtocolHandler`'s promise
  never to throw a transfer failure. The full statement lives in
  `Curl.Protocol.Abstractions.UnitLibrary/CLAUDE.md`.

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

## Amendment, 2026-09-26 — the timestamp is optional (BL-018)

`FileOpenResult.LastWriteTimeUtc` is a `DateTimeOffset?`. `null` means the
`IFileSystem` implementation could not determine a modification time for the opened
handle; it does not mean the epoch, and `FileOpenResult.Failed` always reports `null`.
An absent timestamp has two consequences for `file://`, both following upstream
libcurl 8.21.0:

- **`-z`/`--time-cond`** transfers the body whichever way the condition runs, as
  `Curl_meets_timecondition` does for an unknown document time: a condition that
  cannot be evaluated must not silently suppress data
  (<https://curl.se/libcurl/c/CURLOPT_TIMECONDITION.html>).
- **The header block** leaves out the whole `Last-Modified` line, so it is
  `Content-Length: <n>\r\nAccept-ranges: bytes\r\n\r\n`; `lib/file.c` writes that line
  only when the stat of the opened handle succeeded. This case could not be produced
  from the curl 8.21.0 binary on Windows — `curl -sI file:///NUL` still prints
  `Last-Modified: Thu, 01 Jan 1970 00:00:00 GMT` — so it rests on the upstream source,
  not on a measurement.
