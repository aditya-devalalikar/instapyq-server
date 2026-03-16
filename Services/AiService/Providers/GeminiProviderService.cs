using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using pqy_server.Models.Questions;

namespace pqy_server.Services.AiService.Providers
{
    public class GeminiProviderService : IAiProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly AiConfigurationSettings _config;
        private readonly ILogger<GeminiProviderService> _logger;

        public string ProviderName => "Gemini";

        public GeminiProviderService(HttpClient httpClient, IOptions<AiConfigurationSettings> config, ILogger<GeminiProviderService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
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

            var requestBody = new Dictionary<string, object>
            {
                ["system_instruction"] = new { parts = new[] { new { text = systemPrompt } } },
                ["contents"] = new[] { new { parts = new[] { new { text = userPrompt } } } }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_config.OmniAi.Model}:generateContent?key={_config.OmniAi.ApiKey}";

            var response = await _httpClient.PostAsync(url, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Gemini API Quota Exceeded (429 Too Many Requests).");
                    throw new HttpRequestException("Gemini API QUOTA EXCEEDED: You have hit the free tier rate limit. Please wait and try again later.");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    _logger.LogWarning("Gemini API Overloaded (503 Service Unavailable).");
                    throw new HttpRequestException("Gemini API OVERLOADED: Google's AI model is currently under heavy load. Please try again in 30-60 seconds.");
                }

                _logger.LogError("Gemini API Error: {Error}", error);
                throw new HttpRequestException($"Gemini API request failed with status code {response.StatusCode}: {error}");
            }

            var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>();
            
