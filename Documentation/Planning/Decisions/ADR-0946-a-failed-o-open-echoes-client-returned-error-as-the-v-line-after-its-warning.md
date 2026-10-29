# ADR-0946: A failed `-o` open echoes `client returned ERROR on write of N bytes` as the `-v` line, after its warning

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3951, CGR-088)

## Context

Measured 2026-10-28 against curl 8.22.0 (Schannel), `-v -o nodir/x.txt file:///.../s.txt` (12 bytes):
standard error held the meter's data line, `Warning: Failed to open the file nodir/x.txt: No such file or directory`,
`* client returned ERROR on write of 12 bytes`, an empty line (the meter's end), `* closing connection #0` and
`curl: (23) client returned ERROR on write of 12 bytes`. Curl printed the handler's
`* Failure writing output to destination, passed 12 returned 0` before the warning, and no
`* client returned ERROR` line.

## Decision

`OutputFileOpenFailureTransferEvents` wraps the transfer's events under `-v` and the `--trace` options. When the
handler reports its write-failure info line and the `-o` file's open has failed, it writes the pending
warning to the event stream (unless `-s`), then reports curl's `client returned ERROR on write of N bytes`, N
being the size of the write the file refused. The warning is taken from the file, so the end of the
transfer does not print it again. Only the event layer changes: no protocol handler is touched.
Without `-v` or a trace nothing is wrapped and the warning stays where the progress meter puts it.
