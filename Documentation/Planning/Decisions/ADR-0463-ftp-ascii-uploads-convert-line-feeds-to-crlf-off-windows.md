# ADR-0463: An FTP ASCII upload converts lone line feeds to CRLF off Windows

- Status: Accepted
- Date: 2026-10-10
- Task: BL-1957
- Decided by Claude under Stewart's delegation.

## Context

Upstream's test475 uploads a file to `ftp://.../475;type=a` and expects CRLF lines on the wire.
Its `<file>` holds CRLF lines under `%if win32` and LF lines everywhere else. Curl passed the
case on Windows and failed it on Linux and macOS: its FTP upload sent the bytes unchanged in
ASCII mode, converting only under `--crlf`.

curl adds its line-conversion reader (`cr_lc`, a CR before every LF not already after one)
under `--crlf`, and under `data->state.prefer_ascii` only when built with
`CURL_PREFER_LF_LINEENDS`, which `curl_setup.h` defines everywhere but Windows. So the
OpenSSL builds on Linux and macOS convert an ASCII upload and the Schannel build on Windows
does not. test476 (a file already in CRLF, expected unchanged) confirms the conversion leaves
existing pairs alone.

## Decision

1. `FtpUploadLineEndings.AreConverted` in `Curl.Protocol.Ftp.UnitLibrary` decides: under
   `--crlf` on every platform, and for an ASCII transfer (`-B` or `;type=a`) when not on
   Windows. `FtpSession` asks it with `OperatingSystem.IsWindows()` and reuses the existing
   `CrlfUploadConverter`.
2. The `Uploaded unaligned file size` `-v` line treats an ASCII conversion as it treats
   `--crlf`: reported only when fewer bytes than the file's size were sent, since the
   conversion may add bytes.
3. The rule is a public static method so its off-Windows answer is covered by the Windows
   test run; the handler tests pin each platform's bytes behind `OSCondition`.

## Consequences

test475 holds the ratchet on all three platforms. On Windows nothing changes. Not measured
against real curl in this run: the rule follows upstream's own test data and source, which
are the reference the conformance ratchet already pins.
