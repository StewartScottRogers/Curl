# ADR-0247 — SFTP quote commands run around the transfer as libssh2 does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-572.

## Context

`-Q`/`--quote` on an `sftp://` URL runs commands named after OpenSSH's `sftp` program:
`chgrp`, `chmod`, `chown`, `atime`, `mtime`, `ln`, `symlink`, `mkdir`, `rename`, `rmdir`,
`rm`, `statvfs` and `pwd`. A leading `-` runs a command after the transfer, and a leading `*`
lets it fail. What curl sends for each, where, and what each failure reports had to be
measured before `Curl.Protocol.Ssh.UnitLibrary` could run them.

The Windows reference build (curl 8.21.0, libssh2 1.11.1, Schannel) ran as `curl -sS -k --key
<rsa key> --pubkey <pub> -u <user>: -Q <command> sftp://localhost:2233/~/bl572/files/a.txt`
through `Record-CurlExchange.ps1 -NoServer`. It ran against OpenSSH 10.2 in WSL, with
`sftp-server -l DEBUG3` logging every request (the setup ADR-0220 describes), and `sshd`
at `DEBUG3` for the channel's teardown. For a 64-bit C `long`, Ubuntu's curl 8.18.0 (libssh2
1.11.1, OpenSSL) ran against the same server. Its quote code is the same as 8.21.0's, which
was read from `lib/vssh/libssh2.c` and `lib/vssh/vssh.c` at the `curl-8_21_0` tag.

| Command | Requests after `INIT`, `REALPATH .` | Exit | stdout / stderr |
| --- | --- | ---: | --- |
| `mkdir /f/nd` | `MKDIR` mode 0755, then the transfer's `OPEN` | 0 | the file / - |
| `rmdir /f/dd`, `rm /f/b.txt` | `RMDIR`, `REMOVE` | 0 | |
| `rename /f/b.txt /f/c.txt` | `RENAME` old, new (no flags: version 3) | 0 | |
| `ln /f/b.txt /f/l1`, `symlink ...` | `SYMLINK` with the two paths in the order given | 0 | |
| `chmod 640 /f/b.txt` | `SETSTAT` of the permissions alone | 0 | |
| `chown 1000 /f/b.txt`, `chgrp 1000 ...` | `STAT`, then `SETSTAT` of owner and group, one changed | 0 | |
| `atime "Thu, 02 Jan 2020 03:04:05 GMT" /f/b.txt`, `mtime ...` | `STAT`, then `SETSTAT` of both times, one changed | 0 | |
| `atime 2020-01-02T03:04:05Z /f/b.txt` | `STAT` | 21 | - / `incorrect date format for atime` |
| `mtime "1 Jan 2200" /f/b.txt` | `STAT` | 21 | - / `date overflow` (on Linux: sent as its low 32 bits, 2063-11-24) |
| `chmod 999`, `chmod 17777` | none | 21 | `Syntax error: chmod permissions not a number` |
| `chgrp abc /f/b.txt` | `STAT` | 21 | `Syntax error: chgrp gid not a number` |
| `*chgrp abc /f/b.txt` | `STAT`, `SETSTAT` of every attribute `STAT` gave | 0 | |
| `chown 1000 /f/zz` | `STAT` answered 2 | 21 | `Attempt to get SFTP stats failed: No such file or directory` |
| `*chown 1000 /f/zz` | `STAT` answered 2, `SETSTAT` owner 1000 group 0 answered 2 | 0 | the file |
| `chmod 644 /f/zz` | `SETSTAT` answered 2 | 21 | `Attempt to set SFTP stats for "/f/zz" failed: No such file or directory` |
| `chown 1000 ...`, `chgrp 0 ...` on Linux | `STAT` | 21 | `Syntax error: chown uid not a number` |
| `mkdir /f/dd` (exists) | `MKDIR` answered 4 | 21 | `mkdir "/f/dd" failed: Operation failed` |
| `rmdir`, `rm`, `rename`, `ln` failing | the request, answered 2 or 4 | 21 | `rmdir "<p>" failed: <text>`, `rm "<p>" failed: <text>`, `rename "<a>" to "<b>" failed: <text>`, `symlink "<a>" to "<b>" failed: <text>` |
| `*rm /f/zz` | `REMOVE` answered 2 | 0 | the file |
| `statvfs /f` with `-D -` | `EXTENDED statvfs@openssh.com` | 0 | `statvfs:\nf_bsize: 4096\n...f_namemax: 255\n` then the file |
| `statvfs /f/zz` | answered 2 | 21 | `statvfs "/f/zz" failed: No such file or directory` |
| `pwd` (or `PWD`) with `-D -` | none | 0 | `257 "/home/u/bl572/files/a.txt" is current directory.\n` then the file; nothing with `-i` or neither |
| `-pwd` with `-D -` | none | 0 | the file, then `257 "(nil)" is current directory.\n` |
| `-rm /f/zz` | the transfer, its `CLOSE`, then `REMOVE` answered 2 | 21 | the file / `rm "/f/zz" failed: ...` |
| `-mkdir /f/nd`, the download failing with 78 | the failed `OPEN`, no `MKDIR` | 78 | |
| `+rm /f/zz` | no `REMOVE` | 0 | the file |
| `foo bar`, `*foo bar` | none | 21 | `Unknown SFTP command` |
| `foo`, `rm`, `rm<TAB>/f` | none | 21 | `Syntax error command 'rm', missing parameter` |
| `rm `, `rm ""`, `rm "a\b"` | none | 21 | `Syntax error: Bad first parameter to 'rm '` |
| `chmod 644`, `ln /a`, `rename /a` | none | 21 | `Syntax error in chmod 644: Bad second parameter`, `Syntax error in ln/symlink: Bad second parameter`, `Syntax error in rename: Bad second parameter` |
| `rm /f/b more`, `rm "/f/b"x`, `rm /~/b c` | none | 21 | `Suspicious data after the command line` |
| `rm "/f/b c.txt"`, `rm '/f/b c.txt'` | `REMOVE "/f/b c.txt"` | 0 | |
| `rmdir /~/` | `RMDIR "/home/u/"` answered 3 | 21 | `rmdir "/home/u/" failed: Permission denied` |

