using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SandboxMenu.Networking;

// A request travels as compressed XML: a set with property overrides is far too long to put on the wire as text,
// and since the transport deflates long messages again, keeping this small is about staying under its threshold
// rather than about the bytes themselves. The host offers nothing here to reuse — the network service takes a
// struct and sends it, and has no plugin-facing codec or size limit — so the packing and every limit around it
// are the mod's own, which is also why they are spelled out rather than left to the transport.
//
// Everything a client sends lands here, so the limits below are enforced while decompressing or right after
// parsing — never by believing what the XML says about itself. Both halves compile this code, but only the server
// ever decodes something it did not produce.
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

            using var output = new MemoryStream();
            using (var compressor = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
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

        using var stream = new MemoryStream(buffer, 0, length, writable: false);

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

    // The problem lines go back to the client, so they are capped and truncated: they exist to explain a failure,
    // not to carry a whole log across the network.
    internal static string[] LimitProblems(IReadOnlyList<string> problems)
    {
        if (problems.Count == 0) { return []; }

        int count = Math.Min(problems.Count, MaxProblems);
        string[] limited = new string[count];

        for (int i = 0; i < count; i++)
        {
            string problem = problems[i] ?? string.Empty;
            limited[i] = problem.Length > MaxProblemLength ? problem[..MaxProblemLength] : problem;
        }

        return limited;
    }

    // Returns the number of bytes written, or -1 when the payload unpacks to more than we are willing to hold.
    private static int Decompress(byte[] compressed, byte[] buffer)
    {
        using var input = new MemoryStream(compressed, writable: false);
        using var decompressor = new BrotliStream(input, CompressionMode.Decompress);

        int total = 0;
        while (total < buffer.Length)
        {
            int read = decompressor.Read(buffer, total, buffer.Length - total);
            if (read <= 0) { return total; }

            total += read;
        }

        // Full buffer: anything left over means the payload does not fit.
        return decompressor.ReadByte() < 0 ? total : -1;
    }

    // Walks the payload once with the host's own reader settings before the model is built from it: depth is the
    // one shape a document can take that the other limits would not catch, since every level costs a frame while
    // the entries are read. The settings are the host's (no DTDs, nothing external, whitespace dropped) rather
    // than a second set of the mod's own.
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
            // Malformed XML is the parse's business; here it only has to not be deceptively deep.
            return true;
        }

        return true;
    }
}
