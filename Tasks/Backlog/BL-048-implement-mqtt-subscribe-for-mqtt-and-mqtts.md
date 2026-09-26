---
id: BL-048
title: Implement MQTT subscribe for mqtt and mqtts
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-033, BL-034, BL-035]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-048 — Implement MQTT subscribe for mqtt and mqtts

## Goal

`MqttProtocolHandler` in `Curl.Protocol.Mqtt.UnitLibrary` serves `mqtt` and `mqtts`:
it connects, subscribes to the URL's topic, and writes each received PUBLISH to `Output`
in curl 8.21.0's format, with curl's exit codes for a refused CONNECT, an empty topic and
a closed connection.

## Context

Requirements: the MQTT rows BL-033 adds to `Documentation/Product/Requirements.md`.
Seams: ADR-0005 (`IConnector`; `mqtts` is the same handler with
`ConnectTarget.UseTls = true`) and ADR-0006 (`TransferContext`). Protocol: MQTT 3.1.1
(OASIS standard, <https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html>),
including the variable-length "remaining length" encoding of section 2.2.3. Upstream:
<https://curl.se/docs/mqtt.html> - "curl outputs two bytes topic length (MSB | LSB), the
topic followed by the payload" - and default port 1883
(<https://curl.se/docs/url-syntax.html>), both checked 2026-09-26. Exit codes:
<https://curl.se/libcurl/c/libcurl-errors.html>.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24) against a
loopback listener:

- `curl mqtt://h/a/b/c` sent CONNECT
  `10 18 00 04 "MQTT" 04 02 00 3C 00 0C "curl" <8 random alphanumerics>` (protocol level
  4, clean session, keep-alive 60, client identifier `curl` plus eight random
  characters). After CONNACK `20 02 00 00` it sent SUBSCRIBE
  `82 0A 00 01 00 05 "a/b/c" 00` (packet identifier 1, QoS 0). After SUBACK
  `90 03 00 01 00` and PUBLISH `30 0C 00 05 "a/b/c" "HELLO"`, it wrote
  `00 05 "a/b/c" "HELLO"` to stdout; when the server then closed, stderr was
  `curl: (56) Connection disconnected`, exit 56 (`RecvError`).
- `mqtt://h/a%2Fb` subscribed to `a/b`: the topic is percent-decoded.
- CONNACK `20 02 00 05`: exit 8 (`WeirdServerReply`), `Expected 0000 but got 0005`.
- `mqtt://h/` (empty topic): the CONNECT was sent, then exit 3 (`UrlMalformat`),
  `No MQTT topic found. Forgot to URL encode it?`.

The client identifier is random, so the handler takes its source as a constructor
parameter (for example a `Func<string>` producing the eight characters), defaulting to
`System.Security.Cryptography.RandomNumberGenerator`; tests inject a fixed value.

Publishing with `-d` and credentials from `-u` are the next task; this one ignores
`PostData` and `Credentials`.

## Acceptance criteria

- [ ] `MqttProtocolHandler(IConnector connector)` (plus the client-identifier source)
      implements `IProtocolHandler` with `SupportedSchemes` exactly `["mqtt", "mqtts"]`,
      and constructs no `Socket` or `SslStream`.
- [ ] Tests assert `mqtt://h/t` connects to `ConnectTarget("h", 1883, false)` and
      `mqtts://h/t` sets `UseTls` true; the `mqtts` default port is measured against
      curl 8.21.0, recorded in `Notes`, and pinned.
- [ ] A named test replays the first measured exchange against a scripted fake
      `IConnection` declared in `Curl.Protocol.Mqtt.UnitTests`, with the client
      identifier fixed to `curlPBadK4E3`, and asserts the exact CONNECT and SUBSCRIBE
      bytes, the exact `Output` bytes, and `CurlExitCode.RecvError` with
      `Connection disconnected`.
- [ ] A test asserts two PUBLISH packets arriving in one read are both written, in
      order, and one split across two reads is written once, whole.
- [ ] A test asserts a topic or payload long enough to need a two-byte remaining length
      (over 127 bytes) is encoded and decoded correctly.
- [ ] Tests pin the CONNACK return-code-5 case, the empty-topic case (CONNECT sent
      first), and `%2F` decoding, each with the measured code and message.
- [ ] Every test builds its context with `TransferContext`; no `ITransferContext`
      implementation is declared in the test project, and no test is tagged
      `Integration`.
- [ ] `Curl.Protocol.Mqtt.UnitLibrary/CLAUDE.md` names `IConnector` (ADR-0005) as the seam.
- [ ] `dotnet build Curl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

Do not edit `Documentation/Product/Requirements.md`; if an MQTT row there is wrong, file a
task.

## Log

- 2026-09-26: Created.
