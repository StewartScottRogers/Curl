// Runs one MIME edge case through libcurl's curl_mime_* API and curl_easy_perform, then exits with the CURLcode.
// Usage: dotnet run Record-LibcurlMimeExchange.cs -- <case> <url> [libcurl-path]
//   case 1: part "a" curl_mime_filedata("nope.txt") plus data part "b"
//   case 2: part "a" data "h\xC3\xA9llo" with curl_mime_encoder("7bit")
//   case 3: part "a" with curl_mime_subparts (one data subpart "b") and curl_mime_encoder("base64")
//   case 4: case 1's data part alone, with "Transfer-Encoding: chunked" in CURLOPT_HTTPHEADER
// libcurl-path defaults to the Git for Windows build, C:\Program Files\Git\ucrt64\bin\libcurl-4.dll (libcurl 8.22.0, Schannel).
using System.Runtime.InteropServices;
using System.Text;

const int CurlOptUrl = 10002;
const int CurlOptHttpHeader = 10023;
const int CurlOptMimePost = 10269;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: dotnet run Record-LibcurlMimeExchange.cs -- <case 1-4> <url> [libcurl-path]");
    return 2;
}

string libraryPath = args.Length > 2 ? args[2] : @"C:\Program Files\Git\ucrt64\bin\libcurl-4.dll";
nint library = NativeLibrary.Load(libraryPath);

T Import<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

var globalInit = Import<GlobalInit>("curl_global_init");
var easyInit = Import<EasyInit>("curl_easy_init");
var setOptPointer = Import<SetOptPointer>("curl_easy_setopt");
var perform = Import<Perform>("curl_easy_perform");
var mimeInit = Import<MimeInit>("curl_mime_init");
var mimeAddPart = Import<MimeAddPart>("curl_mime_addpart");
var mimeName = Import<MimeText>("curl_mime_name");
var mimeFileData = Import<MimeText>("curl_mime_filedata");
var mimeEncoder = Import<MimeText>("curl_mime_encoder");
var mimeData = Import<MimeData>("curl_mime_data");
var mimeSubparts = Import<MimeSubparts>("curl_mime_subparts");
var slistAppend = Import<SlistAppend>("curl_slist_append");

globalInit(3);
nint easy = easyInit();
nint mime = mimeInit(easy);

nint AddDataPart(nint owner, string name, byte[] data)
{
    nint part = mimeAddPart(owner);
    mimeName(part, name);
    nint buffer = Marshal.AllocHGlobal(data.Length);
    Marshal.Copy(data, 0, buffer, data.Length);
    mimeData(part, buffer, data.Length);
    return part;
}

switch (args[0])
{
    case "1":
    case "4":
        if (args[0] == "1")
        {
            nint filePart = mimeAddPart(mime);
            mimeName(filePart, "a");
            mimeFileData(filePart, "nope.txt");
        }
        AddDataPart(mime, "b", Encoding.ASCII.GetBytes("hello"));
        if (args[0] == "4")
        {
            setOptPointer(easy, CurlOptHttpHeader, slistAppend(0, "Transfer-Encoding: chunked"));
        }
        break;
    case "2":
        nint encoded = AddDataPart(mime, "a", [0x68, 0xC3, 0xA9, 0x6C, 0x6C, 0x6F]);
        mimeEncoder(encoded, "7bit");
        break;
    case "3":
        nint outer = mimeAddPart(mime);
        mimeName(outer, "a");
        nint subMime = mimeInit(easy);
        AddDataPart(subMime, "b", Encoding.ASCII.GetBytes("hello"));
        mimeSubparts(outer, subMime);
        mimeEncoder(outer, "base64");
        break;
    default:
        Console.Error.WriteLine("case must be 1, 2, 3 or 4");
        return 2;
}

nint url = Marshal.StringToHGlobalAnsi(args[1]);
setOptPointer(easy, CurlOptUrl, url);
setOptPointer(easy, CurlOptMimePost, mime);
int code = perform(easy);
Console.Error.WriteLine($"CURLcode {code}");
return code;


[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GlobalInit(long flags);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate nint EasyInit();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SetOptPointer(nint easy, int option, nint value);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Perform(nint easy);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate nint MimeInit(nint easy);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate nint MimeAddPart(nint mime);
[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] delegate int MimeText(nint part, string text);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int MimeData(nint part, nint data, nint size);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int MimeSubparts(nint part, nint subparts);
[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] delegate nint SlistAppend(nint list, string text);
