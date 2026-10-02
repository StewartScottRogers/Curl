---
id: BL-1198
title: Match curl's SMTP -v data line for an upload read from standard input
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1198 — Match curl's SMTP -v data line for an upload read from standard input

## Goal

Under `-v` without `--trace-config smtp`, an SMTP upload read from standard input (`-T -`) writes the same `} [N bytes data]` line curl 8.21.0 writes.

## Context

- Found in BL-1163 (ADR-0368). Measured 2026-10-02 with `Record-CurlExchange.ps1 -Smtp -StandardInput 'Subject: x\r\n\r\nhi\r\n' -CurlArgs '-sS','-v','--mail-from','a@b','--mail-rcpt','c@d','-T','-','smtp://127.0.0.1:P/'`: curl writes `} [18 bytes data]` then `* upload completely sent off: 21 bytes` - the 3-byte end-of-data mark gets no data line. Curl writes `} [21 bytes data]`.
- With `-T file` (BL-546) curl writes one line of the whole 25 bytes, which Curl matches; find what differs (stdin read size, a second send whose data line curl drops) before changing `SmtpMailTransaction.SendMessageAsync`.

## Acceptance criteria

- [ ] A `-T -` upload under `-v` writes `} [18 bytes data]` for the measured 18-byte body, matching curl's stderr; a test pins it.
- [ ] The `-T file` lines pinned by BL-546's tests are unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
