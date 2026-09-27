# ADR-0057 — A `-T` upload over HTTP resumes from `-C` with curl's `Content-Range`

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-332, 2026-09-27).

## Context

ADR-0055 left `-C`/`--continue-at` with `-T` unhandled: the upload was sent from the stream's
position and `ITransferContext.ResumeFrom` was ignored. BL-332 makes the HTTP handler resume it.

Measured on 2026-09-27 with the Windows reference build (`/mingw64/bin/curl`, curl 8.21.0,
Schannel) against a loopback server that read the whole request and answered `200`. `f.txt`
holds the 10 bytes `abcdefghij`; the exact bytes are in the BL-332 Notes.

| Command | Request curl sent | Exit and message |
| --- | --- | --- |
| `-C 3 -T f.txt` | `PUT`, `Content-Range: bytes 3-9/10` after `Host`, `Content-Length: 7`, body `defghij` | 0 |
| `-C - -T f.txt` | no HEAD; `Content-Range: bytes 0-9/10`, `Content-Length: 10`, the whole file | 0 |
| `-C 0 -T f.txt` | no `Content-Range`, the whole file | 0 |
| `-C 10 -T f.txt`, `-C 20 -T f.txt` | connected, nothing sent | 18 `File already completely uploaded` |
| `-C 3 -T empty.txt` | connected, nothing sent | 26 `Unable to resume from offset 3` |
| `-C 3 -T f.txt -H "Content-Range: bytes 9-9/10"` | the `-H` line among the `-H` values, none of curl's own; body `defghij` | 0 |
| `printf abcdefghij \| curl -C 3 -T -` | `Content-Range: bytes 3-1/2`, chunked, the whole input | 0 |

## Decision

1. `HttpUploadResume` resumes the upload when `ResumeFrom` is above zero. A source that can seek
   is moved past the offset and sends `Content-Range: bytes N-(L-1)/L` for its length L, in the
   slot `Range` uses (after `Authorization`, before `User-Agent`), left out when an `-H` value
   names `Content-Range`.
2. An offset at or past a known length fails once connected with exit 18, and one into an empty
   source with exit 26, both before a byte is sent, as measured.
3. A source that cannot seek (standard input) is sent whole with curl's `Content-Range` for an
   unknown length counted as -1 (`bytes 3-1/2`). This is what the Windows reference build sends;
   it is the platform rule (root `CLAUDE.md`, "Decisions"), even though curl's library would
   read and discard the offset where a pipe truly refuses to seek.
4. `-C -` with `-T` is not handled here. The handler receives `ResumeFrom` `null` for it and
   cannot tell it from no `-C`; the contract change it needs is BL-351.

## Consequences

- `-C N -T file` over HTTP sends curl's bytes, and the two refusals match curl's exit codes and
  messages.
- A `-d`/`-F` body with `-r` or `-C` still sends no `Content-Range` (ADR-0044).
- A retried request (401, 417) re-sends from wherever the stream is, as before this change.

## Alternatives considered

- **Read and discard the offset from standard input**, as curl's library does when the seek
  callback reports it cannot seek. Rejected: it is not what the Windows reference build sends.
- **Seek the stream in `Curl.Console`** before handing it over. Rejected: the handler would then
  not know the offset for `Content-Range`, and the Console is outside this task.
