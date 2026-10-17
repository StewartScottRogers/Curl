# ADR-0626 — CURLOPT_SSH_KEYFUNCTION's FINE_ADD_TO_FILE and FINE_REPLACE write the known-hosts file as libssh2 does

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2489.

## Context

ADR-0623 made `SshHostKeyChecker` ask `SshOptions.KnownHostDecision` (libcurl's `CURLOPT_SSH_KEYFUNCTION`) after the
known-hosts lookup and accept on all three `CURLKHSTAT_FINE*` answers, but it wrote no file.

In curl 8.22.0's `lib/vssh/libssh2.c`, `ssh_knownhost` does this after the callback answers:

- `CURLKHSTAT_FINE_REPLACE`: `libssh2_knownhost_del` of the entry `libssh2_knownhost_checkp` returned, if any, then as
  `FINE_ADD_TO_FILE`.
- `CURLKHSTAT_FINE`, `CURLKHSTAT_FINE_ADD_TO_FILE`: when the check was not a match, `libssh2_knownhost_add` of
  `conn->host.name` (plain, no port, no comment) with the raw key; on failure the info line
  `WARNING: adding the known host <host> failed`; else, for `FINE_ADD_TO_FILE` and `FINE_REPLACE` only,
  `libssh2_knownhost_writefile` of the whole in-memory list to `CURLOPT_SSH_KNOWNHOSTS`, and on failure
  `WARNING: writing <file> failed`. The key stays accepted either way.

The curl tool never sets `CURLOPT_SSH_KEYFUNCTION`; its default `sshkeycallback` answers only `FINE` or `REJECT`. So
no curl command line can produce a write, and real curl cannot be measured here without a C program built against
libcurl and libssh2, which this repository does not build.

## Decision

- `SshHostKeyChecker.Check` returns the known-hosts file to write back, or `null`. It returns one only for
  `AcceptAndAddToFile` and `AcceptAndReplaceInFile` on a lookup that is not a match. `KnownHostsFile.WithHostKey` makes
  it: every entry read, minus the lookup's entry (`KnownHostsLookup.Entry`) for a replace, plus the URL's plain host name
  with the host key's type and base64 last. Files are immutable, so the handler's loaded file is never changed.
- `SshProtocolHandler` writes it in its `SSH_HOSTKEY` step, before `SSH_AUTHLIST`, through its `IFileSystem`
  (`KnownHostsFile.WriteAsync`): truncate, create mode `0666` (libssh2's `fopen(.., "w")`), UTF-8 without a byte order
  mark. A refused open or an `IOException` on the write reports `WARNING: writing <path> failed` and carries on.
- The text follows libssh2 1.11.1's OpenSSH writer: one line per entry (so a comma-separated line comes back as one
  line per name, in libssh2's last-to-first order), `name type key [comment]`, a hashed name re-encoded as
  `|1|<salt>|<hash>`, an SSH-1 line as `name <rest of line>`, and an unknown type's name kept as written. Lines end
  with `Environment.NewLine`: CRLF from the Windows build's text-mode `fopen`, LF elsewhere. Entries after the first
  line libssh2 cannot parse are lost, as libssh2 never read them.
- `WARNING: adding the known host <host> failed` is not produced: `libssh2_knownhost_add` with a raw key of a
  recognized type fails only when memory runs out, and an unrecognized host-key type is refused before the callback.
- `CURLKHSTAT_FINE` on a mismatch adds nothing: its in-memory add lives on the connection's list, which no later
  check reads.

Taken from the source, not measured, for the reason in Context. A measurement through a libcurl program can replace
the source reading if one is ever built.

## Consequences

`Curl.Api` and `Libcurl` callers answering `CURLKHSTAT_FINE_ADD_TO_FILE` or `CURLKHSTAT_FINE_REPLACE` get the file
written as libcurl writes it; tests pin it against an in-memory `IFileSystem` (`KnownHostsFileTests.Write`,
`SshHostKeyCheckerTests.KnownHostsWrite`, `SshProtocolHandlerTests.KnownHostsWrite`).
