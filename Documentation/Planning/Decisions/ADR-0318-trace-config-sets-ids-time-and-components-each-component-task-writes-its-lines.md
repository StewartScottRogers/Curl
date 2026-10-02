# ADR-0318 — `--trace-config` sets ids, time and the components; each component's own task writes its lines

- **Status:** Accepted
- **Date:** 2026-10-01
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-649: curl 8.21.0's `--trace-config <list>` takes a comma list of `ids`, `time`, `all` and libcurl's
trace component names, each optionally prefixed `-` (off) or `+`. Measured on 2026-10-01 with the
reference Schannel build (BL-649 Notes):

- The list splits at commas; a name after a comma may start with blanks (`ids, time` works), but a
  leading or trailing blank on the first name, a blank after `+`, a space or a `;` as separator all
  make an unknown name. Names are case-insensitive. Empty names and the empty value are ignored.
- Unknown names (`bogus`) are ignored without a warning; the exit code is unchanged. `--no-trace-config`
  is refused as an option that cannot be reversed.
- `ids` and `time` work like `--trace-ids` and `--trace-time`, except that a first `-v` does not clear
  them (`--trace-config ids -v` shows `[0-0]`, `--trace-ids -v` does not); `--no-verbose`,
  `--no-trace-ids` / `--no-trace-time` and `-ids` / `-time` do. `all` turns both on and every component.
- `tls` and `http/1` write nothing beyond `-v` on the Schannel build, over HTTP or HTTPS: Schannel's
  filter and the HTTP/1 handler have no trace lines in it. `dns` writes ten `[DNS]` filter lines around the
  connect; `all` writes the whole transfer engine (`[MULTI]`, `[SETUP]`, `[HAPPY-EYEBALLS]`, `[TCP]`,
  `[READ]`, `[WRITE]`, `[TIMER]`, `[PGRS-*]`), with fd numbers and nanosecond durations.
- `-vv`, `-vvv` and `-vvvv` turn components on too (curl's manual: `protocol`, then `read,write,ssl`,
  then `network`); `-vv` already writes `[SETUP]` lines.

## Decision

- `Curl.Cli` parses the list as measured. `ids` and `time` set the same `TraceIds` / `TraceTime` the
  options set, plus a sticky flag a first `-v` leaves alone; every other name is kept, lower case, in
  `CommandLineOptions.TraceComponents` (unknown names too, since curl keeps quiet about them).
- Each component's lines are written by the code that owns that component, in the task below, not by a
  central formatter. Nothing is recorded as "cannot".

| Component (curl 8.21.0) | Lines on the reference build | Written by |
| --- | --- | --- |
| `ids`, `time`, `all` (ids and time) | `[x-y] `, `HH:MM:SS.ffffff ` prefixes | BL-649 (this) |
| `tls`, `ssl` (Schannel) | none beyond `-v` | BL-649: pinned as none |
| `http/1` | none beyond `-v` | BL-649: pinned as none |
| `dns`, `doh` | `[DNS]` filter lines; DoH `[DNS]` / `[DoH]` lines (BL-850) | BL-1102 |
| `setup`, `happy-eyeballs`, `tcp`, `udp`, `unix`, `tcp-accept`, `multi`, `read`, `write`, `timer`, `ssls`, `socks`, `http-proxy`, `h1-proxy`, `h2-proxy`, `haproxy`, `https-connect`, `network`; the components of `-vv` to `-vvvv` | connection-filter and transfer-engine lines | BL-1103 |
| `http/2`, `http/3`, `quic`, `ssh`, `ftp`, `smtp`, `imap`, `pop3`, `ws`, `protocol` | each protocol's own lines; `imap` and `pop3` none (measured) | split by BL-1104: `ftp` BL-1162, `smtp` BL-1163, `ws` BL-1164, `imap` and `pop3` pinned as none BL-1165, `ssh` BL-1166, `http/2` BL-1167, `http/3` BL-1168, `quic` BL-1169; `protocol` turns on each protocol's lines (measured for `ftp`) |
| any other name | none: ignored as curl ignores it | BL-649 |

## Consequences

- `--trace-config` is accepted everywhere it is in curl, and `ids`, `time` and `all`'s prefixes match.
- Until BL-1102 to BL-1104 are done, components other than `tls` and `http/1` are parsed and kept but
  write no lines, so `dns` and `all` output is shorter than curl's.

## Alternatives considered

- Treat `--trace-config ids` as `--trace-ids`: lost, because a first `-v` clears one and not the other.
- Refuse or warn on unknown names: lost, curl does neither.
- Write every component's lines from one formatter in `Curl.Console`: lost, the lines describe each
  library's own steps and only that library knows when they happen.
