using System.Buffers;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SandboxMenu.Networking;

internal static class SpawnPayloadCodec
{
    internal const int MaxCompressedBytes = 48 * 1024;
    internal const int MaxPayloadBytes = 256 * 1024;
    internal const int MaxEntries = 512;
    internal const int MaxTemplates = 64;
    internal const int MaxProblems = 8;

    private const int MaxDepth = 24;
    private const int MaxProblemLength = 160;

    private static readonly XmlWriterSettings WriterSettings = new()
    {
        Encoding = new UTF8Encoding(false),
        OmitXmlDeclaration = true,
        Indent = false,
        CloseOutput = false
    };

    internal static bool TryEncode(SpawnPayload payload, out byte[] compressed)
    {
        compressed = [];

        try
        {
            XElement xml = payload.ToXml();

            using MemoryStream output = new();

            long xmlLength;

            using (BrotliStream compressor = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                using CountingStream counting = new(compressor);

                using (XmlWriter writer = XmlWriter.Create(counting, WriterSettings)) { xml.WriteTo(writer); }

                xmlLength = counting.Written;
            }

            if (output.Length > MaxCompressedBytes)
            {
                DebugConsole.AddWarning($"Spawn request is too large to send ({xmlLength} bytes of XML compress to {output.Length}).");
                return false;
            }

            compressed = output.ToArray();
            return true;
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Packing the spawn request failed: {e}");
            return false;
        }
    }

    internal static bool TryDecode(byte[]? compressed, out SpawnPayload payload)
    {
        if (compressed is not { Length: > 0 } || compressed.Length > MaxCompressedBytes)
        {
            payload = new SpawnPayload();
            return false;
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(MaxPayloadBytes);
        try
        {
            return TryParse(compressed, buffer, MaxPayloadBytes, out payload);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal static string[] LimitProblems(IReadOnlyList<string> problems)
    {
        int count = Math.Min(problems.Count, MaxProblems);

        if (count == 0) { return []; }

        string[] limited = new string[count];

        for (int i = 0; i < count; i++)
        {
            string problem = problems[i];

            limited[i] = problem.Length > MaxProblemLength ? problem[..MaxProblemLength] : problem;
        }

        return limited;
    }

    private static bool TryParse(byte[] compressed, byte[] buffer, int limit, out SpawnPayload payload)
    {
        payload = new SpawnPayload();

        int length;
        try
        {
            length = Decompress(compressed, buffer, limit);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Unpacking a spawn request failed: {e}");
            return false;
        }

        if (length < 0)
        {
            DebugConsole.AddWarning($"A spawn request unpacked to more than {MaxPayloadBytes} bytes.");
            return false;
        }

        if (length == 0) { return false; }

        using MemoryStream stream = new(buffer, 0, length, writable: false);

        XElement? root;
        try
        {
            using XmlReader reader = XMLExtensions.CreateReader(stream);
            root = XDocument.Load(reader).Root;
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"A spawn request carried XML that could not be read: {e}");
            return false;
        }

        if (root is null || root.Name.LocalName != SpawnPayload.RootName) { return false; }

        if (!WithinDepthLimit(root))
        {
            DebugConsole.AddWarning("A spawn request nested its entries deeper than the mod accepts.");
            return false;
        }

        payload = SpawnPayload.FromXml(root);

        if (payload.TemplateCount > MaxTemplates || !payload.WithinEntryLimit())
        {
            DebugConsole.AddWarning("A spawn request asked for more entries than the mod accepts.");
            payload = new SpawnPayload();
            return false;
        }

        return true;
    }

    private static int Decompress(byte[] compressed, byte[] buffer, int limit)
    {
        using MemoryStream input = new(compressed, writable: false);
        using BrotliStream decompressor = new(input, CompressionMode.Decompress);

        int total = 0;
        while (total < limit)
        {
            int read = decompressor.Read(buffer, total, limit - total);
            if (read <= 0) { return total; }

            total += read;
        }

        return decompressor.ReadByte() < 0 ? total : -1;
    }

    private static bool WithinDepthLimit(XElement root)
    {
        Stack<(XElement Element, int Depth)> pending = new();

        pending.Push((root, 0));

        while (pending.Count > 0)
        {
            (XElement element, int depth) = pending.Pop();

            if (depth > MaxDepth) { return false; }

            foreach (XElement child in element.Elements()) { pending.Push((child, depth + 1)); }
        }

        return true;
    }

    private sealed class CountingStream(Stream inner) : Stream
    {
        internal long Written { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => Written;

        public override long Position
        {
            get => Written;
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            Written += count;
            inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Written += buffer.Length;
            inner.Write(buffer);
        }
    }
}
