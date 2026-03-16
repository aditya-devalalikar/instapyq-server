using Microsoft.AspNetCore.Http;

namespace pqy_server.Services
{
    public interface IStorageService
    {
        /// <summary>
        /// Uploads a file to the configured storage provider.
        /// </summary>
        Task<string> UploadFileAsync(IFormFile file, string key);

        /// <summary>
        /// Deletes a file from the configured storage provider.
        /// </summary>
        Task DeleteAsync(string key);

        /// <summary>
        /// Generates a presigned URL for reading a file.
        /// </summary>
        string GetPresignedFileUrl(string key, int expiryMinutes = 5);
    }
}
