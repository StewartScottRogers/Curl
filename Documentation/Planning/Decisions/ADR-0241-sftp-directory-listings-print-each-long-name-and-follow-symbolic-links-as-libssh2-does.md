# ADR-0241 — SFTP directory listings print each long name and follow symbolic links as libssh2 does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-570.

## Context

curl lists a directory when an `sftp://` URL's path ends with a slash. What it sends,
what it prints for each entry, and what each failure reports had to be measured before
`Curl.Protocol.Ssh.UnitLibrary` could list one.

The Windows reference build (curl 8.21.0, libssh2 1.11.1, Schannel) was run with `-sS -k
--key <rsa key> -u <user>:` against OpenSSH 10.2 in WSL, the setup ADR-0220 describes:
once with OpenSSH's own `sftp-server` over a directory holding a file, a subdirectory, a
symbolic link and a broken one, and once with a scripted SFTP subsystem (a throwaway C#
app, not committed) that logged every request and answered by path.

| Case | Requests after `INIT`, `REALPATH .` | Exit | stdout / stderr |
| --- | --- | ---: | --- |
| directory (`sftp-server`) | `OPENDIR` path, `READDIR`, `READLINK` per link, `READDIR`, `CLOSE` | 0 | each long name and `\n`; a link's gains ` -> <target>`; 541 bytes by `%{size_download}` |
| same, `-l` or `--list-only` | no `READLINK` | 0 | each file name and `\n`, `.` and `..` included |
| empty directory | | 0 | the `..` and `.` lines |
| missing directory, or a file named with a slash | | 78 | `curl: (78) Could not open directory for reading: No such file or directory` |
| `-I` | | 0 | nothing |
| `sftp://h/~/bl570/list/`, `sftp://h/d/x%2F` | `OPENDIR <home>/bl570/list/`, `OPENDIR /d/x/` | 0 | |
| `OPENDIR` answered 1 / 3 / 4 | no `CLOSE` | 79 / 9 / 79 | `Could not open directory for reading: Unknown error in libssh2` / `Permission denied` / `Operation failed` |
| `OPENDIR` answered `OK`, then a handle | | 0 | listed |
| second `READDIR` answered 3 | `CLOSE` still sent | 9 | the first line, then `curl: (9) Could not open remote file for reading: Permission denied :: -31` |
| `READLINK` answered 2, or `NAME` with no names | `CLOSE` still sent | 27 | the lines before the link, then `curl: (27) Out of memory` |
| `READLINK` answered `OK` | | 0 | `LONG-l -> l`: the entry's own name |
| `READDIR` answered `NAME` with no names | `CLOSE` | 0 | nothing |
| an entry with no permissions attribute; one with an empty long name | no `READLINK` | 0 | `LONG-x\n\n` |
| an entry with size, owner, permissions (`S_IFLNK`) and times | `READLINK` | 0 | the link line |

## Decision

- **`Sftp.SftpRemotePath.NamesDirectory`** decides: a URL path whose percent-decoded bytes
  end with `/` is listed, any other downloaded. `SshProtocolHandler` sends the first to
  `Sftp.SftpDirectoryListing`.
- **`Sftp.SftpSession`** gains `OPENDIR` (a handle, with an `OK` status waited past as for
  `OPEN`), `READDIR` (the names of one `NAME` answer; `EOF` and an answer of no names end
  the listing; any other status fails) and `READLINK` (the first name; `OK` names no
  target). Each entry's attributes are read to their end - size, owner, permissions,
  times and extended pairs - so the next entry is found, and it is a symbolic link when
  its permissions' file type is `S_IFLNK`.
- **`Sftp.SftpDirectoryListing`** sends `REALPATH .`, resolves the path as a download
  does, stops there with `-I`, opens the directory and writes each entry as it arrives:
  the file name and `\n` with `-l`; otherwise the long name, then for a symbolic link
  ` -> ` and the target `READLINK` names for the directory's path and the file name, the
  entry's own name when `READLINK` answers `OK`, and `\n`. The long name, the link's file
  name and its target stop at a NUL byte, as curl's C strings do; `-l` writes the file
  name whole. Progress is reported after every line, with no size. It then closes the
  handle and the channel.
- **Outcomes:** a failed `OPENDIR` is its status's exit code and `Could not open directory
  for reading: <text>`; a failed `READDIR` is its status's exit code and `Could not open
  remote file for reading: <text> :: -31` (libssh2's `LIBSSH2_ERROR_SFTP_PROTOCOL`); a
  failed `READLINK` is exit 27 `Out of memory`; each after the lines so far, with the
  handle still closed. A connection that breaks during the listing is exit 79 `Error in
  the SSH layer` with the bytes so far, and before it exit 79 too, as ADR-0220 decided for
  downloads.
- `SftpSession.FinishIgnoringFailureAsync` (close the handle, then the channel) and
  `SshConnectionFailure.ReportAsSshLayerErrorAsync` are shared by the download and the
  listing.

## Consequences

- An entry name longer than curl's 1024-byte buffer is printed whole; libssh2 would fail
  it with `LIBSSH2_ERROR_BUFFER_TOO_SMALL`. Not measured: OpenSSH cannot make such a name.
- A `READDIR` answered `OK` is failed with `Unknown error in libssh2 :: -31`, exit 79, the
  table's fallback; not measured.
- The scripted subsystem's attempt to kill the connection during `READDIR` did not break
  it, so a broken connection there follows ADR-0220's decision rather than a measurement.
- Uploads (BL-571), quote commands (BL-572) and the `-v` lines (BL-578) build on these
  classes.
