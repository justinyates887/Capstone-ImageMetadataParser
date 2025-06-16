using ImageMetadataParser.Data;
using ImageMetadataParser.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Web;

namespace ImageMetadataParser.Pages
{
    public partial class Upload : ComponentBase
    {
        private const int MAX_FILES_UI = 10;
        private const int MAX_FILE_SIZE_BYTES = 52428800; // 50MB

        [Inject] protected ImageUploadService UploadService { get; set; } = default!;
        [Inject] protected CsvExportService CsvExportService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        protected List<IBrowserFile> SelectedFiles { get; set; } = new();
        protected List<ImageMetadata> ProcessedImages { get; set; } = new();
        protected ImageMetadata? SelectedImageForModal { get; set; }

        protected bool IsProcessing { get; set; } = false;
        protected bool IsDragOver { get; set; } = false;
        protected int ProcessingProgress { get; set; } = 0;
        protected int CurrentFileIndex { get; set; } = 0;
        protected int TotalFiles { get; set; } = 0;

        // Filtering and sorting
        protected string SearchFilter { get; set; } = string.Empty;
        protected string SelectedMetadataType { get; set; } = string.Empty;
        protected string SortBy { get; set; } = "CreatedAt";
        protected bool SortDescending { get; set; } = true;

        // Pagination
        protected int CurrentPage { get; set; } = 1;
        protected int PageSize { get; set; } = 25;
        protected int TotalPages => (int)Math.Ceiling((double)FilteredImages.Count() / PageSize);

