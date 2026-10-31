# Libcurl

The libcurl-faithful programming interface to Curl (ADR-0506): named `Libcurl`, with no `Curl.`
prefix, to match upstream's library name. A naming exception, with `Curl.Api`, `Curl.Config`, `Curl.Console` and `Curl.Mcp`.
Root namespace `Libcurl`; each area is a folder and namespace (`Libcurl/Global` is `Libcurl.Global`),
with its tests in the same folder of `Libcurl.UnitTests`.

`Global` holds `CurlGlobal` (`curl_global_init`, `_init_mem`, `_sslset`, `_trace`) and `CurlLibrary`,
the object `Init` returns over an injected `ICurlEngine`, or over a new `ManagedCurlEngine` when none is
given; its `DisposeAsync` is `curl_global_cleanup`
(ADR-0556). `CurlLibrary` is `partial`: each handle area adds its public creating method
(`curl_easy_init` and the rest) as a `CurlLibrary` part in its own folder, over the internal
`Create*` methods in `Global`. Tests that count inits use `CurlGlobal.InitCounting` with their own
`GlobalInitCounter`, never the process-wide count, since tests run in parallel. An `InitMemory`
library's easy handles allocate through its callbacks (`EasyAllocations`, ADR-0664): `calloc` at init
and `DupHandle`, `strdup` per accepted string option, `malloc` per `CurlStrings.Escape` given the
handle, and `free` of each at cleanup.

`Options` holds the options table (ADR-0560): `CurlOption` and `CurlOptions` (`curl_easy_option_by_name`,
`_by_id`, `_next`) generated from curl 8.22.0's `lib/easyoptions.c` and `curl.h`, and the internal
`EasyOptionDispatch` `SetOption` goes through. Each option group adds its setter rows by implementing
its `Add<Group>Setters` partial method in an `EasyOptionSetters` part in `Options/<Group>`, keeping the
namespace `Libcurl.Options`; an option no group sets yet answers `NotBuiltIn`. `Options/Behaviour` sets
the `EasyBehaviourOptions` options: an on/off option is on for any value but 0, the two permission
options take 0 to 0777, and an object option given a value its field cannot hold answers
`BadFunctionArgument`, as libcurl's `setopt.c` does. `Options/Network` sets the `EasyNetworkOptions`
options with `setopt.c`'s range checks, and turns the `PROTOCOLS` bitmasks into `PROTOCOLS_STR` names
(ADR-0568); `CURLU` takes a `CurlUrlHandle` and stores its engine `ICurlUrlHandle` by reference as
`UrlHandle`, `null` clears it and any other object answers `BadFunctionArgument` (ADR-0590). `Options/Transfer` sets the
`EasyTransferOptions` options with `setopt.c`'s checks, each `_LARGE` or `_MS` alias setting the same
field as the option it extends, and clamps the two buffer sizes rather than refusing them (ADR-0572). `Options/Proxy` sets the
`EasyProxyOptions` options with `setopt.c`'s checks, keeps a copy of the `PROXYHEADER` list, and
turns `HAPROXYPROTOCOL` on whenever `HAPROXY_CLIENT_IP` is set (ADR-0576). `Options/Tls` sets the `EasyTlsOptions`
options (ADR-0580): `TlsPeerOptionSetters.Origin` and `.Proxy` write the origin's options and their
`PROXY_*` twins with one method each, blobs are copied, and `SSL_FALSESTART` answers `NotBuiltIn`, Curl
having no False Start. Options the platform build's TLS backend or OS lacks answer its code behind one
`*IsBuiltIn` flag each (ADR-0991, ADR-0995): under Schannel (Windows) `CRLFILE`, `ISSUERCERT`, `SSL_EC_CURVES`,
`SSL_SIGNATURE_ALGORITHMS`, `TLS13_CIPHERS`, `PROXY_CAPATH`, `SSLENGINE` with a name, `SSLENGINE_DEFAULT` and
`TCP_FASTOPEN` answer `NotBuiltIn`; under OpenSSL `SSLENGINE` with a name answers `SslEngineNotFound`, Curl having no
engines. `KRBLEVEL` and `PROXYTYPE` 8 answer `NotBuiltIn` everywhere.

