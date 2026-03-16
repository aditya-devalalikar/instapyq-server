using pqy_server.Models.Questions;

namespace pqy_server.Services.AiService
{
    public interface IAiProviderService
    {
        string ProviderName { get; }
        Task<string> GenerateExplanationAsync(Question question, string systemPrompt, string userPromptTemplate);
        Task<string> SubmitBatchJobAsync(List<Question> questions, string systemPrompt, string userPromptTemplate);
        Task<(string State, string? OutputFile)> GetBatchJobStatusAsync(string jobId);
        Task<List<(int QuestionId, string Explanation)>> DownloadBatchResultsAsync(string outputFileUri);
    }
}
