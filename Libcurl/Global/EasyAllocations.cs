using System.Runtime.InteropServices;

namespace Libcurl.Global;

/// <summary>
/// The allocations one easy handle makes through the <c>curl_global_init_mem</c> callbacks (ADR-0664):
/// <c>curl_easy_init</c>'s <c>calloc</c> of the handle (<see cref="ForNewHandle"/>), a <see cref="Strdup"/> of each string option
/// set and a <see cref="Malloc"/> of each <c>curl_easy_escape</c> answer, all freed by <see cref="FreeAll"/> at
/// <c>curl_easy_cleanup</c>. Over no callbacks (a <c>curl_global_init</c> library) every call does nothing.
/// </summary>
/// <param name="callbacks">The library's callbacks, or <see langword="null"/> when it has none.</param>
internal sealed class EasyAllocations(MemoryCallbacks? callbacks)
{
    /// <summary>The bytes <c>curl_easy_init</c> asks <c>calloc</c> for: one handle-sized block.</summary>
    internal const int HandleSize = 64;

    private readonly List<nint> blocks = [];

    /// <summary>Allocations that call no callback, for handles of a <c>curl_global_init</c> library.</summary>
    internal static EasyAllocations None { get; } = new(null);

    /// <summary>A new handle's allocations, its own <c>calloc</c> made; <see cref="None"/> over no callbacks.</summary>
    /// <param name="callbacks">The library's callbacks, or <see langword="null"/>.</param>
    /// <returns>The handle's allocations.</returns>
    internal static EasyAllocations ForNewHandle(MemoryCallbacks? callbacks)
    {
        if (callbacks is null)
        {
            return None;
        }

        var allocations = new EasyAllocations(callbacks);
        allocations.Keep(callbacks.Calloc(1, HandleSize));
        return allocations;
    }

    /// <summary>The allocations of a <c>curl_easy_duphandle</c> copy: a new handle's, over the same callbacks.</summary>
    /// <returns>The copy's allocations.</returns>
    internal EasyAllocations ForDuplicate() => ForNewHandle(callbacks);

    /// <summary><c>malloc(size)</c> through the callback, kept until <see cref="FreeAll"/>.</summary>
    /// <param name="size">The bytes to allocate.</param>
    internal void Malloc(nuint size) => Keep(callbacks?.Malloc(size));

    /// <summary><c>strdup(text)</c> through the callback, kept until <see cref="FreeAll"/>.</summary>
    /// <param name="text">The string to copy.</param>
    internal void Strdup(string text)
    {
        if (callbacks is null)
        {
            return;
        }

        nint source = Marshal.StringToCoTaskMemUTF8(text);
        try
        {
            Keep(callbacks.Strdup(source));
        }
        finally
        {
            Marshal.FreeCoTaskMem(source);
        }
    }

    /// <summary>Frees every kept block through the <c>free</c> callback, as <c>curl_easy_cleanup</c> does.</summary>
    /// <remarks>
    /// Over no callbacks it touches nothing: <see cref="None"/> is shared by every handle of a <c>curl_global_init</c>
    /// library, and even an empty <see cref="List{T}.Clear"/> bumps the list's version, so handles cleaned up at once
    /// would break each other's enumeration.
    /// </remarks>
    internal void FreeAll()
    {
        if (callbacks is null)
        {
            return;
        }

        foreach (nint block in blocks)
        {
            callbacks.Free(block);
        }

        blocks.Clear();
    }

    /// <summary>Remembers a block for <see cref="FreeAll"/>; a callback that answered null or a null pointer (a failed allocation) leaves nothing to free.</summary>
    /// <param name="block">The pointer the callback returned, or <see langword="null"/> when there were no callbacks.</param>
    private void Keep(nint? block)
    {
        if (block is { } allocated && allocated != 0)
        {
            blocks.Add(allocated);
        }
    }
}
