---
id: BL-036
title: Dispatch a transfer to the protocol handler registered for its scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-035]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-036 — Dispatch a transfer to the protocol handler registered for its scheme

## Goal

`Curl.Core.UnitLibrary` has a `ProtocolDispatcher` that hands a transfer to the
`IProtocolHandler` whose `SupportedSchemes` contains the URL's scheme, and reports an
unsupported scheme exactly as curl 8.21.0 does.

## Context

`Curl.Protocol.Abstractions.UnitLibrary/IProtocolHandler.cs` says handlers "are registered
with dependency injection and resolved as a set, so `Curl.Core` dispatches on scheme
without referencing any protocol library". `Curl.Core.UnitLibrary` contains no code yet.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24):
`curl XYZ://foo` prints `curl: (1) Protocol "xyz" not supported` and exits 1
(`CURLE_UNSUPPORTED_PROTOCOL`, <https://curl.se/libcurl/c/libcurl-errors.html>). The
scheme in the message is lowercased. The `curl: (1) ` prefix is the console layer's job;
the dispatcher returns only the message.

Shape: `ProtocolDispatcher(IEnumerable<IProtocolHandler> handlers)` with
`ValueTask<TransferResult> DispatchAsync(ITransferContext context)`. Scheme matching
ignores case. Two handlers claiming the same scheme is a composition bug, not a curl
behaviour, so the constructor throws `ArgumentException` naming the scheme.

## Acceptance criteria

- [x] `ProtocolDispatcher` exists in `Curl.Core.UnitLibrary` with the shape in `Context`
      and XML documentation stating the exit 1 case.
- [x] A test in `Curl.Core.UnitTests` with two fake handlers (`file` and `dict`) shows a
      `file:///x` context reaches only the `file` handler, receives the same context
      instance, and that handler's `TransferResult` is returned unchanged.
- [x] A test shows `XYZ://foo` returns `CurlExitCode.UnsupportedProtocol` with
      `ErrorMessage` exactly `Protocol "xyz" not supported`, and no handler is called.
- [x] A test shows an empty handler set returns exit 1 for any URL, and one shows two
      handlers both claiming `dict` make the constructor throw `ArgumentException`
      whose message contains `dict`.
- [x] Every test builds its context with `TransferContext` from
      `Curl.Protocol.Abstractions.UnitLibrary` (ADR-0006, BL-035); `Curl.Core.UnitTests`
      declares no `ITransferContext` implementation of its own.
- [x] Core references only `Curl.Protocol.Abstractions.UnitLibrary`; no protocol library.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` is green,
      with no test tagged `Integration`.

## Notes

Wiring the dispatcher into `Curl.Console` belongs with the end-to-end composition work,
not here.

Delivered in-session rather than through the full `/feature` agent chain: one class, one
test file, and the task's `Context` already fixed the shape, so a separate architect plan
had nothing left to decide. Choices made:

- `ProtocolDispatcher` sits at the project root in namespace `Curl.Core`, beside the
  `FileSystem` folder; scheme dispatch is not file-system work.
- The lookup is a `Dictionary` with `StringComparer.OrdinalIgnoreCase`, so a handler that
  lists a scheme in upper case still matches; the error message lowercases the scheme
  itself rather than relying on `Uri.Scheme` already doing so.
- The duplicate-scheme `ArgumentException` uses `nameof(handlers)` as its parameter name.
- Coverage of `ProtocolDispatcher` measured with the MSTest collector: 100% line, 100% branch.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ProtocolDispatcher in Curl.Core routes a transfer to the handler for its scheme and returns exit 1 'Protocol "xyz" not supported' otherwise
