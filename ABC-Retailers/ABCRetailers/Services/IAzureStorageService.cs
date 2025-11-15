using Azure.Data.Tables;

namespace ABCRetailers.Services
{
    public interface IAzureStorageService
    {
        // Table operations
        Task<List<T>> GetAllEntitiesAsync<T>() where T : class, ITableEntity, new(); 
        Task<T?> GetEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity, new(); 
        Task<T> AddEntityAsync<T>(T entity) where T : class, ITableEntity; 
        Task<T> UpdateEntityAsync<T>(T entity) where T : class, ITableEntity;
        Task DeleteEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity, new(); 

        // Blob operations
        Task<string> UploadImageAsync(IFormFile file, string containerName); 
        Task<string> UploadFileAsync(IFormFile file, string containerName); 
        Task DeleteBlobAsync(string blobName, string containerName); 
        Task<string> GetFileUrlAsync(string containerName, string blobName); // [ADDED]

        // Queue operations
        Task SendMessageAsync(string queueName, string message); 
        Task<string?> ReceiveMessageAsync(string queueName); 

        // File Share operations
        Task<string> UploadToFileShareAsync(IFormFile file, string shareName, string directoryName = ""); 
        Task<byte[]> DownloadFromFileShareAsync(string shareName, string fileName, string directoryName = ""); 

        // Initialization
        Task InitializeStorageAsync(); 
    }
}