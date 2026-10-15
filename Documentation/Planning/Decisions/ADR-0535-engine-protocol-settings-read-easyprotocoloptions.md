# ADR-0535 — The engine's FTP, SSH, mail, TFTP and TELNET settings read `EasyProtocolOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2348.

## Context

ADR-0529 built `EasyOptionsFromCommandLine.ProtocolOf`. BL-2348 has `TransferContextFactory`'s FTP, TFTP and
TELNET fields, `MailRequestOptionsMapping`'s sender and recipients, `SshOptionsMapping` and
`TransferPerformer`'s SSH step read it in place of `CommandLineOptions`, with behaviour unchanged. Three
settings do not carry over one to one: the record splits `-Q` into libcurl's three lists, holds
`--tftp-blksize` as libcurl's 512 default rather than "not given", and holds `--ftp-method` and
`--ftp-create-dirs` in libcurl's numbers. `EngineComposition.CreateFtpProtocolHandler`,
`RoutingFtpProtocolHandler` and `SshKnownHostsFileSearch` read no command-line setting at all, so they
needed no change.

## Decision

- **`-Q` is rejoined by prefix.** `TransferContext.QuoteCommands` is `Quote` as it is, then `PreQuote` each
  behind `+` and `PostQuote` each behind `-`. The FTP and SFTP handlers sort the list by that prefix, so each
  stage keeps its command-line order; only the order across stages, which no handler reads, changes.
- **A block size of 512 is "not given".** The context's `TftpBlockSize` is `null` for libcurl's default, as it
  was without `--tftp-blksize`; the TFTP handler sends 512 for `null`, so `--tftp-blksize 512` sends the same
  bytes as before.
- **libcurl's FTP numbers map back.** `FtpFileMethod.Default` and `MultiCwd` are the handler's `MultiCwd`;
  any `CreateMissingDirectories` other than `None` creates directories.
- **`--pass`, `-k`, `-s` and `-S` stay on `CommandLineOptions`** for the SSH step: they are the TLS and
  behaviour groups', whose tasks retarget them. `SshOptionsMapping.Of` takes the passphrase as a value.

## Consequences

The engine reads `-P`, `-Q`, `-a`, the `--ftp-*` settings other than `--ftp-ssl-control`,
`--disable-epsv`/`--disable-eprt`, the `--mail-*` sender, recipients, `AUTH` and allow-fails, the SSH keys,
fingerprints, compression and `--knownhosts`, `--tftp-blksize`, `--tftp-no-options` and `-t` from the record.
The default known-hosts search stays the tool's, per transfer (ADR-0529).
