# ADR-0513 — Mapping the command line's network group onto `EasyNetworkOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2303.

## Context

ADR-0506 makes the engine's input the CLI-neutral easy option groups of `Curl.Api.UnitLibrary`,
and has `Curl.Console/EasyOptionsFromCommandLine.cs` map a parsed command line onto them as the
curl tool's `config2setopts.c` does. BL-2303 adds its first group, `EasyNetworkOptions`. A few
command-line settings had no member, one default could not tell "not set" from a value, and one
mapping in `--libcurl`'s output disagrees with the command line's behaviour.

## Decision

- `EasyNetworkOptions.ConnectTimeout` defaults to zero, libcurl's stored default, which libcurl
  takes as 300 seconds. With a 300-second default the engine could not tell `--connect-timeout 300`
  from no `--connect-timeout`, and curl sets its `CONNECTTIMEOUT` timer (the `[TIMER]` and
  `timeouts=` trace lines) only for a set value. The mapping sets it only for a positive value,
  as `--libcurl` writes `CURLOPT_CONNECTTIMEOUT_MS` only then.
- `--ip-tos`, `--vlan-priority` and `--mptcp` become `IpTypeOfService`, `VlanPriority` and
  `MultipathTcp` on `EasyNetworkOptions`. curl applies them through its socket callbacks
  (`CURLOPT_SOCKOPTFUNCTION`, `CURLOPT_OPENSOCKETFUNCTION`), not through an option of their own;
  carrying them as plain members lets the engine apply them without a callback, and a libcurl
  caller who sets neither gets the socket's own values, as libcurl gives.
- The keepalive timings are mapped only with keepalive on and only when given, as `--libcurl`
  writes them: `--keepalive-time` sets both `TcpKeepIdle` and `TcpKeepInterval`, and
  `--keepalive-cnt` sets `TcpKeepCount`. `TcpKeepAlive` is on unless `--no-keepalive`, as the curl
  tool turns it on although libcurl's default is off.
- `UseSsl`: `--ftp-ssl-control` outranks `--ssl-reqd`, whichever comes first, because the command
  line keeps the data connections clear when both are given (`CommandLineOptions.FtpSslControlOnly`);
  `--libcurl` writes `CURLUSESSL_ALL` for that pair. The command line's behaviour wins.
- `--proto` and `--proto-redir` map to every scheme the set allows, lowercase, sorted and joined by
  commas. `--libcurl` leaves out `smb` and `smbs`, which its libcurl lacks; Curl's engine has them,
  so the mapping keeps them.
- `--proto-default` maps lowercase (`CommandLineOptions.DefaultProtocol`); `--libcurl` writes it as
  typed, and libcurl compares it without regard to case.
- `EasyNetworkOptions.Url` is not mapped: the URL is per transfer, not per option group.

## Consequences

The engine retarget (BL-2323) reads every network setting it reads today from the record, so its
behaviour stays byte for byte the same. A libcurl caller who sets `CURLOPT_CONNECTTIMEOUT` to 0
gets the same 300 seconds and no timer, as in libcurl.
