# ADR-1064 — `--coap-observe` follows a CoAP resource and writes each notification, then a newline

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4248.

## Context

RFC 7641 lets a CoAP client register with a resource and receive a notification each time it changes.
Upstream curl has no CoAP, so the CoAP extension (ADR-0999, ADR-1047) needs its own option to ask for an
observation, a way to tell notifications apart in the output, and a way for the option to reach the
`coap://` handler, which an extension option cannot set on curl's transfer options.

## Decision

- **Option.** `--coap-observe`, a flag declared by `Curl.Extension.Coap.Observe.CoapObserveTransferHook` and
  registered in `ExtensionOptionCatalogue` and `ExtensionTransferHookCatalogue`. The name follows the
  extension's scheme, as `--graphql-*` and `--jsonrpc` do.
- **Reaching the handler.** The option activates a transfer hook (ADR-1007) that hands a `coap://` transfer to
  the next stage as a `CoapObserveContext`, a context that forwards every member of the transfer's own.
  `CoapProtocolHandler` recognises that type and observes; every other transfer passes through unchanged.
  This keeps `Curl.Protocol.Abstractions` free of a CoAP-only setting.
- **Registration.** The request carries Observe 0 (section 3.1). When the answer is not a 2.xx with an Observe
  option, the server did not register us, and the answer is written as a plain GET's would be (no newline).
- **Output.** Each notification is written as it arrives: its payload, then one newline (LF), flushed, so
  text payloads such as sensor readings come out one per line and a reader sees each at once. Under `-i`
  each is preceded by a `CoAP/1.0 c.dd` line and a blank line on the header output, as one response is.
  The newline is always written, whatever the payload ends with, so the output is predictable.
- **Ordering.** A notification is taken only when section 3.4 calls it newer than the last taken (sequence
  number ahead by less than 2^23, or more than 128 seconds later); an older one is dropped.
- **Answers.** A confirmable notification is acknowledged, taken or not (section 3.5). Any other confirmable or
  non-confirmable message, of another token or not a response, is answered with Reset (section 3.6).
- **End.** A notification that is not a 2.xx or carries no Observe option ends the observation with exit 0
  (section 3.2). Otherwise the observation runs until the transfer is cancelled, by `--max-time` or
  interruption; it then sends a non-confirmable GET with the registration's token and options and Observe 1
  (section 3.6, proactive cancellation) without waiting for its answer, and ends as a cancelled transfer does.
- **Not yet.** Re-registration when Max-Age runs out (section 3.3.1) and block-wise notifications
  (RFC 7959 section 2.6) are BL-4267's; until it lands, a notification's payload is written as one datagram carries it.
- **Help.** `--coap-observe` is listed by `curl --extensions` and `curl --ai-help extensions`, as every
  extension option is, and not by `curl --help all`: that page is upstream curl's (with only the Curl-only
  rows ADR-0224 and ADR-0485 already decided), and an extension never changes what curl prints (ADR-0999).

## Consequences

`CoapExchange` now starts its next receive only when the exchange goes on, so the datagram after a response
(the first notification) is left for the observation rather than taken and dropped.
