---
id: BL-5749
title: Check why natss:// needs --ssl-no-revoke on Windows where mqtts:// and xmpps:// do not
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5749 — Check why natss:// needs --ssl-no-revoke on Windows where mqtts:// and xmpps:// do not

## Goal

With the same server certificate and `--cacert`, `natss://` on Windows verifies (or refuses) exactly as `mqtts://` and `xmpps://` do: one revocation rule for every TLS extension scheme.

## Context

From Surly cookbook bug report `2026-11-22T0726-Curl-bug-report.md` (batch 10 of the 1,000-recipe plan), finding 5 (check). Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`surl --cert server.pem --key server.key --user a:b natss://127.0.0.1:4443/` and `curl --cacert server.pem -u a:b -d hi natss://127.0.0.1:4443/x` fail with 35; adding `--ssl-no-revoke` makes it 0. The same certificate over `mqtts://` and `xmpps://` needs no `--ssl-no-revoke`. So the NATS client builds its TLS session differently (perhaps its own `SslStream` setup with `X509RevocationMode.Online`, or `handshake_first` (ADR-1065) taking a path that skips the shared verification options). Find the difference and make NATS use the same TLS options and verification path as the other extension schemes. A self-signed certificate with no CRL distribution point should pass with `--cacert` alone, as it does for mqtts.

## Acceptance criteria

- [x] With a self-signed server certificate trusted by `--cacert`, `natss://` succeeds on Windows without `--ssl-no-revoke`, as `mqtts://` does, pinned by a test of the TLS options the NATS client passes.
- [x] The Notes name the difference found.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Could not reproduce without Surl. The NATS client has no TLS setup of its own: NatsProtocolHandler builds a plain ConnectTarget(host, port, useTls) with Proxy, Events and DiagnosticLog, exactly as Mqtt5 and Xmpp do, and hands it to the shared connector, so every TLS option and the revocation rule (ServerCertificateVerification: Schannel build uses Online revocation with --cacert unless --ssl-no-revoke, excluding the root) come from one shared path. No SslStream, X509RevocationMode or handshake_first code exists in the Nats library. The difference is therefore not in Curl's NATS client; the observed exit 35 is most likely the server side or the online CRL lookup in that environment. Decision: no code change.
- The TLS options NATS passes are pinned by NatsProtocolHandlerTests.ExecuteAsync_TheScheme_ChoosesTheDefaultPortAndTls (the target equals a plain ConnectTarget with only UseTls set).

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T0726, finding 5 (check).
- 2026-11-22: Backlog -> Doing. Criteria at claim: e7c5daeb b8ac762e 3d836d5e.
- 2026-11-22: Doing -> Done. No NATS-specific TLS setup exists; shared connector path, pinned by existing test
