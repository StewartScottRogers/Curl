# ADR-0248 — The console finds the SSH known-hosts file as curl's tool does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-576.

## Context

ADR-0122 leaves it to the console, not the SSH handler, to resolve the default
`~/.ssh/known_hosts` and to fail with `curl: Could not find a known_hosts file` and exit 2
when it is missing. It did not say where "default" is, what `--hostpubmd5` and
`--hostpubsha256` change, or what happens to the URLs after the failing one. Measured on
2026-09-29 with curl 8.21.0 (mingw, Schannel, libssh2 1.11.1) against `sftp://127.0.0.1:1/x`,
with each environment variable pointed at its own directory holding `.ssh/known_hosts`
(BL-576 Notes):

- `CURL_HOME`, `HOME`, `USERPROFILE` and `APPDATA` are each searched; `XDG_CONFIG_HOME` is not.
  With `CURL_HOME` empty of the file and `HOME` holding it, `HOME`'s is used.
- With none found and no `-k`: `curl: Could not find a known_hosts file`,
  `curl: (2) Failed initialization`, exit 2, no connection. `-s` hides both lines.
- With none found and `--hostpubmd5` or `--hostpubsha256`: `Warning: Could not find a
  known_hosts file`, and the transfer goes on.
- `http://… sftp://…` transfers the first and then fails; `sftp://… http://…` fails and
  transfers nothing more.

## Decision

- `SshKnownHostsFileSearch` lists curl's `findfile(".ssh/known_hosts", FALSE)` candidates:
  `CURL_HOME`, `HOME`, then on Windows `USERPROFILE`, `APPDATA` and `USERPROFILE\Application
  Data`, then off Windows the account's home directory (`getpwuid`, .NET's user profile
  folder), each joined with curl's `DIR_CHAR`. The first one the runner's data-file reader
  can read is the file.
- `-k` means no known-hosts file whatever `--knownhosts` says; otherwise `--knownhosts` wins
  over the search.
- The runner resolves this for every `scp` and `sftp` transfer after the proxy and
  credentials are chosen, and a missing file without a fingerprint is a run-ending failure.
- `SshOptionsMapping` copies `--key`, `--pubkey`, `--pass`, `--hostpubmd5`, `--hostpubsha256`
  and `--compressed-ssh` verbatim into `SshOptions`.

## Consequences

- `curl sftp://…` and `curl scp://…` run through `SshProtocolHandler`, registered in
  `CurlComposition.CreateProtocolHandlers`, and `-V` lists `scp` and `sftp`.
- The Linux and macOS search order follows curl's source; only the Windows order was measured.