`Options/Callbacks` implements `AddCallbackSetters` once, calling one partial method per callback area
(`AddReadWriteCallbackSetters`, `AddProgressCallbackSetters` and the rest), which each area implements in
an `EasyOptionSetters` part in `Libcurl/Callbacks/<Area>`, keeping the namespace `Libcurl.Options`
(ADR-0573). `Callbacks/ReadWrite` (not `Data`: the `data/` path rule refuses that folder name) sets
`CURLOPT_WRITEFUNCTION`, `READFUNCTION` and `HEADERFUNCTION` (their own delegate type or `null`, any other
delegate `BadFunctionArgument`) and the `*DATA` pointers: the delegate's closure is its user data, so a
`Stream` is the `FILE *` of libcurl's default function (`StreamDataCallbacks`, unless the caller's own
function is set) and any other value is taken and changes nothing. `Callbacks/Progress` sets
`CURLOPT_PROGRESSFUNCTION`, `XFERINFOFUNCTION` and `DEBUGFUNCTION` the same way, which the engine already
calls (ADR-0543); `XFERINFODATA` (`PROGRESSDATA` is the same option) and `DEBUGDATA` take any value and
change nothing (ADR-0577). `Callbacks/Seek` sets `CURLOPT_SEEKFUNCTION` (a `SeekCallback` or `null`, any
other value `BadFunctionArgument`), which the engine calls to rewind a read-callback upload a redirect or
authentication retry sends again; `SEEKDATA` takes any value and changes nothing (BL-2417, ADR-0609). It sets `CURLOPT_IOCTLFUNCTION` (an `IoctlCallback` or `null`) the same way (`EasyIoctlCallbackSetters`), which the engine calls with `RestartRead` for that rewind when no seek callback is set; `IOCTLDATA` takes any value and changes nothing (BL-2607). `Callbacks/Sockets` sets
`CURLOPT_SOCKOPTFUNCTION`, `OPENSOCKETFUNCTION` and `CLOSESOCKETFUNCTION` the same way, which the engine calls around each
TCP connection over opaque `CurlSocket` handles; their `*DATA` options take any value and change nothing (BL-2418, ADR-0610).
`Callbacks/Phases` sets `CURLOPT_PREREQFUNCTION` and `RESOLVER_START_FUNCTION` the same way, which the engine calls as
each request is given its connection and before each look-up its DNS cache misses; `PREREQDATA` and
`RESOLVER_START_DATA` take any value and change nothing, and `TRAILERFUNCTION` takes a `TrailerCallback` the same way,
which the engine calls once a chunked HTTP/1.1 upload's body ends, sending its lines in the closing chunk; `TRAILERDATA`
takes any value and changes nothing (BL-2419, ADR-0611; BL-2475, ADR-0646). `Callbacks/Wildcard` sets `CURLOPT_CHUNK_BGN_FUNCTION`, `CHUNK_END_FUNCTION`, `FNMATCH_FUNCTION`
and `INTERLEAVEFUNCTION` the same way, which the engine calls around each file of an FTP wildcard download under
`CURLOPT_WILDCARDMATCH` and for each interleaved RTP frame of an RTSP transfer; `CHUNK_DATA`, `FNMATCH_DATA` and
`INTERLEAVEDATA` take any value and change nothing (BL-2420, ADR-0619). `Callbacks/Hsts` sets `CURLOPT_HSTSREADFUNCTION`
and `HSTSWRITEFUNCTION` the same way, which the engine calls, while `CURLOPT_HSTS_CTRL` enables HSTS or `CURLOPT_HSTS`
is set, for entries before each perform and with every entry at `curl_easy_cleanup`; `HSTSREADDATA` and
`HSTSWRITEDATA` take any value and change nothing (BL-2421, ADR-0620).

`Misc/ErrorText` holds `CurlErrorText`: `Multi`, `Share` and `Url` are `curl_multi_strerror`,
`curl_share_strerror` and `curl_url_strerror` with upstream's test1538 strings and fallbacks, and
`Header` is the header API's text, which upstream has no function for (ADR-0562). Each is a table
lookup, so a new upstream code is one appended string. `Easy` is `curl_easy_strerror` over
`CurlEasyErrorText`, the `CURLE_*` table keyed by `CurlExitCode` (`"Unknown error"` for a code it does
not name), which moved here from `Curl.Console` (BL-2450); `Curl.Console` references Libcurl for it,
as the tool prints the same texts. The gap office's exit-code measurement reads
`Misc/ErrorText/CurlEasyErrorText.cs` by path and parses its `[CurlExitCode.X] = "..."` lines
(BL-2449), so keep the file's name and that line shape.

`Misc/DatesAndLists` holds `CurlDate` and `CurlSlist` (ADR-0578). `CurlDate.Parse` is `curl_getdate`
over `Curl.Protocol.Abstractions`' `CurlDateParser` (the plan's "Curl.Core date parser"), answering -1
for text that is not a date. `CurlSlist` is `struct curl_slist` as an `IReadOnlyList<string>`, with
`Append` (`curl_slist_append`, a `null` list starting one) and `FreeAll` (`curl_slist_free_all`,
emptying it). Every list-valued option setter copies the list when it is set, so a list changed or
freed afterwards leaves the option as set; a new list setter must copy too.

