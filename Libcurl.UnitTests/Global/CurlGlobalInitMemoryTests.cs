using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.Strings;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests.Global;

[TestClass]
public sealed class CurlGlobalInitMemoryTests
{
    [TestMethod]
    public async Task EasyInit_CallsCalloc_AndCleanupFreesTheBlock()
    {
        var allocator = new RecordingAllocator();
        await using var library = allocator.InitMemory();

        var easy = library.EasyInit();
        Assert.AreEqual($"calloc 1 {EasyAllocations.HandleSize}", string.Join(", ", allocator.Calls));
        await easy.DisposeAsync();

        Assert.AreEqual($"calloc 1 {EasyAllocations.HandleSize}, free 1", string.Join(", ", allocator.Calls));
    }

    [TestMethod]
    public async Task SetOption_WithAString_CallsStrdupWithTheString()
    {
        var allocator = new RecordingAllocator();
        await using var library = allocator.InitMemory();
        await using var easy = library.EasyInit();

        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Useragent, "test509"));

        Assert.AreEqual("strdup test509", allocator.Calls[1]);
    }

    [TestMethod]
    public async Task SetOption_WithALong_OrARefusedString_CallsNoCallback()
    {
        var allocator = new RecordingAllocator();
        await using var library = allocator.InitMemory();
        await using var easy = library.EasyInit();

        _ = easy.SetOption(CurlOption.Verbose, 1L);
        Assert.AreNotEqual(CurlCode.Ok, easy.SetOption(CurlOption.Verbose, "text"));

        Assert.AreEqual(1, allocator.Calls.Count);
    }

    [TestMethod]
    public async Task Escape_WithTheHandle_CallsMallocForTheAnswer_AndCleanupFreesEveryBlock()
    {
        var allocator = new RecordingAllocator();
        await using var library = allocator.InitMemory();
        var easy = library.EasyInit();

        Assert.AreEqual("a%20b", CurlStrings.Escape(easy, "a b", 0));
        Assert.IsNull(CurlStrings.Escape(easy, null, 0));
        await easy.DisposeAsync();

        Assert.AreEqual($"calloc 1 {EasyAllocations.HandleSize}, malloc 6, free 1, free 2", string.Join(", ", allocator.Calls));
    }

    [TestMethod]
    public async Task DupHandle_CallsCallocForTheCopy()
    {
        var allocator = new RecordingAllocator();
        await using var library = allocator.InitMemory();
        await using var easy = library.EasyInit();

        await using var copy = easy.DupHandle();

        Assert.AreEqual(2, allocator.Calls.Count(call => call.StartsWith("calloc", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AHandleOfAPlainInit_AllocatesThroughNothing()
    {
        await using var library = CurlGlobal.InitCounting(new FakeCurlEngine { EasyFactory = () => new FakeEasyTransfer() }, CurlGlobalFlags.Default, new GlobalInitCounter());
        await using var easy = library.EasyInit();

        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Useragent, "agent"));
        Assert.AreEqual("a", CurlStrings.Escape(easy, "a", 0));
        await using var copy = easy.DupHandle();
    }

    [TestMethod]
    public void AnAllocationAnswering0_IsNotKeptToFree()
    {
        var freed = 0;
        var allocations = new EasyAllocations(new MemoryCallbacks(_ => 0, _ => freed++, (_, _) => 0, _ => 0, (_, _) => 0));

        allocations.Malloc(1);
        allocations.Strdup("x");
        allocations.FreeAll();

        Assert.AreEqual(0, freed);
    }

    /// <summary>Allocation callbacks that record each call and answer 1, 2, 3 and so on as the blocks.</summary>
    private sealed class RecordingAllocator
    {
        private int next;

        public List<string> Calls { get; } = [];

        public CurlLibrary InitMemory() =>
            CurlGlobal.InitMemoryCounting(
                new FakeCurlEngine { EasyFactory = () => new FakeEasyTransfer() },
                CurlGlobalFlags.All,
                new MemoryCallbacks(Malloc, Free, Realloc, Strdup, Calloc),
                new GlobalInitCounter());

        private nint Malloc(nuint size) => Record($"malloc {size}");

        private void Free(nint pointer) => Calls.Add($"free {pointer}");

        private nint Realloc(nint pointer, nuint size) => Record($"realloc {pointer} {size}");

        private nint Strdup(nint text) => Record($"strdup {System.Runtime.InteropServices.Marshal.PtrToStringUTF8(text)}");

        private nint Calloc(nuint count, nuint size) => Record($"calloc {count} {size}");

        private nint Record(string call)
        {
            Calls.Add(call);
            return ++next;
        }
    }
}
