using Microsoft.AspNetCore.Mvc;
using ImageMetadataParser.Services;
using ImageMetadataParser.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;

namespace ImageMetadataParser.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ImagesController : ControllerBase
    {
        private readonly ImageUploadService _uploadService;
        private readonly CsvExportService _exportService;
        private readonly ILogger<ImagesController> _logger;
        private const int MAX_FILE_SIZE = 52428800; // 50MB
        private const int MAX_FILES_BATCH = 10;

        public ImagesController(
            ImageUploadService uploadService,
            CsvExportService exportService,
            ILogger<ImagesController> logger)
        {
            _uploadService = uploadService;
            _exportService = exportService;
            _logger = logger;
        }

        /// <summary>
        /// Analyze a single image and extract metadata
        /// </summary>
        /// <param name="file">Image file to analyze</param>
        /// <returns>Extracted metadata in JSON format</returns>
        [HttpPost("analyze")]
        [RequestSizeLimit(MAX_FILE_SIZE)]
        [ProducesResponseType(typeof(ApiResponse<ImageMetadataDto>), 200)]
        [ProducesResponseType(typeof(ApiErrorResponse), 400)]
        [ProducesResponseType(typeof(ApiErrorResponse), 500)]
        public async Task<IActionResult> AnalyzeSingleImage(IFormFile file)
        {
            try
            {
                // Validate input
                var validation = ValidateImageFile(file);
                if (!validation.IsValid)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "Invalid file",
                        Message = string.Join(", ", validation.Errors),
                        Details = validation.Errors.ToList()
                    });
                }

                _logger.LogInformation("Processing API request for file: {FileName}", file?.FileName ?? "unknown");

                // Convert IFormFile to IBrowserFile-like object for compatibility
                var browserFile = new FormFileBrowserFileAdapter(file);

                // Process the image
                var metadata = await _uploadService.ProcessImageAsync(browserFile);

                // Convert to DTO
                var dto = ImageMetadataDto.FromEntity(metadata);

                var response = new ApiResponse<ImageMetadataDto>
                {
                    Success = true,
                    Data = dto,
                    Message = "Image analyzed successfully",
                    ProcessingTime = TimeSpan.FromMilliseconds(100) // This could be tracked properly
                };

                _logger.LogInformation("Successfully processed API request for file: {FileName}", file?.FileName ?? "unknown");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image via API: {FileName}", file?.FileName);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "Processing failed",
                    Message = "An error occurred while processing the image",
                    Details = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Analyze multiple images in a batch
        /// </summary>
        /// <param name="files">Image files to analyze</param>
        /// <returns>Array of extracted metadata in JSON format</returns>
        [HttpPost("analyze-batch")]
        [RequestSizeLimit(MAX_FILE_SIZE * MAX_FILES_BATCH)]
        [ProducesResponseType(typeof(ApiResponse<ImageMetadataDto[]>), 200)]
        [ProducesResponseType(typeof(ApiErrorResponse), 400)]
        [ProducesResponseType(typeof(ApiErrorResponse), 500)]
        public async Task<IActionResult> AnalyzeBatchImages(List<IFormFile> files)
        {
            try
            {
                // Validate batch size
                if (files?.Count > MAX_FILES_BATCH)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "Too many files",
                        Message = $"Maximum {MAX_FILES_BATCH} files allowed per batch",
                        Details = new List<string> { $"Received {files.Count} files" }
                    });
                }

                if (files?.Count == 0)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "No files provided",
                        Message = "At least one file is required"
                    });
                }

                _logger.LogInformation("Processing batch API request for {FileCount} files", files?.Count ?? 0);

                var results = new List<ImageMetadataDto>();
                var errors = new List<string>();

                foreach (var file in files ?? new List<IFormFile>())
                {
                    try
                    {
                        var validation = ValidateImageFile(file);
                        if (!validation.IsValid)
                        {
                            errors.Add($"{file.FileName ?? "unknown"}: {string.Join(", ", validation.Errors)}");
                            continue;
                        }

                        var browserFile = new FormFileBrowserFileAdapter(file);
                        var metadata = await _uploadService.ProcessImageAsync(browserFile);
                        var dto = ImageMetadataDto.FromEntity(metadata);
                        results.Add(dto);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process file {FileName} in batch", file?.FileName ?? "unknown");
                        errors.Add($"{file.FileName ?? "unknown"}: {ex.Message}");
                    }
                }

                var response = new ApiResponse<ImageMetadataDto[]>
                {
                    Success = results.Count > 0,
                    Data = results.ToArray(),
                    Message = $"Processed {results.Count} of {files?.Count ?? 0} files successfully",
                    Errors = errors.Any() ? errors : null
                };

                _logger.LogInformation("Batch processing completed: {SuccessCount}/{TotalCount}", results.Count, files?.Count ?? 0);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch images via API");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "Batch processing failed",
                    Message = "An error occurred while processing the image batch",
                    Details = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Export metadata to CSV format
        /// </summary>
        /// <param name="request">Export request with metadata</param>
        /// <returns>CSV file download</returns>
        [HttpPost("export/csv")]
        [ProducesResponseType(typeof(FileResult), 200)]
        [ProducesResponseType(typeof(ApiErrorResponse), 400)]
        public async Task<IActionResult> ExportToCsv([FromBody] ExportRequest request)
        {
            try
            {
                if (request?.MetadataList?.Any() != true)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "No data provided",
                        Message = "Metadata list is required for export"
                    });
                }

                // Convert DTOs back to entities for export service
                var entities = request.MetadataList.Select(dto => new ImageMetadata
                {
                    Id = dto.Id,
                    FileName = dto.FileName,
                    FileSizeBytes = ParseFileSize(dto.FileSize),
                    MimeType = dto.MimeType,
                    Width = ParseDimension(dto.Dimensions, true),
                    Height = ParseDimension(dto.Dimensions, false),
                    DateTaken = dto.DateTaken,
                    Keywords = string.Join(", ", dto.Keywords),
                    AiAnalysis = dto.AiAnalysis,
                    LocationName = dto.LocationName,
                    ProcessingStatus = dto.ProcessingStatus,
                    CreatedAt = dto.CreatedAt
                }).ToList();

                var csvContent = await _exportService.ExportToCsvAsync(entities);
                var fileName = $"image_metadata_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(System.Text.Encoding.UTF8.GetBytes(csvContent), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to CSV via API");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "Export failed",
                    Message = "An error occurred while exporting data",
                    Details = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Get API health status and supported formats
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ApiHealthResponse), 200)]
        public IActionResult GetHealth()
        {
            return Ok(new ApiHealthResponse
            {
                Status = "Healthy",
                Version = "1.0.0",
                SupportedFormats = new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".tiff", ".tif", ".ico", ".avif", ".heic", ".heif" },
                MaxFileSize = MAX_FILE_SIZE,
                MaxBatchSize = MAX_FILES_BATCH,
                Timestamp = DateTime.UtcNow
            });
        }

        private ValidationResult ValidateImageFile(IFormFile? file)
        {
            var errors = new List<string>();

            if (file == null)
            {
                errors.Add("File is required");
                return new ValidationResult { IsValid = false, Errors = errors };
            }

            if (file.Length == 0)
            {
                errors.Add("File is empty");
            }

            if (file.Length > MAX_FILE_SIZE)
            {
                errors.Add($"File too large. Maximum size is {FormatFileSize(MAX_FILE_SIZE)}");
            }

            var validTypes = new List<string> { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/webp", "image/svg+xml", "image/tiff" };
            var validExtensions = new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".tiff", ".tif", ".ico", ".avif", ".heic", ".heif" };

            var isValidType = validTypes.Contains(file.ContentType ?? "") ||
                             validExtensions.Any(ext => (file.FileName ?? "").EndsWith(ext, StringComparison.OrdinalIgnoreCase));

            if (!isValidType)
            {
                errors.Add("Invalid file type. Supported formats: " + string.Join(", ", validExtensions));
            }

            return new ValidationResult { IsValid = errors.Count == 0, Errors = errors };
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }

        private static long ParseFileSize(string fileSize)
        {
            // Simple parser for file size strings like "1.5 MB"
            if (string.IsNullOrEmpty(fileSize)) return 0;

            var parts = fileSize.Split(' ');
            if (parts.Length != 2) return 0;

            if (!decimal.TryParse(parts[0], out var size)) return 0;

            return parts[1].ToUpperInvariant() switch
            {
                "B" => (long)size,
                "KB" => (long)(size * 1024),
                "MB" => (long)(size * 1024 * 1024),
                "GB" => (long)(size * 1024 * 1024 * 1024),
                _ => 0
            };
        }

        private static int? ParseDimension(string? dimensions, bool getWidth)
        {
            if (string.IsNullOrEmpty(dimensions)) return null;

            var parts = dimensions.Split('x');
            if (parts.Length != 2) return null;

            var index = getWidth ? 0 : 1;
            return int.TryParse(parts[index], out var result) ? result : null;
        }
    }

    // Supporting classes
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string>? Errors { get; set; }
        public TimeSpan? ProcessingTime { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ApiErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string>? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ApiHealthResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<string> SupportedFormats { get; set; } = new();
        public long MaxFileSize { get; set; }
        public int MaxBatchSize { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ExportRequest
    {
        public List<ImageMetadataDto> MetadataList { get; set; } = new();
    }

    public class FormFileBrowserFileAdapter : IBrowserFile
    {
        private readonly IFormFile _formFile;

        public FormFileBrowserFileAdapter(IFormFile formFile)
        {
            _formFile = formFile ?? throw new ArgumentNullException(nameof(formFile));
        }

        public string Name => _formFile.FileName;
        public DateTimeOffset LastModified => DateTimeOffset.Now;
        public long Size => _formFile.Length;
        public string ContentType => _formFile.ContentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize)
            {
                throw new IOException($"The file '{Name}' is {Size} bytes, which exceeds the maximum allowed size of {maxAllowedSize} bytes.");
            }
            return _formFile.OpenReadStream();
        }
    }
}