# ADR-0484: An MQTT topic decoded to a control byte is exit 3 before SUBSCRIBE or PUBLISH

- Status: Accepted
- Date: 2026-10-13
- Decided by Claude under Stewart's delegation (BL-2168)

## Context

Gap finding GF-0076: upstream test 2109 in the curl 8.22.0 release ("MQTT rejects a
control byte in the topic") expects `mqtt://host/aa%00bb` to send CONNECT, read CONNACK
and fail with exit 3 before any SUBSCRIBE. Curl subscribed to `aa\0bb`.

Measured on 2026-10-13 against the local reference `curl.exe` (8.22.0, Schannel, a
Debug-enabled build) with a loopback listener that answered CONNACK `20 02 00 00`:
`aa%00bb`, `aa%0Abb` and `aa%1Fbb` were all subscribed (`82 0A 00 01 00 05 61 61 00 62
62 00` for the NUL), so that binary does not pass the release's own test 2109.

## Decision

Follow the release's test, not the local binary: the targeted version's `tests/data`
cases are the gap office's yardstick (ADR-0433), and test 2109 states the release's
intended behaviour. `MqttTopic.Decode` rejects a decoded topic holding any byte from
0x00 to 0x1F - what curl's `Curl_urldecode` rejects under `REJECT_CTRL` - with exit 3
(`CurlExitCode.UrlMalformat`), after CONNACK and before SUBSCRIBE or PUBLISH. Because
`Curl_urldecode` fails without `failf`, the message is `curl_easy_strerror`'s
`URL rejected: Malformed input to a URL function`, and `-v` shows no line for it.

## Consequences

- `behaviour:test2109` measures `match`.
- A topic with 0x7F or 0x20 is still subscribed; only 0x00 to 0x1F are rejected.
- If a later reference build is measured subscribing to a control byte on purpose, this
  ADR is revisited with that measurement.
