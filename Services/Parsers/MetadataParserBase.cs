using ImageMetadataParser.Data;

namespace ImageMetadataParser.Services.Parsers
{
    public abstract class MetadataParserBase : IImageParser
    {
        protected readonly ILogger Logger;

        protected MetadataParserBase(ILogger logger)
        {
            Logger = logger;
        }

        public abstract Task<ImageMetadata> ParseMetadataAsync(Stream imageStream, string fileName);
        public abstract string[] GetSupportedFormats();

        public virtual bool ValidateFile(string fileName, Stream stream)
        {
            try
            {
                var supportedFormats = GetSupportedFormats();
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                return supportedFormats.Contains(extension);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error validating file {FileName}", fileName);
                return false;
            }
        }

        protected virtual ImageMetadata CreateBaseMetadata(string fileName)
        {
            return new ImageMetadata
            {
                FileName = fileName,
                ProcessingStatus = "Processing",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        protected virtual string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }

        protected virtual void SafeParseDateTime(string? dateString, Action<DateTime> setter)
        {
            if (string.IsNullOrEmpty(dateString)) return;

            try
            {
                if (DateTime.TryParse(dateString, out var result))
                {
                    setter(result);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse date: {DateString}", dateString);
            }
        }

        protected virtual void SafeParseDecimal(string? value, Action<decimal> setter)
        {
            if (string.IsNullOrEmpty(value)) return;

            try
            {
                if (decimal.TryParse(value, out var result))
                {
                    setter(result);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse decimal: {Value}", value);
            }
        }

        protected virtual void SafeParseInt(string? value, Action<int> setter)
        {
            if (string.IsNullOrEmpty(value)) return;

            try
            {
                if (int.TryParse(value, out var result))
                {
                    setter(result);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse integer: {Value}", value);
            }
        }
    }
}