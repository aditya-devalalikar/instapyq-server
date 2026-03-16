namespace pqy_server.Services.AiService
{
    public class AiConfigurationSettings
    {
        public OmniAiConfiguration OmniAi { get; set; } = new OmniAiConfiguration();
        public string[] SystemPromptLines { get; set; } = Array.Empty<string>();
        public string[] UserPromptLines { get; set; } = Array.Empty<string>();

        public string GetSystemPrompt()
        {
            return string.Join("\n", SystemPromptLines);
        }

        public string GetUserPromptTemplate()
        {
            return string.Join("\n", UserPromptLines);
        }
    }

    public class OmniAiConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-3.1-flash-lite-preview"; // updated
    }
}
