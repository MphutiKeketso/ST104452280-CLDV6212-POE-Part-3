// FUNCTION 4: Write to Azure File Shares
using Azure.Storage.Files.Shares;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ABCRetailersFunctions.Functions
{
    public class FileShareFunction
    {
        private readonly ILogger _logger;
        private readonly ShareServiceClient _shareServiceClient;

        public FileShareFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FileShareFunction>();

            var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnection")
                ?? throw new InvalidOperationException("AzureStorageConnection not configured");

            _shareServiceClient = new ShareServiceClient(connectionString);
        }

        [Function("UploadContractFile")]
        public async Task<HttpResponseData> UploadContractFile(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "files/contracts")] HttpRequestData req)
        {
            _logger.LogInformation("UploadContractFile function triggered");

            try
            {
                var boundary = GetBoundary(req.Headers.GetValues("Content-Type").FirstOrDefault());
                var reader = new MultipartReader(boundary, req.Body);

                var section = await reader.ReadNextSectionAsync();

                if (section == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("No file uploaded");
                    return badResponse;
                }

                var fileName = GetFileName(section.ContentDisposition);
                var shareName = "contracts";
                var directoryName = "payments";

                var shareClient = _shareServiceClient.GetShareClient(shareName);
                await shareClient.CreateIfNotExistsAsync();

                var directoryClient = shareClient.GetDirectoryClient(directoryName);
                await directoryClient.CreateIfNotExistsAsync();

                var newFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{fileName}";
                var fileClient = directoryClient.GetFileClient(newFileName);

                using var memoryStream = new MemoryStream();
                await section.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                await fileClient.CreateAsync(memoryStream.Length);
                await fileClient.UploadAsync(memoryStream);

                _logger.LogInformation($"Contract file uploaded successfully: {newFileName}");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    fileName = newFileName,
                    shareName = shareName,
                    directoryName = directoryName
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading contract file: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        private static string GetBoundary(string contentType)
        {
            var elements = contentType.Split(' ');
            var element = elements.First(entry => entry.StartsWith("boundary="));
            var boundary = element.Substring("boundary=".Length);
            return boundary.Trim('"');
        }

        private static string GetFileName(string contentDisposition)
        {
            return contentDisposition
                .Split(';')
                .FirstOrDefault(x => x.Trim().StartsWith("filename="))
                ?.Split('=')[1]
                .Trim('"');
        }

        [Function("DownloadContractFile")]
        public async Task<HttpResponseData> DownloadContractFile(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "files/contracts/{fileName}")] HttpRequestData req,
            string fileName)
        {
            _logger.LogInformation($"DownloadContractFile triggered for: {fileName}");

            try
            {
                var shareName = "contracts";
                var directoryName = "payments";

                var shareClient = _shareServiceClient.GetShareClient(shareName);
                var directoryClient = shareClient.GetDirectoryClient(directoryName);
                var fileClient = directoryClient.GetFileClient(fileName);

                var downloadResponse = await fileClient.DownloadAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/octet-stream");
                response.Headers.Add("Content-Disposition", $"attachment; filename={fileName}");

                await downloadResponse.Value.Content.CopyToAsync(response.Body);

                _logger.LogInformation($"Contract file downloaded successfully: {fileName}");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading contract file: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("ListContractFiles")]
        public async Task<HttpResponseData> ListContractFiles(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "files/contracts")] HttpRequestData req)
        {
            _logger.LogInformation("ListContractFiles triggered");

            try
            {
                var shareName = "contracts";
                var directoryName = "payments";

                var shareClient = _shareServiceClient.GetShareClient(shareName);
                var directoryClient = shareClient.GetDirectoryClient(directoryName);

                var files = new List<string>();

                await foreach (var item in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!item.IsDirectory)
                    {
                        files.Add(item.Name);
                    }
                }

                _logger.LogInformation($"Found {files.Count} contract files");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    files = files,
                    count = files.Count
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error listing contract files: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}