# Libcurl

The libcurl-faithful programming interface to Curl (ADR-0506): the libcurl C API shaped for C#.
It is named `Libcurl`, without the `Curl.` prefix, to match upstream's library name; with
`Curl.Api`, `Curl.Config`, `Curl.Console` and `Curl.Mcp` it is a naming exception. The root namespace is `Libcurl`, and each
area below is a folder and a namespace (`Libcurl/Global` is `Libcurl.Global`).

`Libcurl` does no transfers itself. Every handle wraps an engine interface from
`Curl.Engine.UnitLibrary` (`ICurlEngine` and the handles it creates), so the library can be tested
without a network and a caller can supply its own engine.

## Use

`CurlGlobal.Init` is `curl_global_init`. It returns a `CurlLibrary`, whose `DisposeAsync` is
`curl_global_cleanup`; the handles are created from it.

```csharp
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

await using CurlLibrary library = CurlGlobal.Init();
using CurlEasy easy = library.EasyInit();
easy.SetOption(CurlOption.Url, "http://example.com/");
CurlCode code = easy.Perform();
```

`Init(ICurlEngine, flags)` takes an engine of the caller's choosing; without one a
`ManagedCurlEngine` is created. `InitMemory` also takes the `malloc`, `free`, `realloc`, `strdup`
and `calloc` callbacks of `curl_global_init_mem`.

## Which libcurl APIs it covers

| libcurl API | Folder | Main types |
| --- | --- | --- |
| global (`curl_global_*`) | `Global` | `CurlGlobal`, `CurlLibrary`, `CurlGlobalFlags`, `CurlSslSet`, the memory callback delegates |
| easy (`curl_easy_*`) | `Easy` | `CurlEasy` (`EasyInit`, `Perform`, `PerformAsync`, `SetOption`, `Reset`, `DupHandle`, `Pause`, `Send`, `Recv`) |
| easy options (`curl_easy_setopt`, `curl_easy_option_*`) | `Options`, `Callbacks` | `CurlOption`, `CurlOptions`, `EasyOptionDispatch` |
| easy info (`curl_easy_getinfo`) | `Info` | `CurlInfo`, `EasyInfoDispatch`, `EasyInfoReaders` |
| multi (`curl_multi_*`) | `Multi` | `CurlMulti`, `CurlMultiOption`, `CurlMessage`, the socket and timer callbacks |
| share (`curl_share_*`) | `Share` | `CurlShare`, `CurlShareOption` |
| url (`curl_url_*`) | `Url` | `CurlUrlHandle` |
| header (`curl_easy_header`, `curl_easy_nextheader`) | `Headers` | `CurlEasyHeaders` |
| mime and form (`curl_mime_*`, `curl_formadd`) | `Mime` | `CurlMime`, `CurlMimePart`, `CurlForm` |
| websocket (`curl_ws_*`) | `WebSocket` | `CurlEasyWebSocket` (`WebSocketSend`, `WebSocketSendAsync` and the receive side) |
| printf (`curl_mprintf` family) | `Misc/Printf` | `CurlPrintf` |
| version (`curl_version_info`) | `Misc/Version` | `CurlVersion`, `CurlVersionFeatures` |
| strings, dates, lists, error text | `Misc/Strings`, `Misc/DatesAndLists`, `Misc/ErrorText` | `CurlStrings` (`curl_easy_escape`), `CurlDate`, `CurlSlist`, `CurlErrorText` (`*_strerror`) |

## Folder map

- `Global`: library initialisation and cleanup, and the allocation callbacks (`EasyAllocations`
  makes an `InitMemory` library's easy handles allocate through them).
- `Easy`: `CurlEasy` and its `CurlLibrary.EasyInit` part. `CurlLibrary` is `partial`: each handle
  area adds its public creating method (`EasyInit`, `MultiInit`, `ShareInit`, `UrlInit`,
  `MimeInit`) in its own folder.
- `Options`: the options table generated from curl 8.22.0's `easyoptions.c` and `curl.h`, and the
  `EasyOptionDispatch` that `SetOption` goes through. Each group (`Authentication`, `Behaviour`,
  `Http`, `Network`, `Protocols`, `Proxy`, `Tls`, `Transfer`) holds its setter rows in an
  `EasyOptionSetters` part.
- `Callbacks`: the callback options, one folder per area (`ReadWrite`, `Progress`, `Seek`,
  `Sockets`, `Phases`, `Wildcard`, `Hsts`, `Keys`), each an `EasyOptionSetters` part that keeps the
  namespace `Libcurl.Options`.
- `Info`: `CurlInfo` and the readers behind `getinfo` (`Longs`, `Strings`, `Times`).
- `Multi`: `Core`, `Settings`, `Sockets` and `Wait`, the parts of `CurlMulti`.
- `Share`, `Url`, `Headers`, `Mime`, `WebSocket`: one area each, as in the table above.
- `Misc`: `DatesAndLists`, `ErrorText`, `Printf`, `Strings` and `Version`.

## Quality gates and siblings

`Libcurl` is held to 100% line and branch coverage, cyclomatic complexity at most 10 per method
and a CRAP score at most 30, measured from the fast tests only. Its tests are in
`Libcurl.UnitTests`, in the same folders. It depends on [`Curl.Api.UnitLibrary`](../Curl.Api.UnitLibrary/README.md) (result codes such as
`CurlCode`) and `Curl.Engine.UnitLibrary` (the engine it drives), and the conformance runner
`Curl.Conformance.UnitProgram.UnitLibrary` references it. Its .NET-idiomatic sibling over the same engine is [Curl.Api](../Curl.Api/README.md). See `CLAUDE.md` in this folder for the rules each area follows, and the [Glossary](../Documentation/Wiki/Glossary.md) for the terms.