        protected IEnumerable<ImageMetadata> FilteredImages
        {
            get
            {
                var results = ProcessedImages ?? new List<ImageMetadata>();

                // Apply search filter
                if (!string.IsNullOrEmpty(SearchFilter))
                {
                    results = results.Where(img =>
                        img.FileName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                        (img.Keywords?.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (img.Description?.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (img.AiAnalysis?.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (img.LocationName?.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (img.Artist?.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (img.CameraMake?.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (img.CameraModel?.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ?? false)
                    ).ToList();
                }

                // Apply metadata type filter
                if (!string.IsNullOrEmpty(SelectedMetadataType))
                {
                    results = SelectedMetadataType switch
                    {
                        "EXIF" => results.Where(img => !string.IsNullOrEmpty(img.ExifData)).ToList(),
                        "XMP" => results.Where(img => !string.IsNullOrEmpty(img.XmpData)).ToList(),
                        "AI" => results.Where(img => !string.IsNullOrEmpty(img.AiAnalysis)).ToList(),
                        _ => results.ToList()
                    };
                }

                // Apply sorting
                IEnumerable<ImageMetadata> sortedResults = SortBy switch
                {
                    "FileName" => SortDescending
                        ? results.OrderByDescending(x => x.FileName)
                        : results.OrderBy(x => x.FileName),
                    "FileSize" => SortDescending
                        ? results.OrderByDescending(x => x.FileSizeBytes)
                        : results.OrderBy(x => x.FileSizeBytes),
                    "DateTaken" => SortDescending
                        ? results.OrderByDescending(x => x.DateTaken ?? DateTime.MinValue)
                        : results.OrderBy(x => x.DateTaken ?? DateTime.MinValue),
                    _ => SortDescending
                        ? results.OrderByDescending(x => x.CreatedAt)
                        : results.OrderBy(x => x.CreatedAt)
                };

                // Apply pagination
                return sortedResults.Skip((CurrentPage - 1) * PageSize).Take(PageSize);
            }
        }

        protected void HandleFileSelection(InputFileChangeEventArgs e)
        {
            Console.WriteLine($"HandleFileSelection called with {e.FileCount} files");

            try
            {
                var files = e.GetMultipleFiles(MAX_FILES_UI).ToList();
                Console.WriteLine($"Retrieved {files.Count} files from event");

                // Validate files
                var validFiles = new List<IBrowserFile>();
                var errors = new List<string>();

                foreach (var file in files)
                {
                    Console.WriteLine($"Processing file: {file.Name}, Type: {file.ContentType}, Size: {file.Size}");

                    if (IsValidImageFile(file))
                    {
                        if (file.Size <= MAX_FILE_SIZE_BYTES)
                        {
                            validFiles.Add(file);
                            Console.WriteLine($"File {file.Name} added to valid files");
                        }
                        else
                        {
                            errors.Add($"{file.Name}: File too large (max 50MB)");
                            Console.WriteLine($"File {file.Name} rejected - too large");
                        }
                    }
                    else
                    {
                        errors.Add($"{file.Name}: Invalid file type");
                        Console.WriteLine($"File {file.Name} rejected - invalid type");
                    }
                }

                SelectedFiles = validFiles;
                Console.WriteLine($"SelectedFiles set to {SelectedFiles.Count} files");

                // Force UI update
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleFileSelection: {ex.Message}");
            }
        }

        private bool IsValidImageFile(IBrowserFile file)
        {
            var validTypes = new[] {
                "image/jpeg", "image/jpg", "image/png", "image/gif",
                "image/bmp", "image/webp", "image/svg+xml", "image/tiff",
                "image/x-icon", "image/avif", "image/heic", "image/heif"
            };

            var validExtensions = new[] {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
                ".svg", ".tiff", ".tif", ".ico", ".avif", ".heic", ".heif"
            };

            return validTypes.Contains(file.ContentType) ||
                   validExtensions.Any(ext => file.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        protected void HandleDragOver()
        {
            IsDragOver = true;
        }

        protected void HandleDragLeave()
        {
            IsDragOver = false;
        }

        protected async Task HandleDrop(DragEventArgs e)
        {
            IsDragOver = false;
            // Enhanced drag/drop handling can be implemented with JavaScript interop
        }

        protected void RemoveFile(IBrowserFile file)
        {
            SelectedFiles?.Remove(file);
            StateHasChanged();
        }

        protected async Task ProcessFiles()
        {
            if (SelectedFiles?.Count > 0)
            {
                IsProcessing = true;
                ProcessingProgress = 0;
                CurrentFileIndex = 0;
                TotalFiles = SelectedFiles.Count;

                try
                {
                    var results = new List<ImageMetadata>();

                    foreach (var file in SelectedFiles)
                    {
                        CurrentFileIndex++;
                        ProcessingProgress = (int)((double)CurrentFileIndex / TotalFiles * 100);
                        StateHasChanged();

                        try
                        {
                            var result = await UploadService.ProcessImageAsync(file);
                            if (result != null)
                            {
                                results.Add(result);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Create a failed result for tracking
                            var failedResult = new ImageMetadata
                            {
                                FileName = file.Name,
                                FileSizeBytes = file.Size,
                                MimeType = file.ContentType,
                                ProcessingStatus = "Failed",
                                ErrorMessage = ex.Message
                            };
                            results.Add(failedResult);
                        }

                        await Task.Delay(100); // Small delay for UI responsiveness
                    }

                    ProcessedImages = results;
                    SelectedFiles.Clear();
                    CurrentPage = 1; // Reset to first page

                    await JSRuntime.InvokeVoidAsync("showToast",
                        $"Successfully processed {results.Count(r => r.ProcessingStatus == "Completed")} of {results.Count} images",
                        "success");
                }
                catch (Exception ex)
                {
                    await JSRuntime.InvokeVoidAsync("showToast",
                        $"Error processing files: {ex.Message}", "danger");
                }
                finally
                {
                    IsProcessing = false;
                    ProcessingProgress = 0;
                    StateHasChanged();
                }
            }
        }

        protected async Task ExportToCsv()
        {
            if (ProcessedImages?.Count > 0)
            {
                try
                {
                    var csvData = await CsvExportService.ExportToCsvAsync(FilteredImages);
                    var fileName = $"image_metadata_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                    await JSRuntime.InvokeVoidAsync("downloadFile", fileName, csvData);
                    await JSRuntime.InvokeVoidAsync("showToast", "CSV export completed", "success");
                }
                catch (Exception ex)
                {
                    await JSRuntime.InvokeVoidAsync("showToast",
                        $"Error exporting CSV: {ex.Message}", "danger");
                }
            }
        }

        protected void ClearResults()
        {
            ProcessedImages?.Clear();
            CurrentPage = 1;
            StateHasChanged();
        }

        protected void ToggleSortDirection()
        {
            SortDescending = !SortDescending;
            CurrentPage = 1;
            StateHasChanged();
        }

        protected void ChangePage(int page)
        {
            if (page >= 1 && page <= TotalPages)
            {
                CurrentPage = page;
                StateHasChanged();
            }
        }

        protected void ShowMetadataDetails(ImageMetadata image)
        {
            SelectedImageForModal = image;
            StateHasChanged();
        }

        protected async Task CopyMetadataToClipboard(ImageMetadata? image)
        {
            if (image == null) return;

            var metadata = $"Filename: {image.FileName}\n";
            metadata += $"Size: {image.FileSize}\n";
            metadata += $"Dimensions: {image.Dimensions}\n";
            metadata += $"Date Taken: {image.DateTaken}\n";
            metadata += $"Camera: {image.CameraMake} {image.CameraModel}\n";
            metadata += $"Settings: {image.CameraSettings}\n";
            metadata += $"Location: {image.LocationName}\n";
            metadata += $"GPS: {image.GpsCoordinates}\n";
            metadata += $"Keywords: {image.Keywords}\n";
            metadata += $"AI Analysis: {image.AiAnalysis}\n";

            await JSRuntime.InvokeVoidAsync("copyToClipboard", metadata);
        }

        protected string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }

        protected string FormatMetadataForDisplay(string metadata)
        {
            if (string.IsNullOrEmpty(metadata)) return string.Empty;

            try
            {
                // Try to parse as JSON and format nicely
                var jsonDoc = System.Text.Json.JsonDocument.Parse(metadata);
                var formatted = System.Text.Json.JsonSerializer.Serialize(jsonDoc, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                return $"<pre>{HttpUtility.HtmlEncode(formatted)}</pre>";
            }
            catch
            {
                // Fallback to simple line-by-line formatting
                var lines = metadata.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var formatted = lines.Select(line =>
                {
                    if (line.Contains(':'))
                    {
                        var parts = line.Split(':', 2);
                        return $"<strong>{HttpUtility.HtmlEncode(parts[0].Trim())}:</strong> {HttpUtility.HtmlEncode(parts[1].Trim())}<br>";
                    }
                    return $"{HttpUtility.HtmlEncode(line)}<br>";
                });

                return string.Join("", formatted);
            }
        }
    }
}