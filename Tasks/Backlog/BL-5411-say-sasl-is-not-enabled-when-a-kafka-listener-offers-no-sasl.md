---
id: BL-5411
title: Say SASL is not enabled when a Kafka listener offers no SASL mechanisms
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Wire/KafkaSasl/KafkaSaslMessages.cs, Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaClientFlow.cs, Curl.Extended.Protocol.Kafka.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5411 — Say SASL is not enabled when a Kafka listener offers no SASL mechanisms

## Goal

`-u a:b` against a Kafka listener that offers no SASL mechanism says that SASL is not enabled on the listener (exit 67), without an empty "it enables: " list.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-084 (Low, Confirmed twice). Area: kafka.

**Repro.** The plaintext broker from EXT-075 (a Kafka broker on 127.0.0.1:48092 with a PLAINTEXT listener and no SASL): `curl -sS -m 10 -u a:b -d x kafka://127.0.0.1:48092/t1`

**Expected.** An error saying the listener does not accept SASL authentication, exit 67 (consistent with other login failures).
**Actual (PORT).** `curl: (67) Kafka broker does not enable SASL PLAIN, it enables: ` (empty list).
**Where to start.** `Curl.Extended.Protocol.Kafka.UnitLibrary/Wire/KafkaSasl/KafkaSaslMessages.cs`, `PlainNotEnabled` (line 8) joins the mechanism list without checking for empty; its caller, `Client/KafkaClientFlow.cs` (line ~32, on `handshake.ErrorCode != 0`), should also tell the SaslHandshake error codes apart (for example UNSUPPORTED_SASL_MECHANISM 33 with a list, versus an empty list or ILLEGAL_SASL_STATE 34).

## Acceptance criteria

- [ ] New tests in `Curl.Extended.Protocol.Kafka.UnitTests` (in `Wire/KafkaSasl`, a new file) prove `PlainNotEnabled` with an empty list returns a message that says SASL is not enabled on the listener and contains no "it enables" text.
- [ ] A test proves `PlainNotEnabled` with a non-empty list (for example `SCRAM-SHA-256`) still names the enabled mechanisms (the existing `KafkaSaslMessageTests` case stays green).
- [ ] A handler test with a scripted broker whose SaslHandshake answers an error and no mechanisms proves exit 67 and the new message end to end.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: the review's command prints a message that says SASL is not enabled on the listener (no trailing empty list), exit 67.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- `touches` includes `Client/KafkaClientFlow.cs` for the caller's error-code check.
- Review acceptance check: the command prints a message that says SASL is not enabled on the listener (no trailing empty list), exit 67.

## Log

- 2026-11-17: Created.
