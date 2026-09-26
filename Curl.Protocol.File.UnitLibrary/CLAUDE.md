# Curl.Protocol.File.UnitLibrary

Phase 1.

Local file access. No network involved.

**URL schemes:** `file`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `FileStream` here. Take `IFileSystem` so the tests in the
matching `.UnitTests` project can drive this code against an in-memory fake with no
disk access. See `Documentation/Planning/Decisions/ADR-0002-ifilesystem-as-the-second-protocol-seam.md`
for why `file` uses `IFileSystem` instead of the `IConnection` seam every other
protocol library uses.

## Transfer options, per direction

ADR-0003 requires each protocol to state which `ITransferContext` transfer options it
ignores. `FileProtocolHandler` takes the upload path when `ITransferContext.Upload` is set
and the download path otherwise; the two diverge after `ExecuteAsync`, which refuses
a negative `ResumeFrom` as exit 36 for both before either starts.

| `ITransferContext` member | Download | Upload |
| --- | --- | --- |
| `ResumeFrom` | honoured: start of the window sent (`TryResolveWindow`) | honoured: a positive offset opens the destination for append and skips that many source bytes (`UploadAsync`, `UploadIntoAsync`) |
| `Range` | honoured: the window sent, when `ResumeFrom` is not set (`TryResolveWindow`) | ignored |
| `NoBody` | honoured: headers only, then success (`DownloadFromAsync`) | ignored |
| `TimeCondition` | honoured: an unmet condition is success with nothing written (`DownloadFromAsync`) | ignored |
| `HeaderOutput` | honoured: receives the pseudo-headers (`DownloadFromAsync`) | ignored |
| `ConvertLineEndings` | ignored | honoured: `--crlf` conversion of each chunk (`UploadIntoAsync`) |
| `TimeProvider` | ignored | ignored |
| `MaxFileSize` | honoured: exit 63 past the limit (`DownloadFromAsync`) | ignored |
| `CreateFileMode` | ignored | honoured: the mode the destination is opened with (`UploadAsync`) |
| `PostData`, `Credentials`, `TelnetOptions`, `TftpBlockSize`, `TftpNoOptions` | ignored | ignored |

Every row was read from `FileProtocolHandler.cs` — `ExecuteAsync`, `DownloadFromAsync`,
`UploadAsync`, `UploadIntoAsync` and `TryResolveWindow` — on 2026-09-26. When the handler
starts or stops reading a member, change its row in the same commit.

`TimeProvider` is unused in both directions on purpose: nothing in a local file transfer
is timed or retried, and `-z`/`--time-cond` compares against the timestamp the open
reported (`FileOpenResult.LastWriteTimeUtc`), not against now.

## No path sandboxing

No component between the URL and `IFileSystem` sandboxes or confines a path.
`FileUrlPath` removes dot segments the way curl does (so `..` climbs, it is not refused),
accepts any drive letter, and hands on paths the operating system may reject or resolve
somewhere surprising; `FileProtocolHandler` passes `FileUrlPath.OsPath` straight to
`IFileSystem.OpenForReadAsync` or `IFileSystem.OpenForWriteAsync`. Reading
`file:///C:/Windows/win.ini`, or writing over any file the process may write, is meant
to work.

This matches curl, checked against curl 8.21.0 and
<https://curl.se/docs/url-syntax.html>, which restricts only the hostname of a `file://`
URL and warns that on Windows a crafted path may become a network (SMB) access that curl
cannot control.

An `IFileSystem` implementation must therefore treat every path it receives as
unvalidated and untrusted input. A caller that embeds this library and needs confinement
must supply it itself, in its own `IFileSystem` or before the URL reaches the handler.