            var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception("Gemini API returned an empty or unparseable response.");
            }

            // Remove markdown code blocks if Gemini tried to wrap HTML in ```html ... ```
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

        public async Task<string> SubmitBatchJobAsync(List<Question> questions, string systemPrompt, string userPromptTemplate)
        {
            if (string.IsNullOrWhiteSpace(_config.OmniAi?.ApiKey))
                throw new InvalidOperationException("Gemini API Key is not configured.");

            // 1. Prepare JSONL content (AI Studio format uses 'key' and 'request')
            var jsonlBuilder = new StringBuilder();
            foreach (var q in questions)
            {
                var userPrompt = string.Format(
                    userPromptTemplate, 
                    q.QuestionText ?? "", 
                    q.OptionA ?? "", 
                    q.OptionB ?? "", 
                    q.OptionC ?? "", 
                    q.OptionD ?? "", 
                    q.CorrectOption ?? ""
                );

                var requestPayload = new
                {
                    key = q.QuestionId.ToString(),
                    request = new
                    {
                        system_instruction = new
                        {
                            parts = new[] { new { text = systemPrompt } }
                        },
                        contents = new[]
                        {
                            new { role = "user", parts = new[] { new { text = userPrompt } } }
                        }
                    }
                };
                jsonlBuilder.AppendLine(JsonSerializer.Serialize(requestPayload));
            }

            // 2. Upload JSONL using Google's Resumable Upload Protocol (2-step)
            var jsonlBytes = Encoding.UTF8.GetBytes(jsonlBuilder.ToString());

            // Step 2a: Initiate the resumable upload
            var initRequest = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/upload/v1beta/files?key={_config.OmniAi.ApiKey}");
            initRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
            initRequest.Headers.Add("X-Goog-Upload-Command", "start");
            initRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", jsonlBytes.Length.ToString());
            initRequest.Headers.Add("X-Goog-Upload-Header-Content-Type", "application/jsonl");
            initRequest.Content = new StringContent(
                JsonSerializer.Serialize(new { file = new { display_name = $"batch_{DateTime.UtcNow:yyyyMMddHHmmss}" } }),
                Encoding.UTF8, "application/json");

            var initResponse = await _httpClient.SendAsync(initRequest);
            if (!initResponse.IsSuccessStatusCode)
            {
                var error = await initResponse.Content.ReadAsStringAsync();
                _logger.LogError("Gemini Upload Initiation Failed: {Error}", error);
                throw new HttpRequestException($"Gemini Upload Initiation failed: {error}");
            }

            // Get the upload URL from the response header
            var uploadUrl = initResponse.Headers.TryGetValues("X-Goog-Upload-URL", out var urls) 
                ? urls.FirstOrDefault() 
                : throw new Exception("Gemini did not return an upload URL in X-Goog-Upload-URL header.");

            // Step 2b: Upload the actual file bytes
            var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
            uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
            uploadRequest.Content = new ByteArrayContent(jsonlBytes);
            uploadRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/jsonl");
            uploadRequest.Content.Headers.ContentLength = jsonlBytes.Length;

            var uploadResponse = await _httpClient.SendAsync(uploadRequest);

            if (!uploadResponse.IsSuccessStatusCode)
            {
                var error = await uploadResponse.Content.ReadAsStringAsync();
                _logger.LogError("Gemini Batch File Upload Failed: {Error}", error);
                throw new HttpRequestException($"Gemini Batch File Upload failed: {error}");
            }
            
            var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
            var fileUri = uploadResult.GetProperty("file").GetProperty("uri").GetString() 
                ?? throw new Exception("Failed to get file URI from upload response.");
            var fileName = uploadResult.GetProperty("file").GetProperty("name").GetString()
                ?? throw new Exception("Failed to get file name from upload response.");
            
            _logger.LogInformation("Gemini Batch File Uploaded: URI={Uri}, Name={Name}", fileUri, fileName);

            // 2c. Wait for file to become ACTIVE (large files like 16k questions can take several minutes)
            bool fileActive = false;
            for (int i = 0; i < 36; i++) // Max 6 minutes (36 x 10s)
            {
                await Task.Delay(10000);
                var checkUrl = $"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={_config.OmniAi.ApiKey}";
                var checkResponse = await _httpClient.GetAsync(checkUrl);
                if (checkResponse.IsSuccessStatusCode)
                {
                    var fileStatus = await checkResponse.Content.ReadFromJsonAsync<JsonElement>();
                    var state = fileStatus.GetProperty("state").GetString();
                    _logger.LogInformation("Gemini File State after {Seconds}s: {State}", (i + 1) * 10, state);
                    if (state == "ACTIVE") { fileActive = true; break; }
                    if (state == "FAILED") throw new Exception("Gemini file processing failed during upload.");
                }
            }
            if (!fileActive)
                throw new Exception("Gemini file did not become ACTIVE within 6 minutes.");

            // 3. Create the Batch Job using the Gemini Batch API
            var jobRequestBody = new
            {
                model = $"models/{_config.OmniAi.Model}",
                src_file = new { name = fileName }
            };

            var jobUrl = $"https://generativelanguage.googleapis.com/v1alpha/batches?key={_config.OmniAi.ApiKey}";
            var jobContent = new StringContent(JsonSerializer.Serialize(jobRequestBody), Encoding.UTF8, "application/json");
            var jobResponse = await _httpClient.PostAsync(jobUrl, jobContent);
            
            if (!jobResponse.IsSuccessStatusCode)
            {
                var error = await jobResponse.Content.ReadAsStringAsync();
                _logger.LogError("Gemini Batch Job Submission Failed ({StatusCode}): {Error}", jobResponse.StatusCode, error);
                throw new HttpRequestException($"Gemini Batch Job Submission failed ({jobResponse.StatusCode}): {error}");
            }

            var jobResult = await jobResponse.Content.ReadFromJsonAsync<JsonElement>();
            return jobResult.GetProperty("name").GetString() ?? throw new Exception("Failed to get job name.");
        }

        public async Task<(string State, string? OutputFile)> GetBatchJobStatusAsync(string jobId)
        {
            var url = $"https://generativelanguage.googleapis.com/v1alpha/{jobId}?key={_config.OmniAi.ApiKey}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var rawState = result.GetProperty("state").GetString() ?? "UNKNOWN";
            // Normalize state: Gemini returns "JOB_STATE_SUCCEEDED", strip the prefix for consistency
            var state = rawState.StartsWith("JOB_STATE_") ? rawState.Substring("JOB_STATE_".Length) : rawState;

            string? outputFileUri = null;
            if (result.TryGetProperty("dest_file", out var destFile))
            {
                if (destFile.TryGetProperty("uri", out var uriProp))
                    outputFileUri = uriProp.GetString();
                else if (destFile.TryGetProperty("name", out var nameProp))
                    outputFileUri = $"https://generativelanguage.googleapis.com/v1beta/{nameProp.GetString()}";
            }

            return (state, outputFileUri);
        }

        public async Task<List<(int QuestionId, string Explanation)>> DownloadBatchResultsAsync(string outputFileUri)
        {
            var url = $"{outputFileUri}?key={_config.OmniAi.ApiKey}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var results = new List<(int QuestionId, string Explanation)>();

            foreach (var line in lines)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<JsonElement>(line);
                    // AI Studio format uses 'key' as the identifier
                    var keyId = entry.GetProperty("key").GetString();
                    
                    if (int.TryParse(keyId, out int qId))
                    {
                        var responseText = entry.GetProperty("response")
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text").GetString();

                        if (!string.IsNullOrWhiteSpace(responseText))
                        {
                            results.Add((qId, responseText));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse batch result line: {Line}", line);
                }
            }

            return results;
        }

        // DTOs for Gemini Response
        private class GeminiResponse
        {
            public List<Candidate> Candidates { get; set; } = new();
        }

        private class Candidate
        {
            public Content Content { get; set; } = new();
        }

        private class Content
        {
            public List<Part> Parts { get; set; } = new();
        }

        private class Part
        {
            public string Text { get; set; } = string.Empty;
        }
    }
}
