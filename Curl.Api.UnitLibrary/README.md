# Curl.Api.UnitLibrary

The shared core of Curl's two programming interfaces (ADR-0506): the types that both
[`Curl.Api`](../Curl.Api) (the .NET-idiomatic API) and [`Libcurl`](../Libcurl) (the
libcurl-faithful API) are built from. It holds data and contracts only: option records,
callback delegates, result codes, info records and the interfaces a transfer engine
implements. It does not perform a transfer itself.

## How it is used

`Curl.Api` and `Libcurl` each reference this library and `Curl.Engine.UnitLibrary`. They
describe a transfer with the option records, hand it to an engine through the
`EngineContracts` interfaces (`ICurlEngine` creates easy, multi, share, URL, MIME and form
handles), and report the outcome as a `CurlCode`. `CurlCode` shares its member names and
numbers with `CurlExitCode`, so the two convert without a lookup:

```csharp
using Curl.Api.Codes;
using Curl.Protocol.Abstractions;

CurlExitCode exitCode = CurlCode.Ok.ToCurlExitCode();
CurlCode code = exitCode.ToCurlCode();
```

## Folder and type map

Namespace `Curl.Api` plus the folder path (`Curl.Api.Callbacks`, `Curl.Api.Options.Http` and so on).

| Folder | Holds |
| --- | --- |
| `Options/` | `EasyOptions`, the record of every `curl_easy_setopt` option, split into one record per area: `Behaviour`, `Transfer`, `Network`, `Tls`, `Authentication`, `Proxy`, `Http` and `Protocols` (FTP, SSH, SMTP, TFTP, TELNET, RTSP, WebSocket). Each area is a subfolder with its option records and the enums and flag types they use. |
| `Callbacks/` | The `CURLOPT_*FUNCTION` callbacks as delegates (`WriteCallback`, `HeaderCallback`, `ReadCallback`, `ProgressCallback`, `DebugCallback`, the SSH, HSTS, socket and chunk callbacks), with their result constants and value types. |
| `Codes/` | The result codes `CurlCode`, `CurlMultiCode`, `CurlShareCode`, `CurlUrlCode`, `CurlHeaderCode` and `CurlFormAddCode`, and `CurlCodeConversion`, which converts between `CurlCode` and `CurlExitCode`. |
| `Info/` | `TransferInfo`, what `curl_easy_getinfo` reports, and `TlsBuildAnswers`. |
| `EngineContracts/` | The engine's interfaces and their types: `ICurlEngine`, `IEasyTransfer`, `CurlVersionInfo`, `CurlPause`, and the subfolders `ConnectOnly`, `Headers`, `Mime`, `Multi`, `Share`, `Tls`, `Url` and `WebSocket`. |

## Quality gates and sibling projects

- References only `Curl.Protocol.Abstractions.UnitLibrary` (for `CurlExitCode`). It never
  references a protocol library, `Curl.Engine.UnitLibrary`, `Curl.Api` or `Libcurl`.
- Held to the root `CLAUDE.md` gates: 100% line and branch coverage, cyclomatic complexity
  at most 10 per method, CRAP score at most 30.
- Its tests are in `Curl.Api.UnitTests`, which tests this library and `Curl.Api`; the
  project grants that assembly `InternalsVisibleTo`.
- See [CLAUDE.md](CLAUDE.md) for the rules for changing it.
