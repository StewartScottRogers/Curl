# ADR-0466: A `-K` file that is not valid UTF-8 is read in the ANSI code page on Windows

- Status: Accepted
- Date: 2026-10-10
- Task: BL-1973 (GF-0020, upstream test470 and test411)
- Decided by Claude under Stewart's delegation.

## Context

curl 8.21.0's Windows build uses a config file's bytes raw. Curl read every `-K` file as UTF-8,
so a file that is not valid UTF-8 lost its bytes: measured on Windows (Schannel, 2026-10-10), a
file holding `-H <93>host:fake<94>` makes real curl send `93 host:fake 94` with no warning, and
Curl sent `EF BF BD host:fake EF BF BD`. ADR-0446 already makes a UTF-8 file's header text go out
as its UTF-8 bytes; this is the other half.

Separately, the `curl: cannot read config from '<file>'` line wrapped at the process's own
`COLUMNS`, not the `COLUMNS` the parse's injected environment reader gives, so an in-process run
handed a wide `COLUMNS` (the upstream case runner) still saw the line wrapped at 79.

## Decision

1. On Windows, a `-K` file or default config file whose bytes are not valid UTF-8 is decoded in the
   ANSI code page the request side encodes option text in (`ConfigFileWireTextEncoding`). Its
   values then go out as the file's own bytes; they are not re-spelled (ADR-0446) and do not get
   the leading-Unicode warning, which curl gives only for UTF-8 sequences. A nested file is judged
   on its own bytes, and the enclosing file's reading resumes after it.
2. The unreadable-config-file refusal wraps at `WrappedMessage.TerminalColumns` of the parse's
   `ReadEnvironmentVariable("COLUMNS")`. In production that reader is the process environment, so
   nothing changes there.

## Consequences

- A mixed file (some valid UTF-8 sequences and some invalid bytes) is read wholly in the ANSI code
  page: its bytes still go out unchanged, but a UTF-8 smart quote in it gets no warning. Not
  measured as a gap.
- Off Windows a non-UTF-8 file is still read as UTF-8; no gap is measured there yet.
- Other wrapped messages still read the process's `COLUMNS`; only the measured one moved.
