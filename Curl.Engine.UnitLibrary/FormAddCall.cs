using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;

namespace Curl.Engine;

/// <summary>
/// One <c>curl_formadd</c> call's options read as curl 8.22.0's <c>FormAdd</c> in <c>lib/formdata.c</c> reads them:
/// each <c>CURLFORM_*</c> option sets a field of the current <see cref="FormFieldInfo" /> node, a further
/// <c>CURLFORM_FILE</c> or <c>CURLFORM_CONTENTTYPE</c> after a file adds a node for one more file, an option given
/// twice for one node is <see cref="CurlFormAddCode.OptionTwice" />, a missing value <see cref="CurlFormAddCode.Null" />,
/// a <c>CURLFORM_ARRAY</c> inside an array <see cref="CurlFormAddCode.IllegalArray" /> and any other option
/// <see cref="CurlFormAddCode.UnknownOption" />; <see cref="CurlFormOption.End" /> ends the call or the array.
/// </summary>
internal sealed class FormAddCall
{
    private static readonly Dictionary<CurlFormOption, Func<FormAddCall, object?, CurlFormAddCode>> OptionReaders = new()
    {
        [CurlFormOption.CopyName] = static (call, value) => call.ReadName(value),
        [CurlFormOption.PointerName] = static (call, value) => call.ReadName(value),
        [CurlFormOption.NameLength] = static (call, value) => call.ReadNameLength(value),
        [CurlFormOption.CopyContents] = static (call, value) => call.ReadContents(value, FormFieldFlags.None),
        [CurlFormOption.PointerContents] = static (call, value) => call.ReadContents(value, FormFieldFlags.PointerContents),
        [CurlFormOption.ContentsLength] = static (call, value) => call.ReadContentsLength(value),
        [CurlFormOption.ContentLength] = static (call, value) => call.ReadContentsLength(value),
        [CurlFormOption.FileContent] = static (call, value) => call.ReadFileContent(value),
        [CurlFormOption.File] = static (call, value) => call.ReadFile(value),
        [CurlFormOption.BufferPointer] = static (call, value) => call.ReadBufferPointer(value),
        [CurlFormOption.BufferLength] = static (call, value) => call.ReadBufferLength(value),
        [CurlFormOption.Stream] = static (call, value) => call.ReadStream(value),
        [CurlFormOption.ContentType] = static (call, value) => call.ReadContentType(value),
        [CurlFormOption.ContentHeader] = static (call, value) => call.ReadContentHeader(value),
        [CurlFormOption.FileName] = static (call, value) => call.ReadShowFileName(value),
        [CurlFormOption.Buffer] = static (call, value) => call.ReadShowFileName(value),
    };

    private readonly List<FormFieldInfo> nodes = [new()];
    private int current;

    /// <summary>Gets the call's nodes in order, the field first.</summary>
    internal IReadOnlyList<FormFieldInfo> Nodes => nodes;

    private FormFieldInfo Current => nodes[current];

    /// <summary>Reads <paramref name="arguments" /> into a new call's nodes.</summary>
    /// <param name="arguments">The call's options in order.</param>
    /// <param name="call">The call, its nodes as far as the options were read.</param>
    /// <returns><see cref="CurlFormAddCode.Ok" />, or the first option's failure.</returns>
    internal static CurlFormAddCode Read(IReadOnlyList<CurlFormArgument> arguments, out FormAddCall call)
    {
        call = new FormAddCall();
        foreach (CurlFormArgument argument in arguments.TakeWhile(argument => argument.Option != CurlFormOption.End))
        {
            CurlFormAddCode code = argument.Option == CurlFormOption.Array ? call.ReadArray(argument.Value) : call.ReadOption(argument);
            if (code != CurlFormAddCode.Ok)
            {
                return code;
            }
        }

        return CurlFormAddCode.Ok;
    }

    private static long LengthOf(object? value) => value as long? ?? 0;

