---
id: BL-049
title: Implement MQTT publish with -d and CONNECT credentials
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-048]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-049 — Implement MQTT publish with -d and CONNECT credentials

## Goal

The MQTT handler publishes `ITransferContext.PostData` (`-d`) at QoS 0 instead of
subscribing, and puts `ITransferContext.Credentials` (`-u` or URL user information) into
the CONNECT, byte for byte as curl 8.21.0 does.

## Context

Builds on the MQTT subscribe handler from BL-048. Upstream: `curl -d payload
mqtt://host/topic` "sends an MQTT PUBLISH packet to the topic"; "Only QoS level 0 is
implemented for publish" and there is "No way to set retain flag for publish"
(<https://curl.se/docs/mqtt.html>, checked 2026-09-26). Protocol: MQTT 3.1.1 sections 3.1
(CONNECT flags and payload) and 3.3 (PUBLISH),
<https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html>.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24) against a
loopback listener that answered CONNACK `20 02 00 00`:

- `curl -d 75 mqtt://h/bedroom/dimmer` sent CONNECT (as in BL-048) then PUBLISH
  `30 12 00 0E "bedroom/dimmer" "75"`, wrote nothing to stdout, exit 0.
- `curl -u bob:secret -d x mqtt://h/t` sent CONNECT
  `10 25 00 04 "MQTT" 04 C2 00 3C 00 0C "curl"<8 chars> 00 03 "bob" 00 06 "secret"`,
  then PUBLISH `30 04 00 01 "t" "x"`, exit 0.
- `curl -u bob:se:cret -d x mqtt://h/t` sent `00 03 "bob" 00 07 "se:cret"`, then the
  PUBLISH, then `E0 00` (DISCONNECT). In the other publish captures the server closed
  first and no DISCONNECT was seen, so pin DISCONNECT for a server that stays open.
- `curl -d x mqtt://al:pw@h/t` sent flags `C2` with `00 02 "al" 00 02 "pw"`.

## Acceptance criteria

- [ ] A named test with `PostData` `75` and topic `bedroom/dimmer` asserts the exact
      PUBLISH bytes above, then `E0 00`, nothing written to `Output`, and exit 0.
- [ ] A named test with `Credentials` `bob`/`secret` asserts the exact CONNECT bytes above
      with the client identifier fixed, and a test with `se:cret` asserts a password
      containing a colon is sent whole.
- [ ] A test asserts a payload over 127 bytes gets a two-byte remaining length.
- [ ] A test asserts that with `PostData` set no SUBSCRIBE is sent, and one asserts a
      CONNACK with return code 5 still ends in exit 8 before any PUBLISH is sent.
- [ ] Every BL-048 test still passes; no test is tagged `Integration`.
- [ ] `dotnet build Curl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

Choosing between `-u` and the URL's user information is the command-line layer's job
(ADR-0006); the handler reads only `Credentials`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
