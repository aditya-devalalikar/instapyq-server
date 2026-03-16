using Amazon.S3;
using Amazon.S3.Model;
using System.Net.Http.Headers;
using Serilog;

namespace pqy_server.Services
{
    public class StorageService : IStorageService
    {
        private readonly IAmazonS3 _s3;
        private readonly string _bucketName;
        private readonly HttpClient _http;

        public StorageService(IConfiguration config)
        {
            var storageSettings = config.GetSection("StorageSettings");
            var activeProvider = storageSettings["ActiveProvider"] ?? "Railway";
            var settings = storageSettings.GetSection(activeProvider);

            if (!settings.Exists())
            {
                throw new Exception($"Storage provider configuration for '{activeProvider}' not found.");
            }

            _bucketName = settings["BucketName"]!;

            var s3Config = new AmazonS3Config
            {
                ServiceURL = settings["ServiceURL"]!,
                ForcePathStyle = settings.GetValue<bool>("ForcePathStyle", true)
            };

            _s3 = new AmazonS3Client(
                settings["AccessKey"],
                settings["SecretKey"],
                s3Config);

            _http = new HttpClient();
        }

        /// <summary>
        /// Uploads a file to a private bucket using a presigned URL
        /// </summary>
        public async Task<string> UploadFileAsync(IFormFile file, string key)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.");

            // Generate presigned PUT URL
            var presignedUrl = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(10)
            });

            // Upload using HttpClient
            await using var stream = file.OpenReadStream();
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

            var response = await _http.PutAsync(presignedUrl, content);
            Log.Information("File uploaded to storage. Key={Key}, ContentType={ContentType}", key, file.ContentType);

            return key; // Save this key in your DB
        }

        /// <summary>
        /// Delete a file from the bucket
        /// </summary>
        public async Task DeleteAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key is required.");

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3.DeleteObjectAsync(deleteRequest);

            Log.Information("File deleted from storage. Key={Key}", key);
        }

        /// <summary>
        /// Generate pre-signed URL for GET (read)
        /// </summary>
        public string GetPresignedFileUrl(string key, int expiryMinutes = 5)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key is required.");

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
            };

            return _s3.GetPreSignedURL(request);
        }
    }
}
