using Microsoft.Extensions.Options;

namespace pqy_server.Services.AiService
{
    public class AiProviderFactory
    {
        private readonly IEnumerable<IAiProviderService> _providers;
        private readonly AiConfigurationSettings _config;

        public AiProviderFactory(IEnumerable<IAiProviderService> providers, IOptions<AiConfigurationSettings> config)
        {
            _providers = providers;
            _config = config.Value;
        }

        public IAiProviderService GetActiveProvider()
        {
            var model = _config.OmniAi.Model.ToLowerInvariant();
            string activeProviderName;

            if (model.Contains("gemini"))
            {
                activeProviderName = "Gemini";
            }
            else if (model.Contains("gpt") || model.Contains("o1") || model.Contains("o3"))
            {
                activeProviderName = "OpenAI";
            }
            else if (model.Contains("claude"))
            {
                activeProviderName = "Claude";
            }
            else
            {
                throw new InvalidOperationException($"Could not determine AI provider from model name: {_config.OmniAi.Model}");
            }

            var provider = _providers.FirstOrDefault(p => p.ProviderName.Equals(activeProviderName, StringComparison.OrdinalIgnoreCase));
            
            if (provider == null)
            {
                throw new InvalidOperationException($"No active implementation found for AI Provider: {activeProviderName}");
            }
            
            return provider;
        }

        public string GetSystemPrompt()
        {
            return _config.GetSystemPrompt();
        }

        public string GetUserPromptTemplate()
        {
            return _config.GetUserPromptTemplate();
        }

    }
}
