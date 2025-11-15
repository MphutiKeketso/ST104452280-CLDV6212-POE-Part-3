// FUNCTION 2: Write to Blob Storage
using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetailersFunctions.Functions
{
    public class BlobStorageFunction
    {
        private readonly ILogger _logger;
        private readonly BlobServiceClient _blobServiceClient;

        public BlobStorageFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<BlobStorageFunction>();

            var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnection")
                ?? throw new InvalidOperationException("AzureStorageConnection not configured");

            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        [Function("UploadProductImage")]
        public async Task<HttpResponseData> UploadProductImage(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "images/product")] HttpRequestData req)
        {
            _logger.LogInformation("UploadProductImage function triggered");

            try
            {
                // Get boundary from content-type header
                var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
                if (string.IsNullOrEmpty(contentType))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Content-Type header is missing");
                    return badResponse;
                }

                var boundary = GetBoundary(contentType);
                var reader = new MultipartReader(boundary, req.Body);

                var section = await reader.ReadNextSectionAsync();

                if (section == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("No file uploaded");
                    return badResponse;
                }

                var fileName = GetFileName(section.ContentDisposition);
                if (string.IsNullOrEmpty(fileName))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid file upload");
                    return badResponse;
                }

                // Validate file extension
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync($"File type {extension} is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");
                    return badResponse;
                }

                var containerClient = _blobServiceClient.GetBlobContainerClient("product-images");
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                var newFileName = $"{Guid.NewGuid()}{extension}";
                var blobClient = containerClient.GetBlobClient(newFileName);

                // Read file content into memory stream
                using var memoryStream = new MemoryStream();
                await section.Body.CopyToAsync(memoryStream);

                // Check file size (max 5MB)
                if (memoryStream.Length > 5 * 1024 * 1024)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("File size exceeds 5MB limit");
                    return badResponse;
                }

                memoryStream.Position = 0;
                await blobClient.UploadAsync(memoryStream, overwrite: true);

                var imageUrl = blobClient.Uri.ToString();
                _logger.LogInformation($"Image uploaded successfully: {imageUrl}");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    imageUrl = imageUrl,
                    fileName = newFileName
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading image: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("UploadPaymentProof")]
        public async Task<HttpResponseData> UploadPaymentProof(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "files/payment-proof")] HttpRequestData req)
        {
            _logger.LogInformation("UploadPaymentProof function triggered");

            try
            {
                // Get boundary from content-type header
                var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
                if (string.IsNullOrEmpty(contentType))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Content-Type header is missing");
                    return badResponse;
                }

                var boundary = GetBoundary(contentType);
                var reader = new MultipartReader(boundary, req.Body);

                var section = await reader.ReadNextSectionAsync();

                if (section == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("No file uploaded");
                    return badResponse;
                }

                var fileName = GetFileName(section.ContentDisposition);
                if (string.IsNullOrEmpty(fileName))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid file upload");
                    return badResponse;
                }

                // Validate file extension
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync($"File type {extension} is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");
                    return badResponse;
                }

                var containerClient = _blobServiceClient.GetBlobContainerClient("payment-proofs");
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var newFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid()}{extension}";
                var blobClient = containerClient.GetBlobClient(newFileName);

                // Read file content into memory stream
                using var memoryStream = new MemoryStream();
                await section.Body.CopyToAsync(memoryStream);

                // Check file size (max 10MB)
                if (memoryStream.Length > 10 * 1024 * 1024)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("File size exceeds 10MB limit");
                    return badResponse;
                }

                memoryStream.Position = 0;
                await blobClient.UploadAsync(memoryStream, overwrite: true);

                _logger.LogInformation($"Payment proof uploaded successfully: {newFileName}");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    fileName = newFileName,
                    message = "Payment proof uploaded successfully"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading payment proof: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // Helper method to extract boundary from Content-Type header
        private static string GetBoundary(string contentType)
        {
            var elements = contentType.Split(' ');
            var element = elements.FirstOrDefault(entry => entry.StartsWith("boundary="));

            if (element == null)
            {
                throw new InvalidOperationException("Missing boundary in Content-Type header");
            }

            var boundary = element.Substring("boundary=".Length);
            return boundary.Trim('"');
        }

        // Helper method to extract filename from Content-Disposition header
        private static string? GetFileName(string? contentDisposition)
        {
            if (string.IsNullOrEmpty(contentDisposition))
            {
                return null;
            }

            var fileNamePart = contentDisposition
                .Split(';')
                .FirstOrDefault(x => x.Trim().StartsWith("filename="));

            if (fileNamePart == null)
            {
                return null;
            }

            var fileName = fileNamePart.Split('=')[1].Trim('"');
            return fileName;
        }
    }
}