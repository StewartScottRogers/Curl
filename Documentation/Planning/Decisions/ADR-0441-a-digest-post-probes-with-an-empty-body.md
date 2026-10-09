# ADR-0441: A Digest POST or PUT probes with an empty body first

- Status: Accepted
- Date: 2026-10-08
- Decided by Claude under Stewart's delegation.
- Task: BL-1799 (gap finding GF-0006)

## Context

curl 8.21.0 holds back a request body while a multi-pass scheme it has picked has not
been answered yet (`data->req.authneg` in `lib/http.c`): with `--digest` (or
`--proxy-digest`) and no challenge seen, the first POST or PUT goes with an empty body and
`Content-Length: 0`. Upstream test88, test175, test1001, test2058 and others expect this;
Curl sent the whole body on the first request.

## Decision

- When Digest is the one scheme allowed for the origin or the forward proxy, credentials
  are given, no value was made for the first request and the request has a body, the first
  request is framed by `HttpRequestFraming.AsAuthProbe`: empty body, `Content-Length: 0`,
  the body's `Content-Type` kept, no `Expect`.
- The answer to the challenge (401 or 407) is sent with the real body.
- A 2xx answer to the probe is followed by the same request with its body and no
  credentials, as curl re-requests the URL (test175). Any other answer ends the transfer.

## Consequences

- `--anyauth` and NTLM or Negotiate are unchanged; curl probes for single-scheme NTLM and
  Negotiate too, which is left to its own task if a gap run measures it.
- A user `-H "Content-Length: n"` still overrides the probe's length (test1284); filed as a
  follow-up task.
