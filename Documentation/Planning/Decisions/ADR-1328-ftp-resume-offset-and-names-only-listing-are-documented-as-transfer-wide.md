# ADR-1328: CurlFtpOptions.ResumeFrom and ListOnly are documented as transfer-wide (BL-5668)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context

ADR-1325 put the resume offset (`-C`) and names-only listing (`-l`) on `CurlFtpOptions`. They are written into
the shared `EasyTransferOptions`, which the engine reads for every protocol: a resume offset sends
`Range: bytes=N-` on HTTP, as `curl -C N` does, and `ListOnly` reaches SFTP and SCP.

## Decision

Behaviour stays: it matches `curl -C` and `curl -l`, which are not FTP-only. Moving the two members to a new
transfer record would break ADR-1325's callers for no change in behaviour. Instead the doc comments and the
README say the two members apply to every protocol, that a request's `Ftp` record replaces the client's whole
(so `ResumeFrom` returns to 0 when a request sets only another member), and that `-1` on a download is no resume.
A unit test pins that an HTTP request on a client whose resume offset is set carries the offset.

## Consequences

Callers who want FTP-only resume set it on a request whose URL is FTP. Nothing else changes.
