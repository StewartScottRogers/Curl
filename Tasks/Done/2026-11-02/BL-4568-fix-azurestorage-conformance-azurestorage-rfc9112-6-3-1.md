---
id: BL-4568
title: Fix AzureStorage conformance: AzureStorage:RFC9112-6.3-1
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary/Harness/ReferenceTcpDialer.cs, Curl.Conformance.Extended.UnitTests/Harness/ReferenceTcpDialerTests.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4568 — Fix AzureStorage conformance: AzureStorage:RFC9112-6.3-1

## Goal

A reference server behind `ReferenceRoute.Tls` sends close_notify when it ends the connection, as a real TLS server does, so curl reads a close-delimited body to its end.

## Context

Found by BL-4522. `ReferenceTcpDialer.ServeAsync` disposes the stream `TlsServerStream.AcceptAsync` returned without
`TlsServerStream.ShutdownAsync`, so no close_notify is sent; curl's client then fails with `MissingCloseNotifyException`
("schannel: server closed abruptly (missing close_notify)", BL-4540 turns that into an exit code). Real Azure Storage sends
close_notify, so `AzureBlobCredentialAndFramingCases.Download_CloseDelimitedBody_ReadsToTheClose` and
`Download_CloseDelimitedErrorBody_ReadsTheErrorAndExits22` fail only because of the harness. Call `ShutdownAsync` after the
server returns, tolerating a client that has already closed. Check every other extended conformance case on a TLS route
still passes.

## Acceptance criteria

- [x] `ReferenceTcpDialer` sends close_notify on a TLS route when the reference server returns; a unit test in `Curl.Conformance.Extended.UnitTests` pins it.
- [x] The `KnownFailures.txt` lines for `AzureStorage:RFC9112-6.3-1` and `AzureStorage:RFC9112-6.3-2` are removed and those cases pass.
- [x] `dotnet build` clean, fast tests green, `Curl.Conformance.Extended.UnitLibrary` at 100% line and branch coverage.

## Notes

- Pipeline `feature` worked directly: a one-line harness fix (`TlsServerStream.ShutdownAsync` after the server returns on a TLS route) did not warrant the architect and implementer stages.
- No catch around the shutdown: a write to `InMemoryDuplexStream` never fails once the client end is closed (the channel stays writable), and a reference server cannot close its `ReferenceConnection`, so "a client that has already closed" cannot make it throw.
- `ReferenceTcpDialerTests.DialAsync_TlsRouteServerReturns_SendsCloseNotifySoCurlReadsACloseDelimitedBody` pins it; with the shutdown removed it and both Azure cases fail, with it all 388 tests in Curl.Conformance.Extended.UnitTests pass, every other TLS-route case included.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary: 0 failing members. Full build clean, fast tests green on Windows; the change is platform-neutral (in-memory streams, no paths).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 4359b092 7bca20c6 a7d32fd6.
- 2026-11-02: Doing -> Done. Reference TLS routes send close_notify when the server returns, so close-delimited Azure Storage bodies read to the end
