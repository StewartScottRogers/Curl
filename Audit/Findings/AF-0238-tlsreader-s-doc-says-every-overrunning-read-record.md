---
id: AF-0238
title: TlsReader's doc says every overrunning read records DecodeError, but ReadVector skips the bounds check, so ServerNameExtension.DecodeHostName throws instead of returning an alert
auditor: truthfulness
severity: High
status: proposed
reason:
key: truthfulness:Curl.Tls.UnitLibrary/TlsReader.cs:TlsReader.ReadVector:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0238 - TlsReader's doc says every overrunning read records DecodeError, but ReadVector skips the bounds check, so ServerNameExtension.DecodeHostName throws instead of returning an alert

## Summary

High finding from the truthfulness auditor at `Curl.Tls.UnitLibrary/TlsReader.cs:76`: TlsReader's doc says every overrunning read records DecodeError, but ReadVector skips the bounds check, so ServerNameExtension.DecodeHostName throws instead of returning an alert.

## Evidence

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:76`

TlsReader's class summary (lines 3-8): 'The first read that runs past the end records TlsAlertDescription.DecodeError and every later read returns zero or empty, so a decoder reads its fields in a straight line and checks once, in Finish'. ServerNameExtension.DecodeHostName's doc says it returns 'the host name, or the alert the bytes call for'. But ReadVector (line 72-78) does 'int length = (int)ReadUnsigned(lengthBytes); int start = position; position += length; return new TlsReader(buffer, start, position, failure);' and never checks length against end. The child reader's end can lie past the buffer, Take() then passes, and buffer[start..position] throws. Run: DecodeHostName([00 10 00 00 05 61]) (vector length 16 in a 6-byte buffer) threw ArgumentOutOfRangeException. DecodeHostName([00 04 00 00 01 61]) returned normally with no alert. Seed for the draw: 0x16500922; ServerNameExtension was type 16 of the 30 drawn.

## Reproduction

Run from the repository root:

```powershell
$d = Join-Path $env:TEMP 'tlsreader-repro'; New-Item -ItemType Directory -Force $d | Out-Null; Set-Content (Join-Path $d 'sni.cs') ("#:project $PWD/Curl.Tls.UnitLibrary/Curl.Tls.UnitLibrary.csproj`ntry { Console.WriteLine(Curl.Tls.ServerNameExtension.DecodeHostName(new byte[] { 0, 16, 0, 0, 5, 0x61 }).Alert); } catch (Exception e) { Console.WriteLine(e.GetType().Name); }"); dotnet run (Join-Path $d 'sni.cs')
```

- Expected: DecodeError
- Actual: ArgumentOutOfRangeException

## Re-audits

## Log

- 2026-10-16: filed proposed.
