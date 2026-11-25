# ADR-1399: Resumable upload also resumes after exit 95

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation (BL-5969)

## Context

ADR-1398 made the interruptions of HTTP/1.1 and HTTP/2 resumable and did not cover HTTP/3. Over `--http3`,
curl's handler ends a request with exit 95 when the server resets the request stream before any response body
byte, when the stream closes before the final response head, and when a response head has no valid `:status`
(ADR-0144, ADR-0172). After body bytes a reset exits 18, which already resumes. So `--resumable-upload` over
HTTP/3 gave up on the interruption it exists to survive.

## Decision

Exit 95 is resumable as well as 16, 18, 52, 55, 56 and 92: after it the flow asks the server for its offset
(`HEAD`) and appends from there, within the same limit on resumptions. This extends ADR-1398's list.

- A reset or early close is HTTP/3's counterpart of exit 92 and of exit 16's early close, both resumable.
- A response head with no valid `:status` exits 95 too, and the exit code does not tell it apart. As with 16, a
  server that breaks the protocol every time uses up the resumptions and the upload ends with 95.
- Asking the server for its offset is safe whatever it kept of the upload.

## Consequences

- A 95 on the creation request creates the upload again, as a 56 there already does.
