# libcurl MIME edge-case recordings (BL-3210)

What upstream libcurl 8.22.0 sends and returns for four MIME edge cases that the curl tool cannot
reach (it rejects `-F a=@nope.txt` and `-F "a=<file;encoder=7bit"` over non-ASCII with exit 26 before
connecting). Recorded for BL-3211 to BL-3214 to pin Curl's `Libcurl` against. Every byte and code
below was measured, not assumed.

## How they were recorded

- Library: `libcurl-4.dll` from Git for Windows (`C:\Program Files\Git\ucrt64\bin`), libcurl 8.22.0
  with Schannel, loaded with `NativeLibrary.Load` and called by P/Invoke only (no package).
- Harness: `Record-LibcurlMimeExchange.cs`, one case per run, exit code = the CURLcode:
  `dotnet run Record-LibcurlMimeExchange.cs -- <case> http://127.0.0.1:<port>/`.
- Recorder: `Record-CurlExchange.ps1 -Curl dotnet -CurlArgs run,Record-LibcurlMimeExchange.cs,--,<case>,http://127.0.0.1:<port>/ -Port <port> -OutDirectory <dir>`
  (default `HTTP/1.1 200 OK` empty response). `request.bin` holds the bytes below.
- Boundaries are random (`------------------------` plus 22 characters); only their length is fixed.
  Header block (request line to blank line) is 168 bytes in cases 1 to 3 and 175 in case 4.
- Part names: the file or first data part is `a`, the next data part `b`; the data is `hello`.

## Results

| Case | Setup | CURLcode | Body length |
| --- | --- | --- | --- |
| 1 | `a`: `curl_mime_filedata("nope.txt")`; `b`: data `hello` | 0 | 249 (Content-Length) |
| 2 | `a`: data `h\xC3\xA9llo`, `curl_mime_encoder("7bit")` | 26 (CURLE_READ_ERROR) | 187 declared, 128 sent |
| 3 | `a`: `curl_mime_subparts` (one data subpart `b`), `curl_mime_encoder("base64")` | 0 | 483 (Content-Length) |
| 4 | case 1's `b` alone, header `Transfer-Encoding: chunked` | 0 | 153 in one chunk (`99`) |

## Request bytes

`<B>` is the boundary. Lines end `\r\n`.

### Case 1: unreadable file part

The missing file is not an error: the part is sent with an empty body and the code is 0.

```
POST / HTTP/1.1
Host: 127.0.0.1:P
Accept: */*
Content-Length: 249
Content-Type: multipart/form-data; boundary=<B>

--<B>
Content-Disposition: form-data; name="a"

(empty line, then the CRLF before the next boundary)
--<B>
Content-Disposition: form-data; name="b"

hello
--<B>--
```

No `filename=` and no `Content-Type` is sent for the `a` part.

### Case 2: 7bit encoder over non-ASCII

The headers go out with `Content-Length: 187`, then the part's headers
(`Content-Disposition: form-data; name="a"`, `Content-Transfer-Encoding: 7bit`, blank line) and
one body byte, `h` (0x68). The `\xC3` that follows is not 7-bit, the read callback fails, the
transfer stops with CURLcode 26 and the connection is closed. Total bytes on the wire: 296
(168 headers + 128 body). No closing boundary is sent.

### Case 3: base64 encoder over subparts

```
POST / HTTP/1.1
Host: 127.0.0.1:P
Accept: */*
Content-Length: 483
Content-Type: multipart/form-data; boundary=<B>

--<B>
Content-Disposition: form-data; name="a"
Content-Type: multipart/mixed; boundary=<B2>
Content-Transfer-Encoding: base64

<base64 of the whole subpart block, 76-character lines, CRLF line ends: three lines here>
--<B>--
```

The subpart block that is encoded is `--<B2>`, `Content-Disposition: attachment; name="b"`, a blank
line, `hello` and `--<B2>--`, each line ending `\r\n` (the encoded text decodes to exactly that).
The 483-byte body here differs from Stewart's harness (449 bytes) and Curl's (297 bytes): the
subpart's name and the 22-character boundaries change the length, so BL-3213 should pin the
structure above, not a byte count taken from another harness.

### Case 4: chunked transfer encoding

```
POST / HTTP/1.1
Host: 127.0.0.1:P
Accept: */*
Transfer-Encoding: chunked
Content-Type: multipart/form-data; boundary=<B>

99
--<B>
Content-Disposition: form-data; name="b"

hello
--<B>--

0

```

No `Content-Length` is sent. The one chunk is 0x99 = 153 bytes (the multipart body), then the
`0` chunk and the final blank line. Total bytes on the wire: 339 (175 headers + 164 chunk framing
and body).
