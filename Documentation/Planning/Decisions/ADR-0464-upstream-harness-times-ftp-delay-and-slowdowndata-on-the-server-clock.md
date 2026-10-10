# ADR-0464: The upstream harness times ftpserver.pl's DELAY and SLOWDOWNDATA on the server clock

- Status: Accepted
- Date: 2026-10-10
- Task: BL-1958
- Decided by Claude under Stewart's delegation.

## Context

Upstream's `ftpserver.pl` (at `curl-8_21_0`) reads two `<servercmd>` lines that time its
answers: `DELAY <COMMAND> <seconds>` sleeps that long before it answers the command, and
`SLOWDOWNDATA` waits after each byte it sends on a data connection. test190 (`DELAY CWD 60`,
`-m 10`) and test1086 (`SLOWDOWNDATA`, `-m 5`) depend on them to make curl time out with exit
28, and test1112 is test1086 over FTPS. The FTP stand-in answered every line at once and sent
all data bytes together, so screening skipped all three. The task names 5 ms a byte.

## Decision

1. `LineProtocolReply` carries a `Delay`. `FtpControlChannelResponder` sets it from
   `LineProtocolServerCommands.ReplyDelay`, matching the command as the client wrote it (as
   ftpserver.pl's `$delayreply{$FTPCMD}` does) and reading a `DELAY` line only when no `REPLY`
   or `COUNT` branch took it first. An empty or 0 number never delays.
2. `LineProtocolServerConnection` sends a delayed reply after the delay on the clock its
   connector is given, and a reply after a delayed one waits for it, keeping their order, as
   ftpserver.pl answers one command at a time. The SMTP, IMAP and POP3 responders never set a
   delay, so their behaviour is unchanged.
3. `SLOWDOWNDATA` makes `RETR`, `LIST` and `NLST` send their data one byte at a time, 5 ms
   apart, then close (`FtpDataConnection.SendSlowlyAsync`). The control channel's `226` is not
   held back until the data is sent: curl reads the data connection to its end either way, and
   no case depends on the order. Sending stops once the client disposes the data connection,
   so a timed-out case leaves no task trickling bytes for long.
4. Both wait on the runner's server clock: the real clock when a curl timer such as `-m` races
   the server, else the `WaitSkippingTimeProvider`, which takes no real time (ADR-0404).
5. Screening no longer skips an ftp or ftps case for `DELAY` or `SLOWDOWNDATA`.

## Consequences

test190 and test1086 pass and are on the passing list; test190 takes 10 seconds and test1086
5 seconds of real time in the conformance run, within its 20-second curl limit. test1112 runs and
differs on a known Curl difference (`PROT P` where curl sends `PROT C`).
