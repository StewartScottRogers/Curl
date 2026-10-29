# ADR-1028 — --jsonrpc sends the calls named in one option value and pairs the replies by id

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4106.

## Context

BL-4106 adds `--jsonrpc <method> <params>` as the extension `Curl.Extension.JsonRpc.UnitLibrary`
(ADR-0999), with ids, batches and reply pairing. Upstream curl has no such option, so the
JSON-RPC 2.0 specification (https://www.jsonrpc.org/specification, dated 2010-03-26, revised
2013-01-04) decides. An extension option takes no value or exactly one (`ExtensionOptionArgument`,
ADR-1006), and the last value given wins, so the method, its params and a batch must all fit in
one value.

## Decision

- **Option.** `--jsonrpc <calls>`, one value: `<method> [params]`, the method name up to the first
  whitespace, `;`, `[` or `{`, then optionally its params as one JSON array or object (section 4.2).
  `--jsonrpc 'subtract [42,23]'` is one call. A `!` before the method name makes the call a
  notification, sent without an id (section 4.1).
- **Batches.** Calls separated by `;` are one batch (section 6), sent as a JSON array; one call is
  sent as one Request object. A `;` inside the params' JSON belongs to the JSON.
- **Ids.** The calls that are not notifications get the numbers 1, 2, 3 and so on, in order.
- **Transport.** On an HTTP transfer the extension's transfer hook (ADR-1007, ADR-1019) replaces
  the request body with the calls, an `application/json` POST in place of any `-d` data; `-X`, `-H`
  and every other option stay as given. A transfer that is not HTTP runs unchanged.
- **Reply pairing.** The response body is read whole and each Response object (section 5) is
  written on its own line, as the server sent it, in the order of the call whose id it carries;
  responses that answer no call (an error with a null id, an unknown id, a non-object element)
  follow in the order received. A body that is not a JSON object or array, such as the empty body
  a batch of notifications gets or an HTTP error page, is written unchanged.
- **Errors.** A value that names no call, a call with no method name, params that are not valid
  JSON, or anything but params, `;` or the end after a call fails the transfer with exit 2 and a
  message naming the call, before anything is sent. JSON-RPC errors in a response are output, not
  a transfer failure: the exit code is the transfer's.
- **Off.** With `CURL_EXTENSIONS=0` the option is not registered, so the parser refuses it as curl
  refuses an unknown option (exit 2); without the option the hook is never activated.
- **Registration.** One line each in `Curl.Extension.Composition`'s `ExtensionOptionCatalogue`,
  `ExtensionDescriptionCatalogue` and `ExtensionTransferHookCatalogue`. It adds no URL scheme, so
  `ExtensionCatalogue` (protocol handler factories) has no line for it.

## Left out, and why

- **Method names holding whitespace, `;`, `[` or `{`**, and a batch of one call: the one-value form
  cannot tell them from the separators. Such names are vanishingly rare; a batch of one is the same
  call as a single Request.
- **String or caller-chosen ids:** the specification lets the client pick any id; numbering in
  order is what pairing needs.
- **Transports other than HTTP** (raw TCP, WebSocket): the specification is transport agnostic,
  and those need a framing of their own; they can be added beside this one.
- **Synthesizing a reply for a call the server did not answer:** the output holds only what the
  server sent.

## Consequences

`curl --jsonrpc 'sum [1,2,4]; !notify_hello [7]; get_data' https://host/rpc` sends one batch and
prints the two replies in call order. The tests in `Curl.Extension.JsonRpc.UnitTests` check the
request and reply bytes against the specification's sections, with no network.
