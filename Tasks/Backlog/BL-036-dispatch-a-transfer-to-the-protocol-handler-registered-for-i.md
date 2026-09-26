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
completed:
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

- [ ] `ProtocolDispatcher` exists in `Curl.Core.UnitLibrary` with the shape in `Context`
      and XML documentation stating the exit 1 case.
- [ ] A test in `Curl.Core.UnitTests` with two fake handlers (`file` and `dict`) shows a
      `file:///x` context reaches only the `file` handler, receives the same context
      instance, and that handler's `TransferResult` is returned unchanged.
- [ ] A test shows `XYZ://foo` returns `CurlExitCode.UnsupportedProtocol` with
      `ErrorMessage` exactly `Protocol "xyz" not supported`, and no handler is called.
- [ ] A test shows an empty handler set returns exit 1 for any URL, and one shows two
      handlers both claiming `dict` make the constructor throw `ArgumentException`
      whose message contains `dict`.
- [ ] Every test builds its context with `TransferContext` from
      `Curl.Protocol.Abstractions.UnitLibrary` (ADR-0006, BL-035); `Curl.Core.UnitTests`
      declares no `ITransferContext` implementation of its own.
- [ ] Core references only `Curl.Protocol.Abstractions.UnitLibrary`; no protocol library.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` is green,
      with no test tagged `Integration`.

## Notes

Wiring the dispatcher into `Curl.Console` belongs with the end-to-end composition work,
not here.

## Log

- 2026-09-26: Created.
