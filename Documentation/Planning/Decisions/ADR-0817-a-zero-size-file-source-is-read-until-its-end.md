# ADR-0817: A `file://` source that reports size zero is read until its end

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3557
- Decided by Claude under Stewart's delegation.

## Context

`FileProtocolHandler` copied exactly the size the open reported. On Linux a procfs or sysfs
file, a FIFO, `/dev/stdin` from a pipe and `/dev/zero` report size zero (`PhysicalFileSystem`
also reports zero for any stream that cannot seek), so Curl wrote nothing and exited 0
(third-party gap report CGR-028). curl 8.18.0 (OpenSSL, Linux), measured on 2026-10-26, reads
such a source until end of file: `file:///proc/version` prints the kernel version, `-r 0-9`
of `/dev/zero` writes ten bytes, `-r -1` of a pipe its first byte, and `--max-filesize 100`
of `/dev/zero` writes 100 bytes and exits 63. Its `file_do` treats an expected size of zero as
unknown and then bounds the read only by the range's byte count (`maxdownload`) when that is
positive.

## Decision

When the open reports a size of zero, `DownloadBodyAsync` copies until the stream ends,
unless `-r` names a positive byte count: a bounded range's span or a suffix's length. `-C`
wins over `-r` as it does elsewhere, so a resume reads until the end. Every other step - the
headers (`Content-Length: 0`), the exit 36 checks against the zero size, `--max-filesize` -
is unchanged.

## Consequences

An empty regular file still writes nothing, because its stream ends at once. A source that
never ends (`/dev/zero` with no range or limit) streams until `-m` or the output stops it, as
curl does.
