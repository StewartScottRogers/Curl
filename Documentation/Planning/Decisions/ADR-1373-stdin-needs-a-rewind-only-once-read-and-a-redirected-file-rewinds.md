# ADR-1373: Stdin needs a rewind only once read, and a file redirected into it rewinds

- Status: Accepted
- Date: 2026-11-24
- Task: BL-5910 (Surl Port-Board-Handoff entry 29, upstream test1072 and test1073)
- Amends: ADR-0524
- Decided by Claude under Stewart's delegation.

## Context

ADR-0524 made a chunked resend of `-T -` to an HTTP/1.0 server fail with exit 65 off Windows
whenever the wrapped standard input could not seek, and recorded a redirected file as a known
difference: .NET's console stream over descriptor 0 never seeks, so the port exited 65 where curl
exits 25. Two cases were wrong, measured with WSL curl 8.18.0 (OpenSSL) against `nc` answering
HTTP/1.0 401 (`--anyauth -u testuser:testpass`) or 301 (`-L`):

- The answer arrives inside the `Expect: 100-continue` wait, before any of stdin is read: curl
  needs no rewind, issues the resend and refuses it with exit 25. The port exited 65.
- A file is redirected into stdin: curl's `lseek` of descriptor 0 succeeds and curl exits 25, as
  upstream runtests (whose `<stdin>` is a file) expects. The port exited 65.

With a pipe already read (the answer after 2 s), curl prints `seek callback returned error 2` and
exits 65, as ADR-0524 says; that stays.

## Decision

`StandardInputUploadStream.CanRewind` is true while nothing has been read from it, when the wrapped
stream can seek, or when `lseek` of descriptor 0 succeeds. The last is probed once per stream: true
on Windows, whose C runtime seeks a pipe too; elsewhere a `FileStream` over descriptor 0 (not owned)
reports `CanSeek`, which .NET learns from the same `lseek`, and false when that fails or stdin is
closed. The probe is an `init` property so tests pin each platform's answer; its default is the one
platform adapter excluded from coverage (ADR-0083).

## Consequences

- A chunked `-T -` resend to an HTTP/1.0 server fails with exit 25 before any rewind check when the
  answer came before the body was read, on every platform.
- Off Windows a redirected file into stdin gives exit 25, matching curl; only a pipe already read
  gives 65.
- ADR-0524's "known difference" for a redirected file is gone.
