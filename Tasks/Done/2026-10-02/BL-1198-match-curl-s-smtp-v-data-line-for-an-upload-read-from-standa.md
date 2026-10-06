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
completed: 2026-10-02
---
# BL-1198 — Match curl's SMTP -v data line for an upload read from standard input

## Goal

Under `-v` without `--trace-config smtp`, an SMTP upload read from standard input (`-T -`) writes the same `} [N bytes data]` line curl 8.21.0 writes.

## Context

- Found in BL-1163 (ADR-0368). Measured 2026-10-02 with `Record-CurlExchange.ps1 -Smtp -StandardInput 'Subject: x\r\n\r\nhi\r\n' -CurlArgs '-sS','-v','--mail-from','a@b','--mail-rcpt','c@d','-T','-','smtp://127.0.0.1:P/'`: curl writes `} [18 bytes data]` then `* upload completely sent off: 21 bytes` - the 3-byte end-of-data mark gets no data line. Curl writes `} [21 bytes data]`.
- With `-T file` (BL-546) curl writes one line of the whole 25 bytes, which Curl matches; find what differs (stdin read size, a second send whose data line curl drops) before changing `SmtpMailTransaction.SendMessageAsync`.

## Acceptance criteria

- [x] A `-T -` upload under `-v` writes `} [18 bytes data]` for the measured 18-byte body, matching curl's stderr; a test pins it.
- [x] The `-T file` lines pinned by BL-546's tests are unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Cause: curl's tool writes one `} [N bytes data]` line for back-to-back data events (tool_cb_dbg.c's `traced_data`), which `VerboseTransferEventWriter` already mirrors. With `-T -` curl does not know the size and learns the end only from a read that returns 0, so it sends the 18-byte body and the 3-byte mark as two sends: `-v` shows only `} [18 bytes data]`. `--trace-config smtp` writes text lines between them, so both show there (ADR-0368). With `-T file` the size is known and the mark goes out with the last read: one `} [25 bytes data]`.
- Fix: `SmtpMailTransaction.SendMessageAsync` sends each read as it arrives and the mark on its own when the upload's size is unknown (`expected is null`), as it already did under the trace. Pinned by `SmtpProtocolHandlerEventTests.ExecuteAsync_UploadFromStandardInput_ReportsTheBodyAndThenTheMarkAsBackToBackDataEvents`. The stdin progress test now sees two reports (6, then 11). BL-546's `-T file` tests are unchanged and pass.
- Re-measured curl 8.21.0 on 2026-10-02: `} [18 bytes data]`, `* upload completely sent off: 21 bytes`. The sandbox blocked running the built Curl binary through the recorder, so the end-to-end match rests on this unit test plus the writer's existing suppression tests.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A -T - SMTP upload under -v writes curl's single } [18 bytes data] line
