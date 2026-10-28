# ADR-0869 — Standard error is the null stream when descriptor 2 was not inherited

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3824
- Decided by Claude under Stewart's delegation.

## Context

On Linux `curl -sS http://127.0.0.1:21199/ 2>&-` (nothing listening) exits 7 with curl 8.18.0
(OpenSSL). Curl printed `Aborted` and exited 134. curl's `main_checkfds` opens `/dev/null` on any
of descriptors 0, 1 and 2 that is closed before it does anything else, so its error line goes
nowhere. Curl cannot run before the .NET runtime, which opens its own pipes at start-up and takes
the lowest free descriptor, so with 2 closed one of those pipes became descriptor 2; Curl's error
line, written over descriptor 2 since ADR-0866, went into the runtime's pipe and the runtime
aborted. `WriteFailureIgnoringStream` (ADR-0815) cannot help: the write succeeds.

## Decision

Off Windows `StandardErrorOpener` opens descriptor 2 only when it was inherited, and otherwise
gives `Stream.Null`, which is where curl's `/dev/null` sends the line. A descriptor is taken as
inherited when `fcntl(2, F_GETFD)` succeeds and `FD_CLOEXEC` is clear: every descriptor the .NET
runtime opens is close-on-exec, and one that crossed `exec` cannot be. The call goes through the
runtime's own `SystemNative_FcntlGetFD` shim in `libSystem.Native` (fixed signature; libc's
`fcntl` is variadic, ADR-0866), in `NativeInheritedFileDescriptor`, which ADR-0083 lets the
Windows coverage measure leave out; `NativeInheritedFileDescriptorTests` checks it off Windows.

On Windows nothing changes.

## Consequences

- `curl -sS <unreachable URL> 2>&-` exits 7 on Linux as curl does (measured in WSL, BL-3824).
- `2>&1`, `2>file` and `2>&1 | cat` still write the line: dup2 clears close-on-exec.
- Standard output closed (`1>&-`) is not covered here; it is a separate defect.