    private CurlFormAddCode ReadArray(object? value)
    {
        if (value is not IReadOnlyList<CurlFormArgument> forms)
        {
            return CurlFormAddCode.Null;
        }

        foreach (CurlFormArgument form in forms.TakeWhile(form => form.Option != CurlFormOption.End))
        {
            CurlFormAddCode code = form.Option == CurlFormOption.Array ? CurlFormAddCode.IllegalArray : ReadOption(form);
            if (code != CurlFormAddCode.Ok)
            {
                return code;
            }
        }

        return CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadOption(CurlFormArgument argument) =>
        OptionReaders.TryGetValue(argument.Option, out Func<FormAddCall, object?, CurlFormAddCode>? read)
            ? read(this, argument.Value)
            : CurlFormAddCode.UnknownOption;

    private CurlFormAddCode ReadName(object? value)
    {
        if (Current.Name is not null)
        {
            return CurlFormAddCode.OptionTwice;
        }

        Current.Name = value as string;
        return Current.Name is null ? CurlFormAddCode.Null : CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadNameLength(object? value)
    {
        if (Current.NameLength != 0)
        {
            return CurlFormAddCode.OptionTwice;
        }

        Current.NameLength = LengthOf(value);
        return CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadContents(object? value, FormFieldFlags pointer)
    {
        Current.Flags |= pointer;
        if (Current.Value is not null)
        {
            return CurlFormAddCode.OptionTwice;
        }

        Current.Value = value is string or byte[]? value : null;
        return Current.Value is null ? CurlFormAddCode.Null : CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadContentsLength(object? value)
    {
        Current.ContentsLength = LengthOf(value);
        return CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadFileContent(object? value)
    {
        if (Current.HasAny(FormFieldFlags.PointerContents | FormFieldFlags.ReadFile))
        {
            return CurlFormAddCode.OptionTwice;
        }

        if (value is not string path)
        {
            return CurlFormAddCode.Null;
        }

        Current.Value = path;
        Current.Flags |= FormFieldFlags.ReadFile;
        return CurlFormAddCode.Ok;
    }

    /// <summary>
    /// <c>CURLFORM_FILE</c>: the node's file, or, when the node already has a file, a new node after it for one
    /// more file of the field; a node holding any other value answers <see cref="CurlFormAddCode.OptionTwice" />.
    /// </summary>
    private CurlFormAddCode ReadFile(object? value)
    {
        if (Current.Value is not null && !Current.HasAny(FormFieldFlags.FileName))
        {
            return CurlFormAddCode.OptionTwice;
        }

        if (value is not string path)
        {
            return CurlFormAddCode.Null;
        }

        if (Current.Value is null)
        {
            Current.Value = path;
            Current.Flags |= FormFieldFlags.FileName;
        }
        else
        {
            AddFileNode().Value = path;
        }

        return CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadBufferPointer(object? value)
    {
        Current.Flags |= FormFieldFlags.Buffer;
        if (Current.Buffer is not null)
        {
            return CurlFormAddCode.OptionTwice;
        }

        Current.Buffer = value as byte[];
        Current.Value = Current.Buffer;
        return Current.Buffer is null ? CurlFormAddCode.Null : CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadBufferLength(object? value)
    {
        if (Current.BufferLength != 0)
        {
            return CurlFormAddCode.OptionTwice;
        }

        Current.BufferLength = LengthOf(value);
        return CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadStream(object? value)
    {
        Current.Flags |= FormFieldFlags.Callback;
        if (Current.StreamObject is not null)
        {
            return CurlFormAddCode.OptionTwice;
        }

        Current.StreamObject = value;
        Current.Value = value;
        return value is null ? CurlFormAddCode.Null : CurlFormAddCode.Ok;
    }

    /// <summary>
    /// <c>CURLFORM_CONTENTTYPE</c>: the node's type, or, when a file node already has one, a new node after it
    /// whose file the next <c>CURLFORM_FILE</c> gives; any other node with a type answers
    /// <see cref="CurlFormAddCode.OptionTwice" />.
    /// </summary>
    private CurlFormAddCode ReadContentType(object? value)
    {
        if (Current.ContentType is not null && !Current.HasAny(FormFieldFlags.FileName))
        {
            return CurlFormAddCode.OptionTwice;
        }

        if (value is not string type)
        {
            return CurlFormAddCode.Null;
        }

        FormFieldInfo node = Current.ContentType is null ? Current : AddFileNode();
        node.ContentType = type;
        return CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadContentHeader(object? value)
    {
        if (Current.ContentHeader is not null)
        {
            return CurlFormAddCode.OptionTwice;
        }

        Current.ContentHeader = value as IReadOnlyList<string>;
        return CurlFormAddCode.Ok;
    }

    private CurlFormAddCode ReadShowFileName(object? value)
    {
        if (Current.ShowFileName is not null)
        {
            return CurlFormAddCode.OptionTwice;
        }

        Current.ShowFileName = value as string;
        return Current.ShowFileName is null ? CurlFormAddCode.Null : CurlFormAddCode.Ok;
    }

    /// <summary>Adds a file node right after the current one and makes it current: upstream's <c>AddFormInfo</c>.</summary>
    /// <returns>The new node.</returns>
    private FormFieldInfo AddFileNode()
    {
        FormFieldInfo node = new() { Flags = FormFieldFlags.FileName };
        current++;
        nodes.Insert(current, node);
        return node;
    }
}
