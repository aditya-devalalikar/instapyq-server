using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using pqy_server.Models.Questions;

namespace pqy_server.Services.AiService.Providers
{
    public class ClaudeProviderService : IAiProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly AiConfigurationSettings _config;
        private readonly ILogger<ClaudeProviderService> _logger;

        public string ProviderName => "Claude";

        public ClaudeProviderService(HttpClient httpClient, IOptions<AiConfigurationSettings> config, ILogger<ClaudeProviderService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
            
            _httpClient.BaseAddress = new Uri("https://api.anthropic.com/");
        }

        public async Task<string> GenerateExplanationAsync(Question question, string systemPrompt, string userPromptTemplate)
        {
            if (string.IsNullOrWhiteSpace(_config.OmniAi?.ApiKey))
            {
                throw new InvalidOperationException("AI Provider API Key is not configured.");
            }

            var userPrompt = string.Format(
                userPromptTemplate, 
                question.QuestionText ?? "", 
                question.OptionA ?? "", 
                question.OptionB ?? "", 
                question.OptionC ?? "", 
                question.OptionD ?? "", 
                question.CorrectOption ?? ""
            );

            var requestBody = new
            {
                model = _config.OmniAi.Model,
                max_tokens = 2000,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userPrompt }
                }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "v1/messages");
            requestMessage.Headers.Add("x-api-key", _config.OmniAi.ApiKey);
            requestMessage.Headers.Add("anthropic-version", "2023-06-01");
            requestMessage.Content = requestContent;

            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || 
                    (int)response.StatusCode == 429) // Rate Limit
                {
                    _logger.LogWarning("Anthropic API Quota Exceeded (429 Too Many Requests).");
                    throw new HttpRequestException("Anthropic API QUOTA EXCEEDED: You have hit the rate limit. Please wait and try again later.");
                }

                _logger.LogError("Anthropic API Error: {Error}", error);
                throw new HttpRequestException($"Anthropic API request failed with status code {response.StatusCode}: {error}");
            }

            var claudeResponse = await response.Content.ReadFromJsonAsync<ClaudeResponse>();
            
            var text = claudeResponse?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception("Anthropic API returned an empty or unparseable response.");
            }

            // Remove markdown HTML code blocks if present
            text = text.Trim();
            if (text.StartsWith("```html"))
            {
                text = text.Substring(7);
                if (text.EndsWith("```"))
                {
                    text = text.Substring(0, text.Length - 3);
                }
                text = text.Trim();
            }
            else if (text.StartsWith("```"))
            {
                text = text.Substring(3);
                if (text.EndsWith("```"))
                {
                    text = text.Substring(0, text.Length - 3);
                }
                text = text.Trim();
            }

            return text;
        }

        public Task<string> SubmitBatchJobAsync(List<Question> questions, string systemPrompt, string userPromptTemplate) => throw new NotSupportedException();
        public Task<(string State, string? OutputFile)> GetBatchJobStatusAsync(string jobId) => throw new NotSupportedException();
        public Task<List<(int QuestionId, string Explanation)>> DownloadBatchResultsAsync(string outputFileUri) => throw new NotSupportedException();

        // DTOs for Anthropic Response
        private class ClaudeResponse
        {
            public List<ContentPart> Content { get; set; } = new();
        }

        private class ContentPart
        {
            public string Type { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }
    }
}
