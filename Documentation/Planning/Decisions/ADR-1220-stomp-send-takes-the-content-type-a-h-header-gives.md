# ADR-1220: STOMP SEND takes the content-type a -H header gives

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4610)

## Context

ADR-1209 gives every `SEND` frame `content-type:application/octet-stream` and leaves the user's `-H` unread. STOMP is
an extension (ADR-0999): there is no upstream curl behaviour to match.

## Decision

- A `-H "content-type: <type>"` (name matched without regard to case) replaces the default on the `SEND` frame; the
  frame still carries exactly one `content-type`. When several are given, the last wins, as for a repeated setting.
- A `-H` whose value is empty (`content-type:` or `content-type;`) is ignored and the default stays.
- Without such a `-H` the default of ADR-1209 stands. Other `-H` headers are still not sent on the frame.

## Consequences

`StompProtocolHandler.ContentTypeOf` reads `HttpRequestOptions.Headers`; `StompClientFlow.SendFrame` takes the type.