The commands with no prefix run after `REALPATH .` and before the transfer's first request:
before the upload's `STAT` for `-C -`, the download's `OPEN`, and the listing's `OPENDIR`.
The `-` commands run after the handle's `CLOSE` and before the channel's `EOF` and `CLOSE`,
and only after a successful transfer. The channel is closed after a failed command too.

## Decision

`SftpQuoteCommands` runs the commands and `SftpQuoteCommand` reads each one, both ports of
curl's `sftp_quote`, `sftp_quote_stat` and `Curl_get_pathname`:

- A first `-` runs the rest after the transfer. A first `+` is dropped, because curl's SFTP
  never reads `CURLOPT_PREQUOTE`. Anything else runs before the transfer. A `*` after that
  lets a failure the server reports pass. It does not excuse a command that cannot be read,
  or a `chmod` mode that is not a number.
- The messages, exit 21 and the order of checks are curl's: the first argument is read
  before the name is looked at, so `foo ` reports a bad first parameter and `foo bar` an
  unknown command.
- Paths are UTF-8, quoted with `"` or `'` with `\"`, `\'` and `\\` escapes, or a word up to a
  space. An unquoted `/~/` becomes the home directory and a slash.
- `pwd` and `statvfs` write to `ITransferContext.HeaderOutput`, which is `-D`, as curl
  writes them as header data. A `pwd` after the transfer prints `(nil)`, because curl has
  freed the path by then. `statvfs` keeps only the read-only and no-set-uid bits of
  `f_flag`, as libssh2 does, and a status answer fails it even when it is OK.
- `chown` and `chgrp` read a decimal number up to `ULONG_MAX`. On Windows that is 32 bits.
  Where C `long` is 64 bits, `ULONG_MAX` is -1 to curl's parser, so every number fails.
  `atime` and `mtime` send the date's low 32 bits, and on Windows a date past them fails
  with `date overflow`. The handler passes `OperatingSystem.IsWindows()` as "C `long` is
  32 bits", so each platform matches its reference curl.
- `STAT` answered with status OK counts as empty attributes, and `SETSTAT` answered with
  attributes counts as success, as libssh2 takes them.
- A failure before the transfer throws, as a failed open does (ADR-0220): the channel is
  left for the handler's `DISCONNECT`. A failure after the transfer keeps the transfer's
  bytes and `%{size_upload}`, takes exit 21 and curl's message, and the channel is closed.
- An answer of another type, a `statvfs` answer cut short, or a broken connection is exit
  79, `Error in the SSH layer`. curl waits forever on a packet type it does not expect.
  A cut-short `statvfs` answer reports whatever status libssh2 kept last, which cannot be
  seen.

## Consequences

- Real curl closes the channel after a failed quote command or a failed open, before
  `DISCONNECT`. This library still leaves it open for either. BL-973 aligns both.
- The working path `pwd` prints is the one the transfer opens (`SftpRemotePath.Resolve`). A
  URL path of `/~` alone, which curl lists as the home directory, is BL-974's.
- `--ai-help` is unchanged: no option was added or changed.
