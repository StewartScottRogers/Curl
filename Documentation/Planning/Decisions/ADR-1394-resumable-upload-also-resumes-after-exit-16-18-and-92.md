# ADR-1394: Resumable upload also resumes after exit 16, 18 and 92

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation (BL-5965)

## Context

ADR-1379 sends each of `--resumable-upload`'s requests through curl's transfer and resumes after exit 52, 55
or 56. The extension's earlier HTTP/1.1 client reported every broken connection as 55 or 56. Curl's handler
usually negotiates HTTP/2 on `https`, where a reset stream exits 92, a connection closed before the final
response head or a framing failure exits 16, and a response cut off part-way exits 18, over HTTP/1.1 too.
The upload then gave up on the interruption the extension exists to survive.

## Decision

Exit 16, 18 and 92 are resumable as well as 52, 55 and 56: after one of them the flow asks the server for its
offset (`HEAD`) and appends from there, within the same limit on resumptions. This replaces ADR-1379's list.

- 92: the server reset the request's stream.
- 16: curl does not tell a closed connection apart from a framing or protocol error here, so both are
  retried. A server that breaks the protocol every time uses up the resumptions and the upload ends with 16.
- 18: the response did not arrive whole. The server's offset says how much of the upload it kept, so asking
  is safe even when it kept all of it. The other source of 18, `-C` on a file already uploaded, cannot arise:
  the extension's requests carry no `-C`.

## Consequences

- A cut-off response to the creation request creates the upload again, as a 56 there already does.
