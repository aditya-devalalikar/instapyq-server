using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using pqy_server.Models.Questions;

namespace pqy_server.Services.AiService.Providers
{
    public class OpenAiProviderService : IAiProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly AiConfigurationSettings _config;
        private readonly ILogger<OpenAiProviderService> _logger;

        public string ProviderName => "OpenAI";

        public OpenAiProviderService(HttpClient httpClient, IOptions<AiConfigurationSettings> config, ILogger<OpenAiProviderService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
            
            _httpClient.BaseAddress = new Uri("https://api.openai.com/");
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
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.OmniAi.ApiKey);
            requestMessage.Content = requestContent;

            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("OpenAI API Quota Exceeded (429 Too Many Requests).");
                    throw new HttpRequestException("OpenAI API QUOTA EXCEEDED: You have hit the rate limit. Please wait and try again later.");
                }

                _logger.LogError("OpenAI API Error: {Error}", error);
                throw new HttpRequestException($"OpenAI API request failed with status code {response.StatusCode}: {error}");
            }

            var openAiResponse = await response.Content.ReadFromJsonAsync<OpenAiResponse>();
            
            var text = openAiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception("OpenAI API returned an empty or unparseable response.");
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

        // DTOs for OpenAI Response
        private class OpenAiResponse
        {
            public List<Choice> Choices { get; set; } = new();
        }

        private class Choice
        {
            public Message Message { get; set; } = new();
        }

        private class Message
        {
            public string Content { get; set; } = string.Empty;
        }
    }
}
