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
completed:
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

- [ ] For each of exits 35, 60 and 77 the provider can produce, a named test asserts the
      `ErrorMessage` the ADR from BL-059 specifies, byte for byte, on each platform the
      ADR distinguishes (a test that does not apply to the running platform reports
      `Assert.Inconclusive` with the reason).
- [ ] `TlsClientOptions` has `CaCertificateDirectory`, and named tests assert the decided
      `--capath` behaviour: trust through the directory's certificates, or the warning
      lines with the directory unused.
- [ ] No test is tagged `Integration` and no test opens a socket.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
