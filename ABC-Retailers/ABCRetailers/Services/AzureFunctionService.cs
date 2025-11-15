namespace ABCRetailers.Services
{
    public interface IAzureFunctionService
    {
        Task<string?> UploadProductImageAsync(IFormFile file);
        Task<string?> UploadPaymentProofAsync(IFormFile file);
    }

    public class AzureFunctionService : IAzureFunctionService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureFunctionService> _logger;
        private readonly string _baseUrl;

        public AzureFunctionService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AzureFunctionService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("AzureFunctions");
            _baseUrl = configuration["AzureFunctions:BaseUrl"] ?? "http://localhost:7071/api";
            _logger = logger;
        }

        public async Task<string?> UploadProductImageAsync(IFormFile file)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new StreamContent(file.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                content.Add(fileContent, "file", file.FileName);

                var response = await _httpClient.PostAsync($"{_baseUrl}/images/product", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
                    return result?.ImageUrl;
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to upload image: {error}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling UploadProductImage function");
                return null;
            }
        }

        public async Task<string?> UploadPaymentProofAsync(IFormFile file)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new StreamContent(file.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                content.Add(fileContent, "file", file.FileName);

                var response = await _httpClient.PostAsync($"{_baseUrl}/files/payment-proof", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
                    return result?.FileName;
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to upload payment proof: {error}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling UploadPaymentProof function");
                return null;
            }
        }

        private class UploadImageResponse
        {
            public bool Success { get; set; }
            public string? ImageUrl { get; set; }
            public string? FileName { get; set; }
        }

        private class UploadFileResponse
        {
            public bool Success { get; set; }
            public string? FileName { get; set; }
            public string? Message { get; set; }
        }
    }
}