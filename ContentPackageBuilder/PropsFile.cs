using System.Xml;
using System.Xml.Linq;

namespace ContentPackageBuilder;

internal sealed class BuildDataException(string message, Exception? innerException = null) : Exception(message, innerException);

internal static class PropsFile
{
    internal static string ReadElementValue(string propsPath, string elementName)
    {
        XDocument document;

        try
        {
            document = XDocument.Load(propsPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException)
        {
            throw new BuildDataException($"Failed to read '{propsPath}': {exception.Message}", exception);
        }

        var value = document
            .Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, elementName, StringComparison.Ordinal))
            ?.Value
            .Trim();

        if (string.IsNullOrEmpty(value))
        {
            throw new BuildDataException($"'{elementName}' is missing or empty in '{propsPath}'.");
        }

        return value;
    }

    internal static string ReadDirectory(string propsPath, string elementName)
    {
        var value = ReadElementValue(propsPath, elementName);

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new BuildDataException($"'{value}' is not a usable directory path: {exception.Message}", exception);
        }
    }
}
