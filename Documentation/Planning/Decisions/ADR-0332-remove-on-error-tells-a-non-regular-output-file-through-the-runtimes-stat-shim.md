# ADR-0332 — `--remove-on-error` tells a non-regular output file apart through the runtime's `stat` shim

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-752.

## Context

curl 8.21.0's `post_per_transfer` (`src/tool_operate.c`) stats a failed transfer's output file
before deleting it under `--remove-on-error`: a regular file is unlinked (`Note: Removed output
file`, or `Warning: Failed removing`), and anything else - a device, a directory, nothing -
gets `Warning: Skipping removal; not a regular file: <file>` and is left alone. Measured
2026-10-01 in Docker on `curlimages/curl:8.21.0` (OpenSSL 3.5.7, musl, Linux x86-64), a server
cutting a 100-byte body short after 3 bytes:

| Command | Exit | stderr |
| --- | --- | --- |
| `curl --no-progress-meter --remove-on-error -o /dev/null <url>` | 18 | `curl: (18) end of response with 97 bytes missing` LF `Warning: Skipping removal; not a regular file: /dev/null` LF |
| `curl -s --remove-on-error -o /dev/null <url>` | 18 | (none) |

On Windows the Schannel curl 8.21.0 prints `Warning: Failed removing: NUL` for `-o NUL`
(BL-494 Notes), so its `stat` does not tell `NUL` apart there.

.NET has no public way to tell a character device from a regular file on Unix:
`File.Exists`, `File.GetAttributes`, `FileStream.CanSeek` and `RandomAccess.GetLength` say the
same of `/dev/null` as of an empty file (probed in `mcr.microsoft.com/dotnet/sdk:10.0`).

## Decision

`IOutputPaths.RemoveFile` returns an `OutputFileRemoval`: `Removed`, `Failed` or
`NotRegularFile`. `PhysicalOutputPaths` asks an injected regular-file test first; production
passes `NativeRegularFileTest.ForCurrentPlatform()`, which is `null` on Windows (Windows keeps
BL-494's measured behaviour) and elsewhere calls the .NET runtime's own `stat` shim,
`SystemNative_Stat` in `libSystem.Native`, reading `S_IFMT` from its `Mode`. The runner prints
curl's `Skipping removal` warning for `NotRegularFile`, unless `-s`.

## Alternatives considered

- **libc `stat` directly.** Its `struct stat` differs by platform and architecture (`st_mode`
  at byte 24 on Linux x86-64, 16 on Linux arm64, 4 on macOS, and the `stat` symbol is missing
  before glibc 2.33); the runtime's shim has one layout everywhere and ships with every .NET
  app, statically linked under native AOT.
- **Leave it as `Failed removing`.** Not what curl prints, and as root it would unlink
  `/dev/null`, which is exactly what curl's check prevents.

## Consequences

- `NativeRegularFileTest` is a thin adapter excluded from the Windows coverage measure (ADR-0083)
  and checked by `NativeRegularFileTestTests` on Linux and macOS in CI.
- The shim is an internal runtime export; were it ever renamed, `NativeRegularFileTestTests`
  would fail on Linux and macOS CI before a release.
