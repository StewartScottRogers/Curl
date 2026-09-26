---
id: BL-064
title: Apply the TLS build decision to TLS failure messages and --capath
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-059, BL-063]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-064 — Apply the TLS build decision to TLS failure messages and --capath

## Goal

`SslStreamTlsProvider` writes the TLS failure messages, and treats `--capath`, exactly as
the ADR from BL-059 decides, and the tests pin the decided text byte for byte.

## Context

- BL-059 (Stewart) decides, per platform, which curl TLS build's message text Curl
  reproduces for exits 35, 58, 60 and 77, and whether `--capath` is honoured or warned
  about and ignored. Read that ADR in `Documentation/Planning/Decisions/` first; it is the
  specification for this task. Its Context records the Schannel build's measured lines,
  for example
  `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.`
  followed by the `More details here: https://curl.se/docs/sslcerts.html` block.
- BL-062 put every TLS failure message in one internal place in
  `Curl.Networking.UnitLibrary`; change it there. The `curl: (N) ` prefix is added by
  `Curl.Console` (BL-068), not here. If a decided message has more than one line (the
  exit 60 help block does in the Schannel build), state in `Notes` whether the extra
  lines belong in `ConnectResult.ErrorMessage` or are printed by the console, and file a
  `Curl.Console` task for the latter rather than editing `Curl.Console` here.
- `--capath`: add `string? CaCertificateDirectory` to `TlsClientOptions`. If the ADR says
  honour it, load every certificate file in the directory into the custom trust store
  BL-063 uses; if it says warn and ignore, expose the warning lines the ADR gives so the
  console can print them, and do not use the directory.
- Upstream: <https://curl.se/docs/manpage.html> (`--capath`), exit codes
  <https://curl.se/libcurl/c/libcurl-errors.html>; the reference build is curl 8.21.0.

## Acceptance criteria

- [x] For each of exits 35, 60 and 77 the provider can produce, a named test asserts the
      `ErrorMessage` the ADR from BL-059 specifies, byte for byte, on each platform the
      ADR distinguishes (a test that does not apply to the running platform reports
      `Assert.Inconclusive` with the reason).
- [x] `TlsClientOptions` has `CaCertificateDirectory`, and named tests assert the decided
      `--capath` behaviour: trust through the directory's certificates, or the warning
      lines with the directory unused.
- [x] No test is tagged `Integration` and no test opens a socket.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

- **Plan.** Follow ADR-0009 per platform. `SslStreamTlsProvider`'s public constructor
  picks the build from `OperatingSystem.IsWindows()` (Schannel on Windows, OpenSSL
  elsewhere); an internal constructor takes `matchesSchannelBuild`, so both builds' text
  and `--capath` behaviour are pinned by tests on any platform. Every message stays in
  `TlsFailureMessages`, now one method per build per exit.
- **Exit 60 help block: the console prints it.** The five `More details here` lines are
  written by curl's tool, not by libcurl's error buffer, so `ConnectResult.ErrorMessage`
  carries only the one line after `curl: (60) `. Filed as BL-135 (`Curl.Console`).
- **`--capath`.** `TlsClientOptions.CaCertificateDirectory` added. Schannel build: the
  directory is never read, and `SslStreamTlsProvider.Warnings` holds the two measured
  lines (trailing space kept); they are reported whether or not `-k` is given, and the
  console (BL-072) prints them unless `-s`. OpenSSL build: every PEM certificate in the
  directory's files (not subdirectories) is trusted. Without `--cacert` it is trusted
  beside the system store (the chain is rebuilt against the directory's roots when the
  system store rejects it); with `--cacert` the roots are the file's plus the directory's.
  A missing, unlistable or non-directory path, or an unreadable or unparsable file, adds
  nothing and prints nothing.
- **`--cacert` by build.** Schannel: a file that cannot be opened (a directory) is 77
  `schannel: failed to open CA file '<path>'`; one with no parsable certificate (empty,
  text, corrupt block) trusts what parses and so fails 60 with the "based on an untrusted
  root" line, as the ADR says. OpenSSL: unreadable, empty, no certificate, or any block
  that does not parse is 77 `error adding trust anchors from file: <path>`.
- **Exit 35.** Schannel: the `Win32Exception` in the chain gives the status; names come
  from a table of the SEC_E_* codes curl's table names, others print `Unknown error`, hex
  is `X8` per the ADR. The real .NET-server handshake on Windows gives
  `SEC_E_ILLEGAL_MESSAGE (0x80090326)` (a .NET server refuses differently from
  `openssl s_server`); the measured `SEC_E_UNSUPPORTED_FUNCTION` line is pinned from a
  recorded exception. OpenSSL: the first message in the exception chain that starts
  `error:` is the OpenSSL error string; its real-handshake test runs on Linux only
  (macOS's SslStream is not OpenSSL) and could not be run here: WSL has no .NET.
- **Defaults taken for cases the ADR did not measure** (all re-checked by BL-136):
  handshake closed with no status, Schannel `schannel: failed to receive handshake,
  SSL/TLS connection failed` (curl's source text), OpenSSL `TLS connect error:
  <innermost exception message>`; OpenSSL verify results 19, 20, 9 and 10 beside the
  measured 18, chosen from the chain's shape and status; the ADR's single name-mismatch
  line for every certificate in each build, although the OpenSSL build probably prints
  a different line for a certificate with subjectAltNames; chain errors win over a name
  mismatch in both builds, as both verify the chain first; `--cacert` still replaces
  the system store. The ADR's store-replacement and macOS bundle measurements are
  deferred to BL-136, since no reference build is available to this lane.
- **ADR Consequences, "a test that asserts Windows text must not run a real handshake
  on Linux".** The handshake tests that assert Schannel text for exit 60 and 77 run on
  every platform on purpose: that text comes from our own mapping of platform-neutral
  `SslPolicyErrors` and file errors, not from Schannel. The one test whose text comes
  from Schannel itself (exit 35 status) is gated to Windows.
- **Review (code-reviewer).** Fixed: an unlistable `--capath` directory threw out as
  exit 77 with an empty path; an empty chain in the directory rebuild. Accepted: `--capath`
  files are read synchronously, as `--cacert` already was; chain certificates are not
  disposed in the short-lived process.
- **Quality.** `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100%
  branch; every new or changed member at 100% line, complexity at most 10. The only
  members still failing are the socket paths in `TcpDialer` and `UdpDatagramChannel`,
  which were already failing before this task because only `Integration` tests reach
  them.
- **Filed:** BL-135 (console prints the exit 60 help block), BL-136 (measure the
  unmeasured cases and pin them).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. SslStreamTlsProvider writes ADR-0009's Schannel or OpenSSL text for exits 35, 60 and 77, and --capath is honoured (OpenSSL) or ignored with the two warning lines (Schannel)