`Misc/Version` holds `CurlVersion` (ADR-0591): `Text(library)` is `curl_version`, `libcurl/<version>`
and the engine's TLS backend, the tail of `curl -V`'s first line; `Info(library, age)` is
`curl_version_info`, which ignores the age as libcurl does and answers a `CurlVersionInfoData` of
`CurlVersionAge.Now` from `ICurlEngine.VersionInfo`, its `CurlVersionFeatures` bits the OR of
`FeatureBit` over the feature names (`lib/version.c`'s table) and every library version the seam lacks
`null` or 0. Both answer after `curl_global_cleanup`.
`Misc/Strings` holds `CurlStrings` (ADR-0595): `Escape` and `Unescape` are `curl_easy_escape` and
`curl_easy_unescape` (their handle-less forms the `[Obsolete]` `curl_escape` and `curl_unescape`),
reading text as UTF-8 bytes with lengths in bytes, `Unescape` answering `byte[]`; `null`, a negative
length or one past the end answers `null`. `GetEnv` is `curl_getenv` (`null` for unset or empty), and
`StrEqual`/`StrNEqual` are `curl_strequal`/`curl_strnequal`, ignoring ASCII case only.
`Misc/Printf` holds `CurlPrintf` (ADR-0668): `MPrintf`, `MFPrintf` (a `Stream`), `MSPrintf`, `MSNPrintf`
(a `Span<byte>` whose length is `maxlength`) and `MAPrintf`, a port of curl 8.22.0's `lib/mprintf.c`
(`PrintfFormatParser` is `parsefmt`, `PrintfRenderer` `formatf`, `PrintfNumberWriter`, `PrintfTextWriter` and
`PrintfDoubleWriter` its `out_*`). Each argument is read as its conversion's C type, `%ld` 32 bits on Windows
and 64 elsewhere (`PrintfPlatform`, which tests pin both ways); the `v` forms are the same methods given an
array. `out_double`'s `snprintf` is hand-written over `ExactDecimal`, so doubles format the same everywhere.

`Easy` holds `CurlEasy`, the easy handle `CurlLibrary.EasyInit` (`curl_easy_init`) returns over the
engine's `IEasyTransfer` (ADR-0563): `SetOption` goes through `EasyOptionDispatch` and writes the
changed `EasyOptions` back to the transfer only on `Ok`; `PerformAsync` is the native shape, and
`Perform` blocks on it; `Upkeep` (`curl_easy_upkeep`) blocks on `UpkeepAsync` the same way, as `Recv`
(`curl_easy_recv`) and `Send` (`curl_easy_send`) block on `RecvAsync` and `SendAsync`, which pass straight to
the transfer's `IConnectOnlyConnection` and never wait for the peer (ADR-0608); `Recv` and `Send` keep libcurl's
`*n` out parameter, 0 unless `Ok`, and every other answer (`Again`, `UnsupportedProtocol` with no connect-only
connection) is the engine's. These and `WebSocket`'s blocking calls are Libcurl's only sanctioned sync-over-async bridges. `Pause` is `curl_easy_pause` and passes the
engine only the `Receive` and `Send` bits, dropping the rest as libcurl does. A perform clears the
`CURLOPT_ERRORBUFFER` buffer, if one is set, and on failure writes the engine's `ErrorText` into it
cut to 255 UTF-8 bytes (`EasyErrorBuffer`, libcurl's 256-byte `CURL_ERROR_SIZE` less the NUL);
when the engine gives no detail (empty `ErrorText`) the buffer stays empty, never `curl_easy_strerror`'s
text, as libcurl 8.22.0 writes it only through `failf` (`lib/curl_trc.c`) and nowhere falls back (BL-2464);
`CurlEasy.ErrorText` answers the same cut text with or without a buffer. `Dispose` and
`DisposeAsync` are `curl_easy_cleanup` and dispose the transfer once; a member used afterwards throws
`ObjectDisposedException`. `GetInfo` is `curl_easy_getinfo` and goes through `Info`'s dispatch.
`Reset` is `curl_easy_reset` and `DupHandle` is `curl_easy_duphandle`, each a call to the transfer's
`Reset` or `Duplicate`: what survives each (connections kept by a reset, only the options carried by a
duplicate) is the engine's contract on `IEasyTransfer`, not Libcurl's. `SslsImport` and `SslsExport`
(`CurlEasy.SslSessions.cs`, BL-2669) are `curl_easy_ssls_import` and `curl_easy_ssls_export`, straight calls to the
transfer's `ImportTlsSession` and `ExportTlsSessions` (the share's session cache while it shares
`CURL_LOCK_DATA_SSL_SESSION`, else the engine's; `NotBuiltIn` where the build lists no `SSLS-EXPORT`); a `null`
packet is passed as an empty one, and a `null` `CurlSslsExportCallback` answers `BadFunctionArgument` without the engine.

`WebSocket` holds `CurlEasyWebSocket`, libcurl's WebSocket API as `CurlEasy` extension methods over the
transfer's `IEasyWebSocket` (ADR-0585): `WebSocketRecv` (`curl_ws_recv`), `WebSocketSend` (`curl_ws_send`)
and `WebSocketStartFrame` (`curl_ws_start_frame`) block on their `Async` forms and keep libcurl's out
parameters, `WebSocketRecv` answering 0 bytes and no frame unless `Ok`; `WebSocketMeta` is `curl_ws_meta`. A
`null` or cleaned-up handle answers `BadFunctionArgument` (`WebSocketMeta` `null`) without the engine;
every other check is the engine's.

`Headers` holds `CurlEasyHeaders`, libcurl's header API as `CurlEasy` extension methods over the
transfer's `ITransferHeaders` (ADR-0603): `Header` is `curl_easy_header` and `NextHeader`
`curl_easy_nextheader`. A `null` or cleaned-up handle, or a `null` name, answers `BadArgument`
(`NextHeader` `null`) without the engine; every other `CURLHE_*` answer is the engine's.

`Multi` holds `CurlMulti`, the multi handle `CurlLibrary.MultiInit` (`curl_multi_init`) returns over
the engine's `IMultiTransfer` (ADR-0570). It is `partial`: each multi area adds its members in its own
folder under `Libcurl/Multi`, keeping the namespace `Libcurl.Multi`. `Multi/Core` has `AddHandle`,
`RemoveHandle`, `PerformAsync`, `InfoRead` and `DisposeAsync` (`curl_multi_cleanup`); it maps each
added engine transfer back to its `CurlEasy`, so `InfoRead` answers a `CurlMessage` naming the handle
the caller added. A `null` or cleaned-up `CurlEasy` answers `BadEasyHandle`, one this multi holds
already `AddedAlready`, both without calling the engine. `Multi/Settings` has `SetOption`
(`curl_multi_setopt`, every `CurlMultiOption` through `MultiOptionSetters` with `multi.c`'s checks,
writing `MultiOptions` back only on `Ok`), `GetHandles` (the engine's transfers mapped to their
`CurlEasy`), `WaitFds`, `GetInfo` (`curl_multi_get_offt`) and `NotifyEnable`/`NotifyDisable`; a count
or notification `multi.h` lacks answers `UnknownOption` without calling the engine (ADR-0582).
`Multi/Wait` has `WaitAsync`, `PollAsync`, `Wakeup`, `Timeout` and `FdSet` (ADR-0584): a negative
`timeout_ms` answers `BadFunctionArgument` without the engine, and `FdSet` fills `ISet<CurlSocket>`
read, write and exception sets from the engine's `GetWaitSockets` (`curl_multi_waitfds`), since the
seam has no `fdset`. `Multi/Sockets` has `SocketActionAsync`
(`curl_multi_socket_action`) and `Assign` (`curl_multi_assign`), straight calls to the engine, and the
internal `SetSocketFunction` and `SetTimerFunction`, which `SetOption` calls for a `SOCKETFUNCTION` or
`TIMERFUNCTION` value of Libcurl's own `CurlMultiSocketCallback` (given the added `CurlEasy`) or
`CurlMultiTimerCallback` (given the `CurlMulti`): they wrap it into the seam's delegate (ADR-0588).

`Share` holds `CurlShare`, the share handle `CurlLibrary.ShareInit` (`curl_share_init`) returns over
the engine's `IShareHandle`, and `CurlShareOption` (ADR-0581). `SetOption` is `curl_share_setopt`:
`SHARE`/`UNSHARE` of `COOKIE` to `HSTS` and the seam's lock delegates go to the engine, `NONE`, `SHARE`,
an unknown kind or option, or a value of the wrong type answer `BadOption` without it, and `USERDATA` is
kept as `UserData`. `Cleanup` is `curl_share_cleanup`; after the engine's `Ok` every call answers
`Invalid`. `CURLOPT_SHARE` (`Options/Behaviour`) stores a `CurlShare`'s engine handle, and a cleaned-up
`CurlShare` or `null` detaches.

`Mime/Mime` holds `CurlMime` (`curl_mime`, from `CurlLibrary.MimeInit`) and `CurlMimePart` (from
`AddPart`), namespace `Libcurl.Mime` (ADR-0583): each `curl_mime_*` part setter is one call to the
engine's `IMimePartBuilder`, whose answer it returns; `Dispose` is `curl_mime_free`, after which the
handle and its parts throw `ObjectDisposedException`. It implements `EasyOptionSetters`'
`AddMimePostSetters`: `CURLOPT_MIMEPOST` takes a `CurlMime` (else `BadFunctionArgument`) and stores its
body as it is when set, or for `null` an empty `HttpPost` form, a zero-byte POST (upstream lib589,
BL-2620); either clears `CURLOPT_NOBODY`; `CurlEasy` remembers the structure and writes its body as it is now into the options at each perform and `DupHandle`, or, once it is freed, an empty `HttpPost` form, a zero-length post (ADR-0675). `Mime/Form` holds the deprecated form API's `CurlForm`, marked `[Obsolete]`
as upstream deprecates it (ADR-0586): `CurlForm.Add(library, ref form, ...)` is `curl_formadd`, every
`CURLFORM_*` argument passed to the engine's `ICurlFormBuilder` as given, a `null` form started over a
new builder and set only on `Ok`; `Get` is `curl_formget`, and `Free` or `Dispose` `curl_formfree`,
after which the form throws `ObjectDisposedException`. It implements `AddFormPostSetters`:
`CURLOPT_HTTPPOST` takes a `CurlForm` (else `BadFunctionArgument`) and stores its body as it is when set,
or for `null` an empty form, which the engine posts with `Content-Length: 0` (upstream test516, BL-2599);
either clears `CURLOPT_NOBODY`.

`Url` holds `CurlUrlHandle`, the URL handle `CurlLibrary.UrlInit` (`curl_url`) returns over the
engine's `ICurlUrlHandle` (ADR-0587): `Get` and `Set` (`curl_url_get`, `_set`) pass through to the
engine and answer its code, `GetBytes` answering `Get`'s part as the C ABI's raw bytes (a decoded `%FF`
the byte 0xFF, ADR-0877), `Duplicate` (`curl_url_dup`) wraps the engine's copy in a new handle, and
`ErrorText` is `curl_url_strerror` over `CurlErrorText.Url`. `Dispose` is `curl_url_cleanup` and
disposes the engine handle once; a member used afterwards throws `ObjectDisposedException`. The internal
`EngineHandle` is what `CURLOPT_CURLU` stores (ADR-0590).

`Info` holds `CurlInfo` (every `CURLINFO_*` of curl 8.22.0's `curl.h` by name and number) and the
internal `EasyInfoDispatch` `CurlEasy.GetInfo` goes through (ADR-0566): an id `curl.h` lacks answers
`UnknownOption`, a known info no group reads yet `NotBuiltIn`. Each info group adds its reader rows by
implementing its `Add<Group>Readers` partial method in an `EasyInfoReaders` part in `Info/<Group>`,
keeping the namespace `Libcurl.Info`, with each reader a named method in an explicit
`new EasyInfoReader(...)` (a lambda or cached method group counts against the adding method's
complexity). `Info/Strings` reads the strings, lists, `PRIVATE` and `TLS_SSL_PTR`, which answers a
`CurlTlsSessionInfo` with the build's backend and no native pointer. `Info/Times` reads the times,
sizes, speeds, content lengths, `RETRY_AFTER` and `EARLYDATA_SENT_T`: a time's `_T` form is its
`TimeSpan` in whole (truncated) microseconds and its double form that count over 1,000,000, as
`getinfo.c` derives both; a size or speed is the same `long` in both forms. `Info/Longs` reads every `_LONG` info, the
`_SOCKET` `ACTIVESOCKET` and the `_OFF_T` `FILETIME_T`, `XFER_ID` and `CONN_ID`, each the
`TransferInfo` `long` as it is (`FILETIME` and `FILETIME_T` the same seconds); the two sockets stay -1.

Reference `Curl.Api.UnitLibrary` and `Curl.Engine.UnitLibrary` only (project references). The engine
reference is for `CurlGlobal.Init(flags)`, the parameterless `curl_global_init`, which builds a
`ManagedCurlEngine` (ADR-0579); every other member takes its `ICurlEngine` from the library, so tests
inject fakes. Never reference `Curl.Api` or a protocol library.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Libcurl.UnitTests`,
which sorts after every `Curl.*` project.
