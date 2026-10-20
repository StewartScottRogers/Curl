# ADR-0715: An SFTP download's remote time comes from the download's own STAT

- Status: Accepted
- Date: 2026-10-19
- Task: BL-2871
- Decided by Claude under Stewart's delegation.

## Context

`curl -R sftp://...` stamps the saved file with the time the server reports. curl's
`lib/vssh` takes it from the `mtime` of an `SSH_FXP_ATTRS` answer when its flags carry
`SSH_FILEXFER_ATTR_ACMODTIME`. Curl already sends one `STAT` per SFTP download, for the
size, and its answer carries the same attributes. The console stamps an `-o` file under
`-R` from `TransferResult.SourceLastWriteUnixSeconds`, and the engine reports
`%{filetime}` from it only when the file time was asked for.

## Decision

`SftpSession.StatSizeAndModifyTimeAsync` reads the size and the modification time from
the download's `STAT` answer, and `SftpFileDownload` puts the time in
`SourceLastWriteUnixSeconds`: the attributes' `mtime` when they carry `ACMODTIME`,
otherwise null (no flag, a status answer, or attributes cut short before the time).
No second `STAT` is sent under `-R`; the console and engine already decide whether the
time is used, and no upstream case checks the SFTP requests' count.

SCP is left as it is: libssh2's `libssh2_scp_recv2` runs `scp -pf` and reads the `T`
line's times into its `stat`, but curl's `ssh_state_scp_download_init` takes only
`st_size` from it and never sets the file time, so `-R` leaves an SCP download's time alone.

## Consequences

Upstream case 1446 gets the time `rofile.txt` carries. An SFTP download with no times in
its attributes keeps the time it was written, as curl does.
