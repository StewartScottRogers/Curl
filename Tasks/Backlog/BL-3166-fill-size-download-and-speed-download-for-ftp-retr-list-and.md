---
id: BL-3166
title: Fill size_download and speed_download for FTP RETR, LIST and resumed downloads
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerSizeDownloadTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3166 — Fill size_download and speed_download for FTP RETR, LIST and resumed downloads

## Goal

For FTP transfers, `%{size_download}` and `%{speed_download}` report the bytes received and their speed, not 0.

## Context

Stewart reported on 2026-10-22: against `ftp://test.rebex.net/readme.txt` (user demo, password password), `-w '%{size_download} %{speed_download}'` gives `0 0` from Curl and `379 173` from real curl (the speed varies with the link). The write-out numbers are built in `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs` from `result.Report?.DownloadSize ?? result.BytesTransferred` (`WithTimesAndSpeeds`), so the FTP handler's `TransferResult` carries a zero size or no report. Find in `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs` where the result is built after RETR and LIST (it already counts `bytesTransferred`) and fill `TransferReport.DownloadSize` (`Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs`; a contract, so only read it; if a field is missing, file a follow-up task). Cover: a plain RETR, a LIST/NLST listing, and a resumed `-C` download (curl reports the bytes received in this transfer, not the file size; measure curl 8.22.0 with `Record-CurlExchange.ps1` for the exact value and pin it). The real server also fails with exit 18 today (the data-connection close task BL-3161), so this task's tests use a loopback fake FTP server. Speed is bytes over total time, truncated, as `trspeed` computes it (`BytesPerSecond`); no change is expected there.

## Acceptance criteria

- [ ] `Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerSizeDownloadTests.cs` has a RETR test where the returned `TransferResult`/report gives a download size equal to the bytes written to the output.
- [ ] A LIST test and an NLST test give a download size equal to the listing bytes written.
- [ ] A resumed `-C <offset>` download test pins the size curl 8.22.0 reports, noted in the test with the measurement.
- [ ] A test through `WriteOutTransferInfo` (in `Curl.Output.UnitTests/WriteOutTransferInfoTests.cs` only if a gap shows there) or through the FTP handler's report shows `%{size_download}` is non-zero and `%{speed_download}` is size over total time for a fake clock that advances 1 s.
- [ ] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Protocol.Ftp.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-22: Created.
