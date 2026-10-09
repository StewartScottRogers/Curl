# ADR-0440: An authentication retry rewinds a seekable upload

- Status: Accepted
- Date: 2026-10-08
- Task: BL-1798 (gap finding GF-0005)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0034 made a 401 (and later a 407, ADR-0239) to a request whose body is a stream the
result, because the stream had been read by the time the challenge arrived. Upstream curl
8.21.0's own cases say otherwise for a stream that can seek: test1030 (`--anyauth -T file`)
and test259 (`--anyauth -F`) expect the PUT or POST sent again, whole, with the picked
scheme's credentials; test154, test155, test258, test1071 and test1075 have the same shape.
curl rewinds its client reader (`cr_in_rewind`) before the resend, and fails only when the
reader cannot seek, as stdin cannot.

## Decision

`HttpProtocolHandler.MayRetry` answers a 401 or 407 for a body of bytes, no body, or a
stream body that can seek. Before the retry, `RewindForResend` seeks the stream back to
where the request's `HttpRequestBodyWriter` began reading it. A `-T` file is a seekable
`FileStream`; a `-F` form is a `ConcatenatedReadStream`, seekable when every part is. A
stream that cannot seek (stdin, a rate-limited upload) still leaves the 401 or 407 as the
result, as ADR-0034 decided.

## Consequences

- `--anyauth`, `--proxy-anyauth` and a scheme picked up front all resend a `-T` file or
  `-F` form with its credentials.
- ADR-0034's rule now holds only for an unseekable stream body.
