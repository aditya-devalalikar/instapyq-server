namespace pqy_server.Services
{
    public class MediaUrlBuilder
    {
        private readonly string _publicBaseUrl;

        public MediaUrlBuilder(IConfiguration configuration)
        {
            _publicBaseUrl = configuration["Media:PublicBaseUrl"]
                ?? throw new ArgumentException("Media:PublicBaseUrl is not configured");
        }

        public string Build(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null!;

            // Encode unsafe chars but keep path structure
            var encodedKey = Uri.EscapeUriString(key);

            return $"{_publicBaseUrl}/{encodedKey}";
        }
    }
}
