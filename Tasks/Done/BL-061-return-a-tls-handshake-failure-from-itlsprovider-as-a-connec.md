---
id: BL-061
title: Return a TLS handshake failure from ITlsProvider as a ConnectResult
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-061 — Return a TLS handshake failure from ITlsProvider as a ConnectResult

## Goal

`ITlsProvider.AuthenticateAsClientAsync` returns `ValueTask<ConnectResult>` instead of
throwing on a failed handshake, and `TcpConnector` passes a failed result back to its
caller unchanged, so a TLS failure reaches the handler as an exit code like every other
connect failure.

## Context

ADR-0005 (`Documentation/Planning/Decisions/ADR-0005-protocol-handlers-acquire-transports-through-connectors.md`)
requires a connector to return "every resolve, connect or TLS failure as a `Failed`
result"; only an `OperationCanceledException` may escape. Today
`Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs` returns `ValueTask<IConnection>`,
and `Curl.Networking.UnitLibrary/TcpConnector.cs` says in its remarks that TLS handshake
failures "are not yet mapped to curl's exit codes and propagate from ITlsProvider
unchanged". The provider is the only place that can tell a verification failure
(exit 60, `CURLE_PEER_FAILED_VERIFICATION`) from any other handshake failure (exit 35,
`CURLE_SSL_CONNECT_ERROR`; <https://curl.se/libcurl/c/libcurl-errors.html>, curl 8.21.0),
because only it sees the certificate validation callback. Reusing the existing
`ConnectResult` (`Connected`/`Failed`) needs no new type.

`ITlsProvider` is referenced only by `Curl.Networking.UnitLibrary/TcpConnector.cs` and
`Curl.Networking.UnitTests/Fakes/FakeTlsProvider.cs`, so the change is contained to the
four projects in `touches`. No production `ITlsProvider` exists yet (BL-062 adds it).

## Acceptance criteria

- [x] `ITlsProvider.AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)`
      returns `ValueTask<ConnectResult>`; its XML documentation states that a failed
      handshake is returned as `ConnectResult.Failed` with a curl exit code, that only
      `OperationCanceledException` escapes, and that on failure the provider has
      disposed `plaintext`.
- [x] `TcpConnector.ConnectAsync` returns the provider's `Failed` result unchanged
      (same `ExitCode`, same `ErrorMessage`) when `UseTls` is set; a test in
      `Curl.Networking.UnitTests/TcpConnectorTests.cs` asserts it with a fake provider
      returning `ConnectResult.Failed(CurlExitCode.SslConnectError, "x")`.
- [x] A test asserts that when the provider succeeds, `TcpConnector` returns the
      provider's connection.
- [x] The `TcpConnector` remark about unmapped TLS failures is removed.
- [x] `FakeTlsProvider` can be configured to return either result.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` and
      `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` are clean, and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` and
      `dotnet test Curl.Protocol.Abstractions.UnitTests --filter "TestCategory!=Integration"`
      are green.

## Notes

This touches the shared contract project on purpose, so it runs apart from protocol
tasks; it adds nothing a protocol handler sees.

Delivered in the session rather than through the full `/feature` stage agents: the
task fixes the signature, the connector change and the tests exactly, so a separate
architecture plan would have added nothing (unattended-run default).
`TcpConnector` now returns the provider's `ConnectResult` as it is, success or failure;
`FakeTlsProvider` gained `FailureToReturn` (null means succeed). New test:
`ConnectAsync_WithUseTls_WhenHandshakeFails_ReturnsTheProvidersFailureUnchanged`; the
existing `ConnectAsync_WithUseTls_PassesConnectionAndHostToTlsProviderAndReturnsItsResult`
covers the success case. Networking tests 33, Abstractions tests 63, all green.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ITlsProvider returns a failed handshake as ConnectResult.Failed and TcpConnector passes it through unchanged
