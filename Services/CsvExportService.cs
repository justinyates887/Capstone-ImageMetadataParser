using ImageMetadataParser.Data;
using System.Text;

namespace ImageMetadataParser.Services
{
    public class CsvExportService
    {
        private readonly ILogger<CsvExportService> _logger;

        public CsvExportService(ILogger<CsvExportService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ExportToCsvAsync(IEnumerable<ImageMetadata> data)
        {
            try
            {
                var csv = new StringBuilder();

                // Add headers
                var headers = new[]
                {
                    "ID", "FileName", "FileSize", "MimeType", "Dimensions", "DateTaken",
                    "CameraMake", "CameraModel", "LensInfo", "CameraSettings",
                    "ISO", "Aperture", "ShutterSpeed", "FocalLength",
                    "GpsLatitude", "GpsLongitude", "GpsAltitude", "LocationName",
                    "Keywords", "Description", "AiAnalysis", "Artist", "Copyright",
                    "ProcessingStatus", "CreatedAt", "UpdatedAt"
                };

                csv.AppendLine(string.Join(",", headers.Select(EscapeCsvValue)));

                // Add data rows
                foreach (var item in data)
                {
                    var values = new object?[]
                    {
                        item.Id,
                        item.FileName,
                        item.FileSize,
                        item.MimeType,
                        item.Dimensions,
                        item.DateTaken?.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.CameraMake,
                        item.CameraModel,
                        item.LensInfo,
                        item.CameraSettings,
                        item.Iso,
                        item.Aperture,
                        item.ShutterSpeed,
                        item.FocalLength,
                        item.GpsLatitude,
                        item.GpsLongitude,
                        item.GpsAltitude,
                        item.LocationName,
                        item.Keywords,
                        item.Description,
                        item.AiAnalysis,
                        item.Artist,
                        item.Copyright,
                        item.ProcessingStatus,
                        item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    csv.AppendLine(string.Join(",", values.Select(v => EscapeCsvValue(v?.ToString()))));
                }

                _logger.LogInformation("Exported {Count} records to CSV", data.Count());
                return csv.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting data to CSV");
                throw;
            }
        }

        public async Task<string> ExportToJsonAsync(IEnumerable<ImageMetadata> data)
        {
            try
            {
                var dtoList = data.Select(ImageMetadataDto.FromEntity).ToList();
                var json = System.Text.Json.JsonSerializer.Serialize(dtoList, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });

                _logger.LogInformation("Exported {Count} records to JSON", data.Count());
                return json;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting data to JSON");
                throw;
            }
        }

        private string EscapeCsvValue(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // Escape quotes by doubling them and wrap in quotes if needed
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}