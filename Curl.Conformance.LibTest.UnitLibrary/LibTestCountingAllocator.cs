using System.Runtime.InteropServices;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib509.c</c>'s <c>custom_malloc</c>, <c>custom_free</c>, <c>custom_realloc</c>, <c>custom_strdup</c> and
/// <c>custom_calloc</c>: the allocator's own functions over the process heap, each counting its call in
/// <see cref="Seen"/>.
/// </summary>
public sealed class LibTestCountingAllocator
{
    /// <summary>How many times any of the functions was called, upstream's <c>seen</c>.</summary>
    public int Seen { get; private set; }

    /// <summary><c>custom_malloc</c>.</summary>
    /// <param name="size">The bytes to allocate.</param>
    /// <returns>The allocated memory.</returns>
    public nint Malloc(nuint size)
    {
        Seen++;
        return Marshal.AllocHGlobal((nint)size);
    }

    /// <summary><c>custom_free</c>.</summary>
    /// <param name="pointer">The memory to free.</param>
    public void Free(nint pointer)
    {
        Seen++;
        Marshal.FreeHGlobal(pointer);
    }

    /// <summary><c>custom_realloc</c>.</summary>
    /// <param name="pointer">The memory to resize.</param>
    /// <param name="size">Its new size.</param>
    /// <returns>The resized memory.</returns>
    public nint Realloc(nint pointer, nuint size)
    {
        Seen++;
        return Marshal.ReAllocHGlobal(pointer, (nint)size);
    }

    /// <summary><c>custom_strdup</c>.</summary>
    /// <param name="text">The NUL-terminated string to copy.</param>
    /// <returns>The copy.</returns>
    public nint Strdup(nint text)
    {
        Seen++;
        return Marshal.StringToHGlobalAnsi(Marshal.PtrToStringAnsi(text));
    }

    /// <summary><c>custom_calloc</c>.</summary>
    /// <param name="count">The number of elements.</param>
    /// <param name="size">The size of each.</param>
    /// <returns>The zeroed memory.</returns>
    public nint Calloc(nuint count, nuint size)
    {
        Seen++;
        int length = checked((int)(count * size));
        nint memory = Marshal.AllocHGlobal(length);
        Marshal.Copy(new byte[length], 0, memory, length);
        return memory;
    }
}
