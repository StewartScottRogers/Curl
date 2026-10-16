# ADR-0529: The protocol command-line group maps onto EasyProtocolOptions

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2309 (ADR-0506 plan phase 1 step 4)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0506 moves the engine from the curl tool's `CommandLineOptions` to the `Curl.Api` option
groups. `Curl.Console`'s `EasyOptionsFromCommandLine` is the tool's `config2setopts.c`, one method
per group; BL-2309 adds the last one, the protocol group (`EasyProtocolOptions`, BL-2300). Its
engine readers (`EngineComposition`'s FTP handler factory, `RoutingFtpProtocolHandler`,
`MailRequestOptionsMapping`, `SshOptionsMapping`, `SshKnownHostsFileSearch`, `TransferPerformer`'s
SSH step and `TransferContextFactory`'s TFTP and TELNET fields) carry as many test call sites as
BL-2308's did, and the engine still reads `CommandLineOptions` for the groups BL-2343 to BL-2347
retarget, so the engine retarget is split, as BL-2303, BL-2307 and BL-2308 split theirs.

## Decision

1. `EasyOptionsFromCommandLine.ProtocolOf` builds `EasyProtocolOptions`, agreeing with the
   `CURLOPT_*` lines `--libcurl` writes (`LibcurlSourceCode.ProtocolOptions.cs`):
   - FTP: `-P` as `ActiveAddress`; `-Q` split as curl's tool splits it, a `+` command into
     `PreQuote` and a `-` one into `PostQuote` without the prefix, any other (a `*` one included,
     prefix kept) into `Quote`; `-a`; `--disable-eprt` and `--disable-epsv` as `UseEprt` and
     `UseEpsv` off; `--ftp-pret`; `--ftp-create-dirs` as `CreateAndRetry`, as `--libcurl` writes
     `2L`; `--ftp-alternative-to-user`; `--no-ftp-skip-pasv-ip`; `--ftp-ssl-ccc` and
     `--ftp-ssl-ccc-mode` as `ClearCommandChannel`; `--ftp-account`; `--ftp-method` as
     `FileMethod`, absent being `MultiCwd`, libcurl's default.
   - SSH: `--key` as `PrivateKeyFile` (it is also the TLS key, as the tool sets both), `--pubkey`,
     `--hostpubmd5`, `--hostpubsha256`, `--compressed-ssh` and `--knownhosts` as `KnownHostsFile`.
     The tool never sets `CURLOPT_SSH_AUTH_TYPES`, so `AuthenticationTypes` keeps libcurl's any.
     The file the tool finds without `--knownhosts` (`~/.ssh/known_hosts`) is per transfer, as
     ADR-0528 keeps `-C -` and the `-T` size, so `ProtocolOf` leaves it unset.
   - Mail: `--mail-from`, `--mail-rcpt` in order, `--mail-rcpt-allowfails` and `--mail-auth`.
   - TFTP: `--tftp-blksize` as given (the handler clamps it) and `--tftp-no-options`.
   - TELNET: the `-t` options in order.
   - RTSP and WebSocket: curl's tool sets none of `CURLOPT_RTSP_*` or `CURLOPT_WS_OPTIONS` (an RTSP
     request is `-X`, the transfer group's `CustomRequest`), so both records keep libcurl's defaults.
   - `-l`, `-B` and `--crlf` are the transfer group's (ADR-0528); `--ssl`, `--ssl-reqd` and
     `--ftp-ssl-control` the network group's `UseSsl` (ADR-0513).
2. The engine retarget is BL-2348; BL-2310 waits on it too. The engine stops reading
   `CommandLineOptions` only once BL-2343 to BL-2348 are all Done.

## Consequences

No engine class reads the record yet, so behaviour is unchanged. Every option group now has a
mapper tested option by option, so the remaining retarget tasks move readers without deciding
anything about the mapping.
