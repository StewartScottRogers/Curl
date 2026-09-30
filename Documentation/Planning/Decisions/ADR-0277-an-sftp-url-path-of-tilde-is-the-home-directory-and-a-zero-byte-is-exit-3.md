# ADR-0277 — An sftp:// URL path of /~ is the home directory, and a %00 in it is exit 3

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-974.

## Context

ADR-0220 resolved only a leading `/~/` against the home directory, always adding a slash
after it, so `sftp://host/~` asked the server for a file named `/~`, and a home directory
ending in `/` gained a second one. curl 8.21.0's `Curl_getworkingpath` (`lib/vssh/vssh.c`
at `curl-8_21_0`) decodes the path with `REJECT_ZERO`, then, for SFTP, turns `/~` into the
home directory and `/`, and `/~/rest` into the home directory followed by the path from
index 2 (`/rest`), or from index 3 (`rest`) when the home directory is empty or ends with
`/`. It runs after `REALPATH .` has named the home directory.

The Windows reference build (curl 8.21.0, libssh2 1.11.1, Schannel) was run on 2026-09-30
through `Record-CurlExchange.ps1 -NoServer` with `-sS -k --key <rsa key> --pubkey <pub> -u
<user>:` against OpenSSH 10.2 in WSL (the setup ADR-0247 describes; home
`/home/stewart_rogers`):

| URL path | Requests after `INIT` | stdout | stderr | Exit |
| --- | --- | --- | --- | ---: |
| `/~` | `REALPATH .`, `OPENDIR /home/stewart_rogers/` | the home directory's listing | empty | 0 |
| `/~` with `-D - -Q pwd` | the same | `257 "/home/stewart_rogers/" is current directory.`, then the listing | empty | 0 |
| `/~/` | the same | the home directory's listing | empty | 0 |
| `/~/bl572/files/a.txt` | `REALPATH .`, `OPEN .../a.txt` | `hello\n` | empty | 0 |
| `/~/bl572/files/a%00.txt` | `REALPATH .` | empty | `curl: (3) URL using bad/illegal format or missing URL` | 3 |
| `/~bl572` | `REALPATH .`, `OPEN /~bl572` | empty | `curl: (78) Could not open remote file for reading: No such file or directory` | 78 |

After the `%00` case `sshd` logged the channel's `EOF` and `CLOSE` before `DISCONNECT 11
Shutdown`, as after every other failure ADR-0274 measured.

## Decision

- `SftpRemotePath.Resolve` ports `Curl_getworkingpath`'s SFTP branch: `/~` becomes the
  home directory and `/`; `/~/rest` joins the home directory and `/rest`, or `rest` when
  the home directory is empty or ends with `/`; any other path is sent as it is.
- `SftpRemotePath.NamesDirectory` is true for `/~` as well as for a path ending with `/`:
  curl decides between listing and downloading on the resolved path, and `/~` always
  resolves to one ending with `/`.
- `SftpRemotePath.ResolveUrlPath` decodes, refuses a zero byte, and resolves. The refusal
  is `SshTransferException.UrlPathHoldsZeroByte`: exit 3, the exit code's own text (curl
  has no `failf` for it, so `-v` shows no extra line), thrown after `REALPATH` inside
  `CloseChannelOnFailureAsync`, so the channel is closed before `DISCONNECT` for a
  download, a listing and an upload alike.

## Consequences

- The home directory's case of a server whose `REALPATH .` answers `/` was not measured;
  it follows the source: `/~/f` is `/f`, and `/~` is `//`.
- `scp://` paths are unchanged (ADR-0225); their `%00` behaviour was not measured in this
  task.
