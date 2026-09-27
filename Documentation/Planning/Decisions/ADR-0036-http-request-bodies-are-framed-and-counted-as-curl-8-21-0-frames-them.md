# ADR-0036 — HTTP request bodies are framed and counted as curl 8.21.0 frames them

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-175 taught `Curl.Protocol.Http.UnitLibrary` to send request bodies from `BytesBody`
and `StreamBody`: body headers, chunked framing, `Expect: 100-continue` and its wait. Four
points had more than one reasonable answer. Each was settled by measuring the reference
build (curl 8.21.0, mingw, Schannel) on 2026-09-26 against loopback servers on 127.0.0.1;
the full commands and captured bytes are in BL-175's Notes.

- `curl --json {"a":1} http://127.0.0.1:18081/` sent, after `User-Agent`,
  `Content-Type: application/json`, `Accept: application/json`, `Content-Length: 7`. With
  `-H 'X-A: b'` before or after `--json`, `X-A: b` came first. With `-H 'Accept: foo'`,
  only `Content-Type: application/json` was added.
- `curl -X POST -T -` with `hello world` on a pipe sent the body chunked. `curl -F f=@locked`,
  a 100000-byte file whose every read fails, sent `Content-Length: 100207`, then only the
  207 framing bytes, and exited 26 with
  `curl: (26) client mime read EOF fail, only 207/100207 of needed bytes read`: curl's
  reader takes a failed read as end of file.
- A server answering `HTTP/1.1 401 No` (body `no!`) as soon as a head carrying
  `Expect: 100-continue` arrived: curl sent no body byte, printed `no!`, exited 0. Without
  a reply the body followed about 1 second after the head.
- `curl -d x=1 -w '%{size_request} %{size_upload}'` printed `151 3`: the 148-byte head plus
  the 3-byte body. `-X PUT -d x=1` printed `150 3`.

## Decision

1. **`--json` is a `BytesBody` with Content-Type `application/json`, plus two appended
   headers.** `Content-Type: application/json` and `Accept: application/json` are appended
   after every `-H`, each only when no `-H` names that header. That is the measured order,
   and it leaves the handler needing no `--json` flag of its own.
2. **A failed read ends a body of unknown length and fails one of known length.** A
   `StreamBody` of unknown length whose read fails ends the chunked body there (the
   terminating `0` chunk is sent), as curl's reader treats a failed read as end of file.
   One of known length fails with exit 26 and
   `client mime read EOF fail, only N/M of needed bytes read`; a stream that simply ends
   early gets the same, as in curl.
3. **A final status during the `100 Continue` wait is the response.** When a final status
   line arrives within the 1-second wait, the body is left unsent and that response is
   written as the transfer's result. The 417 retry without `Expect` is BL-260.
4. **`TransferReport.RequestSize` counts the head and the body bytes sent**, because
   `%{size_request}` does (151 for `-d x=1`).

## Consequences

Request bytes, exit codes and `%{size_request}` match the reference build for the measured
cases. A 417 answer is not yet retried (BL-260). The wait reads only up to the first line
feed to tell 100 from a final status, with its buffer capped at the 100 KiB line limit.

## Alternatives considered

- **`--json` as a handler flag that adds its headers itself.** Puts CLI knowledge into the
  protocol library and still has to reproduce the after-every-`-H` order; the plain
  `BytesBody` plus appended headers gives the same bytes with nothing new in the contract.
- **`--json` headers placed where curl's own `-d` Content-Type goes (before `-H`).**
  Measured wrong: curl puts them after every `-H`.
- **Fail a chunked body on a read error (exit 26 or 23).** curl does not: it ends the body
  and the transfer carries on, so a script would see a different exit code.
- **Pad or abandon a known-length body silently.** Sends bytes curl does not, or hides the
  exit 26 curl reports.
- **Send the body anyway after an early final status.** Measured wrong: curl sends no body
  byte and uses the early reply.
- **Keep `RequestSize` to the head only.** `%{size_request}` would print 148 where curl
  prints 151.
