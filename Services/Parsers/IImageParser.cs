using ImageMetadataParser.Data;

namespace ImageMetadataParser.Services.Parsers
{
    public interface IImageParser
    {
        /// <summary>
        /// Parse metadata from the provided image stream
        /// </summary>
        Task<ImageMetadata> ParseMetadataAsync(Stream imageStream, string fileName);

        /// <summary>
        /// Get the file formats supported by this parser
        /// </summary>
        string[] GetSupportedFormats();

        /// <summary>
        /// Validate if this parser can handle the given file
        /// </summary>
        bool ValidateFile(string fileName, Stream stream);
    }
}
