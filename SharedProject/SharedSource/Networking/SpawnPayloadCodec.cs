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

    internal static bool TryEncode(SpawnPayload payload, out byte[] compressed)
    {
        compressed = [];

        try
        {
            byte[] xml = Encoding.UTF8.GetBytes(payload.ToXml().ToString(SaveOptions.DisableFormatting));

            using MemoryStream output = new();
            using (BrotliStream compressor = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                compressor.Write(xml, 0, xml.Length);
            }

            if (output.Length > MaxCompressedBytes)
            {
                Log.Warn($"Spawn request is too large to send ({xml.Length} bytes of XML compress to {output.Length}).");
                return false;
            }

            compressed = output.ToArray();
            return true;
        }
        catch (Exception e)
        {
            Log.Warn("Packing the spawn request failed", e);
            return false;
        }
    }

    internal static bool TryDecode(byte[]? compressed, out SpawnPayload payload)
    {
        payload = new SpawnPayload();

        if (compressed is not { Length: > 0 } || compressed.Length > MaxCompressedBytes) { return false; }

        int length;
        byte[] buffer = new byte[MaxPayloadBytes];
        try
        {
            length = Decompress(compressed, buffer);
        }
        catch (Exception e)
        {
            Log.Warn("Unpacking a spawn request failed", e);
            return false;
        }

        if (length < 0)
        {
            Log.Warn($"A spawn request unpacked to more than {MaxPayloadBytes} bytes.");
            return false;
        }

        if (length == 0) { return false; }

        using MemoryStream stream = new(buffer, 0, length, writable: false);

        if (!WithinDepthLimit(stream))
        {
            Log.Warn("A spawn request nested its entries deeper than the mod accepts.");
            return false;
        }

        XElement? root;
        try
        {
            stream.Position = 0;

            using XmlReader reader = XMLExtensions.CreateReader(stream);
            root = XDocument.Load(reader).Root;
        }
        catch (Exception e)
        {
            Log.Warn("A spawn request carried XML that could not be read", e);
            return false;
        }

        if (root is null || root.Name.LocalName != SpawnPayload.RootName) { return false; }

        payload = SpawnPayload.FromXml(root);

        if (payload.TemplateCount > MaxTemplates || !payload.WithinEntryLimit())
        {
            Log.Warn("A spawn request asked for more entries than the mod accepts.");
            payload = new SpawnPayload();
            return false;
        }

        return true;
    }

    internal static string[] LimitProblems(IReadOnlyList<string> problems)
        =>
        [
            .. problems.Take(MaxProblems).Select(problem =>
                problem.Length > MaxProblemLength ? problem[..MaxProblemLength] : problem)
        ];

    private static int Decompress(byte[] compressed, byte[] buffer)
    {
        using MemoryStream input = new(compressed, writable: false);
        using BrotliStream decompressor = new(input, CompressionMode.Decompress);

        int total = 0;
        while (total < buffer.Length)
        {
            int read = decompressor.Read(buffer, total, buffer.Length - total);
            if (read <= 0) { return total; }

            total += read;
        }

        return decompressor.ReadByte() < 0 ? total : -1;
    }

    private static bool WithinDepthLimit(Stream stream)
    {
        try
        {
            using XmlReader reader = XMLExtensions.CreateReader(stream);
            while (reader.Read())
            {
                if (reader.Depth > MaxDepth) { return false; }
            }
        }
        catch (XmlException)
        {
            return true;
        }

        return true;
    }
}
