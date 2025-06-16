using ImageMetadataParser.Data;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using System.Text.Json;

namespace ImageMetadataParser.Services.Parsers
{
    public class ExifParser : MetadataParserBase
    {
        public ExifParser(ILogger<ExifParser> logger) : base(logger)
        {
        }

        public override async Task<ImageMetadata> ParseMetadataAsync(Stream imageStream, string fileName)
        {
            var metadata = CreateBaseMetadata(fileName);

            try
            {
                Logger.LogInformation("Parsing EXIF data for {FileName}", fileName);

                // Reset stream position
                imageStream.Position = 0;

                // Extract metadata using MetadataExtractor
                var directories = ImageMetadataReader.ReadMetadata(imageStream);

                // Create detailed EXIF data dictionary
                var exifData = new Dictionary<string, object>();

                foreach (var directory in directories)
                {
                    var directoryName = directory.Name;
                    var directoryData = new Dictionary<string, string>();

                    foreach (var tag in directory.Tags)
                    {
                        directoryData[tag.Name] = tag.Description ?? "";
                    }

                    if (directoryData.Any())
                    {
                        exifData[directoryName] = directoryData;
                    }
                }

                // Store raw EXIF data as JSON
                metadata.ExifData = JsonSerializer.Serialize(exifData, new JsonSerializerOptions { WriteIndented = true });

                // Extract specific EXIF values
                ExtractCameraInfo(directories, metadata);
                ExtractDateTimeInfo(directories, metadata);
                ExtractLocationInfo(directories, metadata);
                ExtractTechnicalInfo(directories, metadata);
                ExtractImageInfo(directories, metadata);

                Logger.LogInformation("EXIF parsing completed for {FileName}", fileName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error parsing EXIF data for {FileName}", fileName);
                metadata.ErrorMessage = $"EXIF parsing failed: {ex.Message}";
            }

            return metadata;
        }

        private void ExtractCameraInfo(IEnumerable<MetadataExtractor.Directory> directories, ImageMetadata metadata)
        {
            // Camera Make and Model
            var ifd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0Directory != null)
            {
                metadata.CameraMake = ifd0Directory.GetString(ExifIfd0Directory.TagMake)?.Trim();
                metadata.CameraModel = ifd0Directory.GetString(ExifIfd0Directory.TagModel)?.Trim();
                metadata.Software = ifd0Directory.GetString(ExifIfd0Directory.TagSoftware)?.Trim();
                metadata.Artist = ifd0Directory.GetString(ExifIfd0Directory.TagArtist)?.Trim();
                metadata.Copyright = ifd0Directory.GetString(ExifIfd0Directory.TagCopyright)?.Trim();

                // Orientation
                if (ifd0Directory.TryGetInt32(ExifIfd0Directory.TagOrientation, out var orientation))
                {
                    metadata.Orientation = orientation;
                }
            }

            // Lens Info
            var exifSubDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubDirectory != null)
            {
                metadata.LensInfo = exifSubDirectory.GetString(ExifSubIfdDirectory.TagLensModel)?.Trim();
            }
        }

