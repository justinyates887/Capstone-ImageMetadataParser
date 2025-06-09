using ImageMetadataParser.Data;
using Microsoft.EntityFrameworkCore;

namespace ImageMetadataParser.Services
{
    public class KeywordService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<KeywordService> _logger;

        // Common stop words to filter out
        private readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "are", "as", "at", "be", "by", "for", "from",
            "has", "he", "in", "is", "it", "its", "of", "on", "that", "the",
            "to", "was", "will", "with", "image", "photo", "picture", "file"
        };

        public KeywordService(AppDbContext context, ILogger<KeywordService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public List<string> ExtractKeywordsFromMetadata(ImageMetadata metadata)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Extract from existing keywords field
                if (!string.IsNullOrEmpty(metadata.Keywords))
                {
                    var existingKeywords = metadata.Keywords.Split(',', ';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => CleanKeyword(k))
                        .Where(k => !string.IsNullOrEmpty(k));
                    
                    foreach (var keyword in existingKeywords)
                        keywords.Add(keyword);
                }

                // Extract from camera make/model
                if (!string.IsNullOrEmpty(metadata.CameraMake))
                    keywords.Add(CleanKeyword(metadata.CameraMake));
                
                if (!string.IsNullOrEmpty(metadata.CameraModel))
                    keywords.Add(CleanKeyword(metadata.CameraModel));

                // Extract from location
                if (!string.IsNullOrEmpty(metadata.LocationName))
                {
                    var locationParts = metadata.LocationName.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => CleanKeyword(l))
                        .Where(l => !string.IsNullOrEmpty(l));
                    
                    foreach (var location in locationParts)
                        keywords.Add(location);
                }

                // Extract from artist/copyright
                if (!string.IsNullOrEmpty(metadata.Artist))
                    keywords.Add(CleanKeyword(metadata.Artist));

                // Extract from description
                if (!string.IsNullOrEmpty(metadata.Description))
                {
                    var descriptionWords = ExtractWordsFromText(metadata.Description);
                    foreach (var word in descriptionWords)
                        keywords.Add(word);
                }

                // Extract from AI analysis
                if (!string.IsNullOrEmpty(metadata.AiAnalysis))
                {
                    var aiWords = ExtractWordsFromText(metadata.AiAnalysis);
                    foreach (var word in aiWords)
                        keywords.Add(word);
                }

                // Add technical keywords based on camera settings
                AddTechnicalKeywords(metadata, keywords);

                // Remove stop words and normalize
                var cleanedKeywords = keywords
                    .Where(k => !_stopWords.Contains(k) && k.Length > 2)
                    .ToList();

                return NormalizeKeywords(cleanedKeywords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting keywords from metadata for {FileName}", metadata.FileName);
                return new List<string>();
            }
        }

        public List<string> NormalizeKeywords(List<string> keywords)
        {
            return keywords
                .Select(k => CleanKeyword(k))
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();
        }

        public async Task<List<string>> GetPopularKeywordsAsync(int count = 50)
        {
            try
            {
                var imageRecords = await _context.ImageMetadataRecords
                    .Where(m => !string.IsNullOrEmpty(m.Keywords))
                    .Select(m => m.Keywords!)
                    .ToListAsync();

                var popularKeywords = imageRecords
                    .SelectMany(keywords => keywords.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => CleanKeyword(k))
                        .Where(k => !string.IsNullOrEmpty(k)))
                    .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .Take(count)
                    .Select(g => g.Key)
                    .ToList();

                return popularKeywords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular keywords");
                return new List<string>();
            }
        }

        public async Task SaveKeywordsAsync(int imageId, List<string> keywords)
        {
            try
            {
                var image = await _context.ImageMetadataRecords.FindAsync(imageId);
                if (image != null)
                {
                    image.Keywords = string.Join(", ", NormalizeKeywords(keywords));
                    image.Touch();
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving keywords for image {ImageId}", imageId);
                throw;
            }
        }

        private string CleanKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return string.Empty;

            // Remove special characters, trim, and convert to title case
            var cleaned = new string(keyword.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-' || c == '_')
                .ToArray())
                .Trim()
                .ToLowerInvariant();

            return cleaned;
        }

        private List<string> ExtractWordsFromText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            return text.Split(new char[] { ' ', '\t', '\n', '\r', '.', ',', ';', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => CleanKeyword(w))
                .Where(w => !string.IsNullOrEmpty(w) && w.Length > 2 && !_stopWords.Contains(w))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void AddTechnicalKeywords(ImageMetadata metadata, HashSet<string> keywords)
        {
            // Add camera type keywords
            if (metadata.CameraMake?.ToLowerInvariant().Contains("canon") == true)
                keywords.Add("canon");
            if (metadata.CameraMake?.ToLowerInvariant().Contains("nikon") == true)
                keywords.Add("nikon");
            if (metadata.CameraMake?.ToLowerInvariant().Contains("sony") == true)
                keywords.Add("sony");

            // Add technical keywords based on settings
            if (metadata.Iso.HasValue)
            {
                if (metadata.Iso >= 3200)
                    keywords.Add("high-iso");
                else if (metadata.Iso <= 200)
                    keywords.Add("low-iso");
            }

            if (metadata.Aperture.HasValue)
            {
                if (metadata.Aperture <= 2.8m)
                    keywords.Add("wide-aperture");
                else if (metadata.Aperture >= 8m)
                    keywords.Add("narrow-aperture");
            }

            if (metadata.FocalLength.HasValue)
            {
                if (metadata.FocalLength <= 35m)
                    keywords.Add("wide-angle");
                else if (metadata.FocalLength >= 85m)
                    keywords.Add("telephoto");
            }

            // Add GPS-based keywords
            if (metadata.GpsLatitude.HasValue && metadata.GpsLongitude.HasValue)
            {
                keywords.Add("geotagged");
                keywords.Add("location-data");
            }
        }
    }
}