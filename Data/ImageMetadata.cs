using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImageMetadataParser.Data
{

    public class ImageMetadata
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        [NotMapped]
        public string FileSize => FormatFileSize(FileSizeBytes);

        [MaxLength(50)]
        public string? MimeType { get; set; }

        [MaxLength(32)]
        public string? FileHash { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        [NotMapped]
        public string? Dimensions => (Width.HasValue && Height.HasValue) ? $"{Width}x{Height}" : null;

        [Column(TypeName = "text")]
        public string? ExifData { get; set; }

        [Column(TypeName = "text")]
        public string? XmpData { get; set; }

        [Column(TypeName = "text")]
        public string? IptcData { get; set; }

        [Column(TypeName = "text")]
        public string? AiAnalysis { get; set; }

        [Column(TypeName = "text")]
        public string? Keywords { get; set; }

        [MaxLength(100)]
        public string? CameraMake { get; set; }

        [MaxLength(100)]
        public string? CameraModel { get; set; }

        [MaxLength(200)]
        public string? LensInfo { get; set; }

        public DateTime? DateTaken { get; set; }

        public int? Iso { get; set; }

        public decimal? Aperture { get; set; }

        public decimal? ShutterSpeed { get; set; }

        public decimal? FocalLength { get; set; }

        public decimal? GpsLatitude { get; set; }

        public decimal? GpsLongitude { get; set; }

        public decimal? GpsAltitude { get; set; }

        [MaxLength(200)]
        public string? LocationName { get; set; }

        [MaxLength(500)]
        public string? Copyright { get; set; }

        [MaxLength(200)]
        public string? Artist { get; set; }

        [Column(TypeName = "text")]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? Software { get; set; }

        [MaxLength(50)]
        public string? ColorSpace { get; set; }

        public int? Orientation { get; set; }

        [MaxLength(50)]
        public string? WhiteBalance { get; set; }

        [MaxLength(100)]
        public string? Flash { get; set; }

        [MaxLength(50)]
        public string? MeteringMode { get; set; }

        [MaxLength(50)]
        public string? ExposureMode { get; set; }

        /// <summary>
        /// Scene capture type
        /// </summary>
        [MaxLength(50)]
        public string? SceneCaptureType { get; set; }

        /// <summary>
        /// Status of processing (e.g., "Processing", "Completed", "Failed")
        /// </summary>
        [MaxLength(20)]
        public string ProcessingStatus { get; set; } = "Pending";

        /// <summary>
        /// Any error messages encountered during processing
        /// </summary>
        [Column(TypeName = "text")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Date and time when the record was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date and time when the record was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User identifier (if implementing user-specific uploads)
        /// </summary>
        [MaxLength(100)]
        public string? UserId { get; set; }

        /// <summary>
        /// Batch identifier for grouping related uploads
        /// </summary>
        [MaxLength(50)]
        public string? BatchId { get; set; }

        /// <summary>
        /// Original file path (for reference, not stored permanently)
        /// </summary>
        [NotMapped]
        public string? OriginalFilePath { get; set; }

        /// <summary>
        /// Indicates whether the image has been successfully processed
        /// </summary>
        [NotMapped]
        public bool IsProcessed => ProcessingStatus == "Completed";

        /// <summary>
        /// Indicates whether there were errors during processing
        /// </summary>
        [NotMapped]
        public bool HasErrors => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Gets a summary of camera settings for display
        /// </summary>
        [NotMapped]
        public string? CameraSettings
        {
            get
            {
                var settings = new List<string>();
                if (Aperture.HasValue)
                    settings.Add($"f/{Aperture:F1}");
                if (ShutterSpeed.HasValue)
                {
                    if (ShutterSpeed >= 1)
                        settings.Add($"{ShutterSpeed}s");
                    else
                        settings.Add($"1/{Math.Round(1 / ShutterSpeed.Value)}s");
                }
                if (Iso.HasValue)
                    settings.Add($"ISO {Iso}");
                if (FocalLength.HasValue)
                    settings.Add($"{FocalLength}mm");
                return settings.Any() ? string.Join(" • ", settings) : null;
            }
        }

        /// <summary>
        /// Gets GPS coordinates as a formatted string
        /// </summary>
        [NotMapped]
        public string? GpsCoordinates
        {
            get
            {
                if (GpsLatitude.HasValue && GpsLongitude.HasValue)
                {
                    var lat = GpsLatitude.Value >= 0 ? $"{GpsLatitude:F6}°N" : $"{Math.Abs(GpsLatitude.Value):F6}°S";
                    var lng = GpsLongitude.Value >= 0 ? $"{GpsLongitude:F6}°E" : $"{Math.Abs(GpsLongitude.Value):F6}°W";
                    return $"{lat}, {lng}";
                }
                return null;
            }
        }

        /// <summary>
        /// Gets keywords as a list for easier manipulation
        /// </summary>
        [NotMapped]
        public List<string> KeywordList
        {
            get => string.IsNullOrEmpty(Keywords)
                ? new List<string>()
                : Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(k => k.Trim())
                         .Where(k => !string.IsNullOrEmpty(k))
                         .ToList();
            set => Keywords = value?.Any() == true ? string.Join(", ", value) : null;
        }

        /// <summary>
        /// Helper method to format file size
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        /// <summary>
        /// Updates the UpdatedAt timestamp
        /// </summary>
        public void Touch()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the processing as completed
        /// </summary>
        public void MarkAsCompleted()
        {
            ProcessingStatus = "Completed";
            Touch();
        }

        /// <summary>
        /// Marks the processing as failed with an error message
        /// </summary>
        public void MarkAsFailed(string errorMessage)
        {
            ProcessingStatus = "Failed";
            ErrorMessage = errorMessage;
            Touch();
        }

        /// <summary>
        /// Clears any error state and resets to pending
        /// </summary>
        public void ResetProcessingState()
        {
            ProcessingStatus = "Pending";
            ErrorMessage = null;
            Touch();
        }
    }

    /// <summary>
    /// DTO for API responses
    /// </summary>
    public class ImageMetadataDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string? MimeType { get; set; }
        public string? Dimensions { get; set; }
        public DateTime? DateTaken { get; set; }
        public string? CameraSettings { get; set; }
        public string? GpsCoordinates { get; set; }
        public string? LocationName { get; set; }
        public List<string> Keywords { get; set; } = new();
        public string? AiAnalysis { get; set; }
        public string ProcessingStatus { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Convert from ImageMetadata entity to DTO
        /// </summary>
        public static ImageMetadataDto FromEntity(ImageMetadata entity)
        {
            return new ImageMetadataDto
            {
                Id = entity.Id,
                FileName = entity.FileName,
                FileSize = entity.FileSize,
                MimeType = entity.MimeType,
                Dimensions = entity.Dimensions,
                DateTaken = entity.DateTaken,
                CameraSettings = entity.CameraSettings,
                GpsCoordinates = entity.GpsCoordinates,
                LocationName = entity.LocationName,
                Keywords = entity.KeywordList,
                AiAnalysis = entity.AiAnalysis,
                ProcessingStatus = entity.ProcessingStatus,
                CreatedAt = entity.CreatedAt
            };
        }
    }
}