        private void ExtractDateTimeInfo(IEnumerable<MetadataExtractor.Directory> directories, ImageMetadata metadata)
        {
            var exifSubDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubDirectory != null)
            {
                try
                {
                    if (exifSubDirectory.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out var dateTime))
                    {
                        metadata.DateTaken = dateTime;
                    }
                    else if (exifSubDirectory.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeDigitized, out var digitizedTime))
                    {
                        metadata.DateTaken = digitizedTime;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Could not parse date/time for {FileName}", metadata.FileName);
                }
            }

            // Fallback to IFD0 DateTime
            if (!metadata.DateTaken.HasValue)
            {
                var ifd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (ifd0Directory != null)
                {
                    try
                    {
                        if (ifd0Directory.TryGetDateTime(ExifIfd0Directory.TagDateTime, out var dateTime))
                        {
                            metadata.DateTaken = dateTime;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Could not parse fallback date/time for {FileName}", metadata.FileName);
                    }
                }
            }
        }

        private void ExtractLocationInfo(IEnumerable<MetadataExtractor.Directory> directories, ImageMetadata metadata)
        {
            var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gpsDirectory != null)
            {
                try
                {
                    var location = gpsDirectory.GetGeoLocation();
                    if (location != null)
                    {
                        metadata.GpsLatitude = (decimal)location.Latitude;
                        metadata.GpsLongitude = (decimal)location.Longitude;
                    }

                    // GPS Altitude
                    if (gpsDirectory.TryGetDouble(GpsDirectory.TagAltitude, out var altitude))
                    {
                        metadata.GpsAltitude = (decimal)altitude;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Could not parse GPS data for {FileName}", metadata.FileName);
                }
            }
        }

        private void ExtractTechnicalInfo(IEnumerable<MetadataExtractor.Directory> directories, ImageMetadata metadata)
        {
            var exifSubDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubDirectory != null)
            {
                // ISO
                if (exifSubDirectory.TryGetInt32(ExifSubIfdDirectory.TagIsoEquivalent, out var iso))
                {
                    metadata.Iso = iso;
                }

                // Aperture (F-Number)
                if (exifSubDirectory.TryGetDouble(ExifSubIfdDirectory.TagFNumber, out var fNumber))
                {
                    metadata.Aperture = (decimal)fNumber;
                }

                // Shutter Speed (Exposure Time)
                if (exifSubDirectory.TryGetDouble(ExifSubIfdDirectory.TagExposureTime, out var exposureTime))
                {
                    metadata.ShutterSpeed = (decimal)exposureTime;
                }

                // Focal Length
                if (exifSubDirectory.TryGetDouble(ExifSubIfdDirectory.TagFocalLength, out var focalLength))
                {
                    metadata.FocalLength = (decimal)focalLength;
                }

                // White Balance
                if (exifSubDirectory.TryGetInt32(ExifSubIfdDirectory.TagWhiteBalance, out var whiteBalance))
                {
                    metadata.WhiteBalance = whiteBalance == 0 ? "Auto" : "Manual";
                }

                // Flash
                if (exifSubDirectory.TryGetInt32(ExifSubIfdDirectory.TagFlash, out var flash))
                {
                    metadata.Flash = GetFlashDescription(flash);
                }

                // Metering Mode
                if (exifSubDirectory.TryGetInt32(ExifSubIfdDirectory.TagMeteringMode, out var meteringMode))
                {
                    metadata.MeteringMode = GetMeteringModeDescription(meteringMode);
                }

                // Exposure Mode
                if (exifSubDirectory.TryGetInt32(ExifSubIfdDirectory.TagExposureMode, out var exposureMode))
                {
                    metadata.ExposureMode = GetExposureModeDescription(exposureMode);
                }

                // Scene Capture Type
                if (exifSubDirectory.TryGetInt32(ExifSubIfdDirectory.TagSceneCaptureType, out var sceneType))
                {
                    metadata.SceneCaptureType = GetSceneCaptureTypeDescription(sceneType);
                }

                // Color Space
                if (exifSubDirectory.TryGetInt32(ExifSubIfdDirectory.TagColorSpace, out var colorSpace))
                {
                    metadata.ColorSpace = colorSpace == 1 ? "sRGB" : "Uncalibrated";
                }

                // Note: CameraSettings is a computed property - no need to set it manually
                // The property will automatically build the summary from Aperture, ShutterSpeed, Iso, FocalLength
            }
        }

        private void ExtractImageInfo(IEnumerable<MetadataExtractor.Directory> directories, ImageMetadata metadata)
        {
            var exifSubDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubDirectory != null)
            {
                // Image Description
                metadata.Description = exifSubDirectory.GetString(ExifSubIfdDirectory.TagImageDescription)?.Trim();
            }
        }

        private string GetFlashDescription(int flashValue)
        {
            return flashValue switch
            {
                0x0000 => "No Flash",
                0x0001 => "Flash Fired",
                0x0005 => "Flash Fired, Return not detected",
                0x0007 => "Flash Fired, Return detected",
                0x0008 => "On, Did not fire",
                0x0009 => "On, Fired",
                0x000D => "On, Return not detected",
                0x000F => "On, Return detected",
                0x0010 => "Off, Did not fire",
                0x0014 => "Off, Did not fire, Return not detected",
                0x0018 => "Auto, Did not fire",
                0x0019 => "Auto, Fired",
                0x001D => "Auto, Fired, Return not detected",
                0x001F => "Auto, Fired, Return detected",
                0x0020 => "No flash function",
                0x0030 => "Off, No flash function",
                0x0041 => "Fired, Red-eye reduction",
                0x0045 => "Fired, Red-eye reduction, Return not detected",
                0x0047 => "Fired, Red-eye reduction, Return detected",
                0x0049 => "On, Red-eye reduction",
                0x004D => "On, Red-eye reduction, Return not detected",
                0x004F => "On, Red-eye reduction, Return detected",
                0x0050 => "Off, Red-eye reduction",
                0x0058 => "Auto, Did not fire, Red-eye reduction",
                0x0059 => "Auto, Fired, Red-eye reduction",
                0x005D => "Auto, Fired, Red-eye reduction, Return not detected",
                0x005F => "Auto, Fired, Red-eye reduction, Return detected",
                _ => $"Unknown ({flashValue:X4})"
            };
        }

        private string GetMeteringModeDescription(int meteringMode)
        {
            return meteringMode switch
            {
                0 => "Unknown",
                1 => "Average",
                2 => "Center Weighted Average",
                3 => "Spot",
                4 => "Multi Spot",
                5 => "Pattern",
                6 => "Partial",
                255 => "Other",
                _ => $"Unknown ({meteringMode})"
            };
        }

        private string GetExposureModeDescription(int exposureMode)
        {
            return exposureMode switch
            {
                0 => "Auto",
                1 => "Manual",
                2 => "Auto Bracket",
                _ => $"Unknown ({exposureMode})"
            };
        }

        private string GetSceneCaptureTypeDescription(int sceneType)
        {
            return sceneType switch
            {
                0 => "Standard",
                1 => "Landscape",
                2 => "Portrait",
                3 => "Night Scene",
                _ => $"Unknown ({sceneType})"
            };
        }

        private string FormatShutterSpeed(decimal shutterSpeed)
        {
            if (shutterSpeed >= 1)
                return shutterSpeed.ToString("F1");
            else
                return $"1/{Math.Round(1 / shutterSpeed)}";
        }

        public override string[] GetSupportedFormats()
        {
            return new[] { ".jpg", ".jpeg", ".tiff", ".tif", ".cr2", ".nef", ".orf", ".arw", ".dng" };
        }
    }
}