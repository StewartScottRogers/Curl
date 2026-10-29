# ADR-0870 — Standard output is closed when descriptor 1 was not inherited

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3936
- Decided by Claude under Stewart's delegation.

## Context

On Linux `curl -sS file:///etc/hostname 1>&-` writes `curl: Failed writing body` and exits 23
with curl 8.18.0 (OpenSSL): its `main_checkfds` opens `/dev/null` read-only on the closed
descriptor 1, so the body write fails. Curl printed `curl: (2) Access to the path is denied.` and
exited 2, because the .NET runtime takes the free descriptor 1 for one of its own pipes and
`StandardOutputOpener` opened a `FileStream` over it (the same cause as ADR-0869's descriptor 2).

## Decision

Off Windows `StandardOutputOpener.ReadStandardOutputHandleValue` asks
`NativeInheritedFileDescriptor.IsInherited(1)` (ADR-0869) and answers `-1`, a closed standard
output, when descriptor 1 is closed or close-on-exec. A closed standard output is already a
`ClosedStandardOutputStream` whose writes throw, which `StandardOutputFailureDeferringStream`
reports as curl does. Windows is unchanged.

## Consequences

`1>&-` on Linux and macOS gives curl's standard error bytes and exit 23 (measured in WSL Ubuntu
against curl 8.18.0, BL-3936 Notes). A redirected or terminal standard output is inherited and
not close-on-exec, so it is opened as before.
