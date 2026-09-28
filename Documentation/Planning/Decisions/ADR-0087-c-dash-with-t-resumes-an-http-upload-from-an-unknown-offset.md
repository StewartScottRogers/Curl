# ADR-0087 — `-C -` with `-T` resumes an HTTP upload from an unknown offset

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-351, 2026-09-27).

## Context

ADR-0057 left `-C -` with `-T` unhandled: `Curl.Console` resolves `-C -` only against an
output file, so the HTTP handler received `ResumeFrom` `null` (or the `-o` file's size) and
could not tell `-C -` from no `-C` or from `-C N`.

Measured on 2026-09-27 with the Windows reference build (`/mingw64/bin/curl`, curl 8.21.0,
Schannel) through `Record-CurlExchange.ps1`; `f.txt` holds `abcdefghij`, the exact bytes are
in the BL-351 Notes.

| Command | Request curl sent |
| --- | --- |
| `-C - -T f.txt` | `Content-Range: bytes 0-9/10`, `Content-Length: 10`, the whole file |
| `-C - -T f.txt -o out.txt` (3-byte `out.txt`) | the same: `-o` plays no part |
| `-C - -T empty.txt` | `Content-Range: bytes 0--1/0`, `Content-Length: 0`, exit 0 |
| `-C - -T -` | `Content-Range: bytes 0--2/-1`, chunked |

Every run also wrote `** Resuming transfer from byte position -1` to standard error: curl's
tool turns an upload's `-C -` into offset -1, which its library sends as the whole source from 0.

## Decision

1. `ITransferContext` gains `ResumeUploadFromUnknownOffset`, `true` for `-C -` with a `-T`
   source. `TransferContextFactory` sets it; it leaves `ResumeFrom` as it resolved it.
2. `HttpUploadResume` honours it before `ResumeFrom`: the whole source, from its position, with
   `Content-Range: bytes 0-(L-1)/L`, L counted as -1 when unknown. It never fails, even for an
   empty source.
3. Other handlers ignore the flag and behave as before.

## Consequences

- `-C - -T file` over HTTP sends curl's bytes; `-C 0 -T file` still sends no `Content-Range`.
- The `** Resuming transfer from byte position -1` line and a redirected hop carrying the flag
  are follow-up work, filed from BL-351.

## Alternatives considered

- **Pass offset -1 in `ResumeFrom`**, as curl does internally. Rejected: `ResumeFrom` is
  documented as an offset, and the file handler refuses a negative one.
- **Pass offset 0 and a separate "was dash" flag only to HTTP.** Rejected: the flag is the
  contract fact; which handler uses it is each handler's business.
