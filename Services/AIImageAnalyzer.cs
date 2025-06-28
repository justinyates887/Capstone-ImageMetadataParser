using System.Text.Json;

namespace ImageMetadataParser.Services
{
    public class AiImageAnalyzer
    {
        private readonly ILogger<AiImageAnalyzer> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public AiImageAnalyzer(ILogger<AiImageAnalyzer> logger, IConfiguration configuration, HttpClient httpClient)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<AiAnalysisResult> AnalyzeImageAsync(Stream imageStream, string fileName)
        {
            _logger.LogInformation("AI analysis requested for file: {FileName}", fileName);

            return new AiAnalysisResult
            {
                Description = "AI image analysis has not been implemented yet.",
                Keywords = new List<string> { "not-implemented", "placeholder" },
                Confidence = 0.0f
            };
        }
    }
}