# ADR-0006 — The transfer context carries the Phase 4 protocol options

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

ADR-0003 extended `ITransferContext` with the options `file://`, HTTP and FTP share.
The Phase 4 protocols - DICT, Gopher, Telnet, TFTP and MQTT - need more, each checked
against curl 8.21.0 on 2026-09-26:

- `-d`/`--data` - "For MQTT, the data is sent as a PUBLISH"
  (<https://curl.se/docs/manpage.html#-d>; <https://curl.se/docs/mqtt.html>).
- `-u`/`--user` - measured: `curl -u bob:secret -d x mqtt://127.0.0.1:11883/t` sends a
  CONNECT whose flags byte is `0xC2`, followed by the user name `bob` and the password
  `secret`.
- `-t`/`--telnet-option` - `TTYPE`, `XDISPLOC` and `NEW_ENV`, as `<option=value>`
  (<https://curl.se/libcurl/c/CURLOPT_TELNETOPTIONS.html>). Measured: an unknown name is
  exit 48 and a value without `=` is exit 49, both reported at transfer time after the
  connection is made. Validation therefore belongs to the telnet handler, and the list
  travels to it unvalidated.
- `--tftp-blksize` - default 512, valid range 8-65464
  (<https://curl.se/libcurl/c/CURLOPT_TFTP_BLKSIZE.html>). Measured: 5 is sent as
  `blksize 8` and 70000 as `blksize 65464`, so the value is clamped, not refused.
- `--tftp-no-options` - suppresses the RFC 2347, 2348 and 2349 options
  (<https://curl.se/libcurl/c/CURLOPT_TFTP_NO_OPTIONS.html>).
- Telnet's input - "it sends what it reads on stdin" (<https://curl.se/docs/manpage.html>,
  TELNET) - is already carried by `ITransferContext.Upload`.

A second, structural problem: every protocol test project would otherwise declare its
own `ITransferContext` implementation, as
`Curl.Protocol.File.UnitTests/Fakes/FakeTransferContext.cs` does. Tasks on the board
add members to `ITransferContext` (BL-013, BL-020), and each addition would then break
every protocol test project - which parallel dark factory lanes cannot see coming,
because those projects sit outside the adding task's `touches`.

ADR-0003 is Accepted and so immutable (`Documentation/Planning/Decisions/README.md`);
this is a new decision alongside it, not an edit of it.

## Decision

`ITransferContext` gains exactly these members:

- `ReadOnlyMemory<byte>? PostData` - the `-d` data; `null` when not given.
- `System.Net.NetworkCredential? Credentials` - from `-u`, else from the URL's user
  information; `null` when neither is present.
- `IReadOnlyList<string> TelnetOptions` - each `-t` value verbatim, in command-line
  order; empty when none. The telnet handler validates them.
- `int? TftpBlockSize` - `--tftp-blksize` as given, unclamped; `null` when not given.
  The TFTP handler clamps it to 8-65464.
- `bool TftpNoOptions` - `--tftp-no-options`.

Telnet's input stays on the existing `Upload`; no member is added for it.

`TransferContext` is added to `Curl.Protocol.Abstractions.UnitLibrary`: a sealed class
implementing `ITransferContext` with `init` properties, where

- `Url` and `Output` are `required`;
- `TimeProvider` defaults to `TimeProvider.System`;
- every other member defaults to its "not given" value (`null`, `false`, an empty
  list, `CancellationToken.None`).

Protocol test projects build contexts with `TransferContext` instead of declaring
their own `ITransferContext` implementation. Adding a member to `ITransferContext`
then means adding it to `TransferContext` in the same project, and no test project
breaks.

## Consequences

Good:

- Each Phase 4 option is typed and nullable-checked, as ADR-0003's options are.
- Adding a context member is a one-project change, so contract tasks and protocol
  tasks can run in parallel lanes without breaking each other's builds.
- Where curl validates late (telnet options) or clamps (TFTP block size), the
  context carries the raw value and the handler reproduces curl's timing and exit code.

Costs and caveats:

- `ITransferContext` grows further, and most members mean nothing to most schemes;
  as ADR-0003 says, each protocol states which it ignores.
- Protocol-specific names (`TelnetOptions`, `TftpBlockSize`, `TftpNoOptions`) now sit
  on a shared contract.
- Existing test fakes such as `FakeTransferContext` must move to `TransferContext`;
  that migration is the protocol test projects' work, not this ADR's.

## Alternatives considered

- **An untyped option bag** (`IReadOnlyDictionary<string, object>`). Rejected, as it was
  by ADR-0003: it defeats nullable-reference-type checking and turns a compile-time
  error into a runtime one.
- **A per-protocol context interface** (for example `ITftpTransferContext :
  ITransferContext`). Rejected: the command-line layer would have to build a different
  context type per scheme and every handler would downcast to reach its options, which
  moves a type error to run time; and `-d` and `-u` are not specific to one protocol,
  so they would be duplicated across interfaces that then drift.
- **Keep one hand-written `ITransferContext` fake per test project.** Rejected: every
  member addition breaks every such project, outside the adding task's `touches`.

## Addendum (2026-09-27, BL-435): the FTP control options

Decided by Claude under Stewart's delegation.

Six FTP settings join the context on the same terms, parsed by `CommandLineOptionTable`
rows onto `CommandLineOptions` and copied by `Curl.Console`'s `TransferContextFactory`.
`ftp://` (BL-436) is to read them; nothing reads them yet.

| Option | Context member | Not given |
| --- | --- | --- |
| `--disable-epsv` / `--no-disable-epsv` | `bool FtpDisableEpsv` | `false` |
| `--ftp-skip-pasv-ip` / `--no-ftp-skip-pasv-ip` | `bool FtpSkipPasvIp` | `true`, curl 8.21.0's default (ADR-0093 relies on it) |
| `--ftp-method <multicwd\|nocwd\|singlecwd>` | `FtpFileMethod FtpFileMethod` | `FtpFileMethod.MultiCwd` |
| `--ftp-create-dirs` / `--no-ftp-create-dirs` | `bool FtpCreateDirectories` | `false` |
| `-l`, `--list-only` / `--no-list-only` | `bool ListOnly` | `false` |
| `-Q`, `--quote <command>` (repeatable) | `IReadOnlyList<string> QuoteCommands` | empty |

Measured with the local curl 8.21.0 (Windows, Schannel) on 2026-09-27 before pinning:

- `--ftp-method` reads its three values without regard to ASCII case. Any other value,
  empty included, is not refused: curl prints
  `Warning: unrecognized ftp file method '<value>', using default` (wrapped at 79
  columns, dropped under `-s`) and uses `multicwd`, even after an earlier valid
  `--ftp-method`. So the task's "refusal for a bad value" is a warning, and the parser
  records `MultiCwd`.
- `--ftp-method`, `-Q` and `--quote` as the last argument exit 2 with
  `curl: option <as typed>: requires parameter`; `-Q ''` is accepted.
- The four flags accept `--no-`; `--no-ftp-method` and `--no-quote` exit 2 as not reversible.

`-Q` values are carried verbatim and in order, `-`/`+`/`*` prefixes included; the
FTP handler interprets them, because which list a command joins (before or after the
transfer) and whether its failure is ignored is FTP conversation, not parsing.
`FtpFileMethod` is an enum in `Curl.Protocol.Abstractions` rather than curl's string
so the handler switches over a closed set.
