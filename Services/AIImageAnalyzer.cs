using ImageMetadataParser.Services;

namespace ImageMetadataParser.Services
{
    public class AiImageAnalyzer
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiImageAnalyzer> _logger;

        public AiImageAnalyzer(IConfiguration configuration, ILogger<AiImageAnalyzer> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AiAnalysisResult> AnalyzeImageAsync(Stream imageStream, string fileName)
        {
            try
            {
                // For now, return a mock result since we don't have an AI service configured

                await Task.Delay(100); // Simulate processing time

                var result = new AiAnalysisResult
                {
                    Description = "AI analysis is not configured. This is a placeholder description.",
                    Keywords = new List<string> { "placeholder", "no-ai-service" },
                    Confidence = 0.0f
                };

                _logger.LogInformation("Mock AI analysis completed for {FileName}", fileName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI analysis failed for {FileName}", fileName);
                throw;
            }
        }

        public async Task<List<string>> GenerateKeywordsAsync(Stream imageStream)
        {
            try
            {
                var result = await AnalyzeImageAsync(imageStream, "unnamed");
                return result.Keywords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Keyword generation failed");
                return new List<string>();
            }
        }

        public async Task<string> GenerateDescriptionAsync(Stream imageStream)
        {
            try
            {
                var result = await AnalyzeImageAsync(imageStream, "unnamed");
                return result.Description;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Description generation failed");
                return string.Empty;
            }
        }

        // TODO: Implement actual AI service integration when needed
    }
}