using ImageMetadataParser.Data;
using ImageMetadataParser.Services.Parsers;
using Microsoft.AspNetCore.Components.Forms;
using System.Security.Cryptography;
using System.Text;

namespace ImageMetadataParser.Services
{
    public class ImageUploadService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ImageUploadService> _logger;
        private readonly IEnumerable<IImageParser> _parsers;
        private readonly AiImageAnalyzer _aiAnalyzer;
        private readonly KeywordService _keywordService;

        public ImageUploadService(
            AppDbContext context,
            ILogger<ImageUploadService> logger,
            IEnumerable<IImageParser> parsers,
            AiImageAnalyzer aiAnalyzer,
            KeywordService keywordService)
        {
            _context = context;
            _logger = logger;
            _parsers = parsers;
            _aiAnalyzer = aiAnalyzer;
            _keywordService = keywordService;
        }

        public async Task<ImageMetadata> ProcessImageAsync(IBrowserFile file)
        {
            var metadata = new ImageMetadata
            {
                FileName = file.Name,
                FileSizeBytes = file.Size,
                MimeType = file.ContentType,
                ProcessingStatus = "Processing",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                // Copy the browser stream to a seekable MemoryStream
                using var browserStream = file.OpenReadStream(maxAllowedSize: 52428800); // 50MB
                using var memoryStream = new MemoryStream();
                await browserStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Calculate file hash for deduplication
                metadata.FileHash = await CalculateFileHashAsync(memoryStream);
                memoryStream.Position = 0;

                // Extract basic image dimensions
                await ExtractBasicImageInfoAsync(metadata, memoryStream);
                memoryStream.Position = 0;

                // Parse metadata using available parsers
                await ExtractMetadataAsync(metadata, memoryStream, file.Name);
                memoryStream.Position = 0;

                // Generate AI analysis
                await EnrichWithAiAnalysisAsync(metadata, memoryStream);

                // Process keywords
                var keywords = _keywordService.ExtractKeywordsFromMetadata(metadata);
                metadata.Keywords = string.Join(", ", keywords);

                // TODO: Save to database when implemented
                // await SaveToDatabaseAsync(metadata);

                metadata.MarkAsCompleted();
                _logger.LogInformation("Successfully processed image: {FileName}", file.Name);
            }
            catch (Exception ex)
            {
                metadata.MarkAsFailed(ex.Message);
                _logger.LogError(ex, "Error processing image: {FileName}", file.Name);
            }

            return metadata;
        }

        public async Task<List<ImageMetadata>> ProcessMultipleImagesAsync(IEnumerable<IBrowserFile> files)
        {
            var results = new List<ImageMetadata>();

            foreach (var file in files)
            {
                var result = await ProcessImageAsync(file);
                results.Add(result);
            }

            return results;
        }

        private async Task<string> CalculateFileHashAsync(Stream stream)
        {
            using var md5 = MD5.Create();
            var hash = await md5.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private async Task ExtractBasicImageInfoAsync(ImageMetadata metadata, Stream stream)
        {
            try
            {
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(stream);
                metadata.Width = image.Width;
                metadata.Height = image.Height;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract image dimensions for {FileName}", metadata.FileName);
            }
        }

        private async Task ExtractMetadataAsync(ImageMetadata metadata, Stream stream, string fileName)
        {
            foreach (var parser in _parsers)
            {
                try
                {
                    if (parser.ValidateFile(fileName, stream))
                    {
                        stream.Position = 0; // Now this works because it's a MemoryStream
                        var extractedMetadata = await parser.ParseMetadataAsync(stream, fileName);

                        // Merge extracted metadata
                        MergeMetadata(metadata, extractedMetadata);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Parser {ParserType} failed for {FileName}", parser.GetType().Name, fileName);
                }
            }
        }

        private void MergeMetadata(ImageMetadata target, ImageMetadata source)
        {
            // Only update if target doesn't have the value
            target.ExifData ??= source.ExifData;
            target.XmpData ??= source.XmpData;
            target.IptcData ??= source.IptcData;
            target.CameraMake ??= source.CameraMake;
            target.CameraModel ??= source.CameraModel;
            target.LensInfo ??= source.LensInfo;
            target.DateTaken ??= source.DateTaken;
            target.Iso ??= source.Iso;
            target.Aperture ??= source.Aperture;
            target.ShutterSpeed ??= source.ShutterSpeed;
            target.FocalLength ??= source.FocalLength;
            target.GpsLatitude ??= source.GpsLatitude;
            target.GpsLongitude ??= source.GpsLongitude;
            target.GpsAltitude ??= source.GpsAltitude;
            target.LocationName ??= source.LocationName;
            target.Copyright ??= source.Copyright;
            target.Artist ??= source.Artist;
            target.Description ??= source.Description;
            target.Software ??= source.Software;
            target.ColorSpace ??= source.ColorSpace;
            target.Orientation ??= source.Orientation;
            target.WhiteBalance ??= source.WhiteBalance;
            target.Flash ??= source.Flash;
            target.MeteringMode ??= source.MeteringMode;
            target.ExposureMode ??= source.ExposureMode;
            target.SceneCaptureType ??= source.SceneCaptureType;
        }

        private async Task EnrichWithAiAnalysisAsync(ImageMetadata metadata, Stream stream)
        {
            try
            {
                var aiResult = await _aiAnalyzer.AnalyzeImageAsync(stream, metadata.FileName);
                metadata.AiAnalysis = aiResult.Description;

                // Merge AI keywords with existing keywords
                if (aiResult.Keywords?.Any() == true)
                {
                    var existingKeywords = metadata.Keywords?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim()) ?? Enumerable.Empty<string>();

                    var allKeywords = existingKeywords.Concat(aiResult.Keywords).Distinct();
                    metadata.Keywords = string.Join(", ", allKeywords);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI analysis failed for {FileName}", metadata.FileName);
                // Don't fail the entire process if AI analysis fails
            }
        }

        private async Task SaveToDatabaseAsync(ImageMetadata metadata)
        {
            _context.ImageMetadataRecords.Add(metadata);
            await _context.SaveChangesAsync();
        }
    }

    public class AiAnalysisResult
    {
        public string Description { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = new();
        public float Confidence { get; set; }
    }
}