# ADR-0841: sftp and scp header lines never go to the body output

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3661)
- Relates to: [ADR-0779](ADR-0779-curlopt-nobody-keeps-header-lines-out-of-the-body-except-ftps.md), [ADR-0128](ADR-0128-the-websocket-library-writes-its-own-upgrade-request-and-reads-its-own-101-head.md), CGR-132 of the 2026-10-25 curl gap report in `Documentation/Reviews`

## Context

An `sftp` transfer's `-Q pwd` and `-Q statvfs` write their output (`257 "<path>" is current directory.`, the
`statvfs:` / `f_bsize: ...` lines) as header lines, as curl's `libssh2.c` does. `TransferContextFactory` sent
every scheme's header lines to the body output under `-i` and `-I` (WebSocket excepted), so Curl wrote them on
standard output ahead of the body.

Measured 2026-10-26 with curl 8.22.0 (Schannel, libssh2 1.11.1) against a throwaway OpenSSH 10.2p1 sshd in WSL:
`curl -sS -k -u user: --key k --pubkey k.pub -i -Q pwd sftp://127.0.0.1:21561/dir/hello.txt` writes only
`hello sftp\n` to standard output; with `-D file` the `257` line goes to the file. `-Q "statvfs /dir"` is the
same, `-I -Q pwd` writes nothing to standard output, and `scp://` with `-i` writes only the body. The curl tool
writes `-i` header lines to standard output only for the schemes its header callback names (HTTP, HTTPS, RTSP,
FILE), so the SSH schemes' lines never reach it.

## Decision

`TransferContextFactory.WritesHeaderLinesToBody` is false for an `sftp` or `scp` URL, whatever `-i`, `-I` or
`CURLOPT_NOBODY` say: their header lines go only to the `-D` output (or the header callback). Other schemes are
left as they are; each is changed only when measured.

## Consequences

`-i` and `-I` on an SSH URL write the same standard output as curl 8.22.0. Libcurl and `Curl.Api` handles with
`CURLOPT_HEADER` follow the same rule through the same factory, which matches the tool's output; whether
libcurl's own write callback gets the quote lines under `CURLOPT_HEADER` was not measured.
