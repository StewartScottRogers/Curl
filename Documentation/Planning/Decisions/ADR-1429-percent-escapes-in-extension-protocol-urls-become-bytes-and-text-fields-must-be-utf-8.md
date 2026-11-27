# ADR-1429: Percent-escapes in extension protocol URLs become bytes, and text fields must be UTF-8

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6030)

## Context

The redis, mqtt5, nats, stomp, amqp (0-9-1), kafka and xmpp extensions decoded URL parts with
`Uri.UnescapeDataString`, which leaves an escape that is not valid UTF-8 undecoded. `DEL/k%E9` therefore
deleted the key literally named `k%E9`, and a publish went to a topic, subject or queue the URL did not
name, with exit 0 (fitness finding EXT-128).

## Decision

- `UrlPercentDecoding` in `Curl.Extension.Abstractions.UnitLibrary` is the one decoder: every `%XX` becomes
  its byte; a `%` without two hex digits and `+` stay as they are.
- A Redis argument is binary safe, so it is those bytes.
- A field that can only hold text (MQTT topic, NATS subject, STOMP destination, AMQP virtual host and queue,
  Kafka topic, XMPP address and query) is refused with exit 3 and a message naming the field when the bytes
  are not valid UTF-8, before anything is connected.
- An XMPP message body from the URL's query ends with exit 43, as the same bytes from `-d` or `-T` already do
  (ADR-1350).

## Why

Sending the escape literally acts on something the user did not name; sending U+FFFD would do the same.
Refusing matches what these extensions already do with a subject or body they cannot carry. One shared
decoder in the contract every extension already references avoids seven copies.
