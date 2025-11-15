using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Services;
using ABCRetailers.Constants;

namespace ABCRetailers.Controllers
{
    public class UploadController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<UploadController> _logger;

        public UploadController(IAzureStorageService storageService, ILogger<UploadController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View(new FileUploadModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FileUploadModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (model.ProofOfPayment != null && model.ProofOfPayment.Length > 0)
                    {
                        // FIXED: Added file validation
                        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                        var extension = Path.GetExtension(model.ProofOfPayment.FileName).ToLowerInvariant();

                        if (!allowedExtensions.Contains(extension))
                        {
                            ModelState.AddModelError("ProofOfPayment",
                                "Only PDF and image files (JPG, PNG) are allowed.");
                            return View(model);
                        }

                        // Upload to blob storage
                        var fileName = await _storageService.UploadFileAsync(
                            model.ProofOfPayment,
                            StorageConstants.PaymentProofsContainer);

                        // Also upload to file share for contracts
                        await _storageService.UploadToFileShareAsync(
                            model.ProofOfPayment,
                            StorageConstants.ContractsShare,
                            StorageConstants.PaymentsDirectory);

                        TempData["Success"] = $"File uploaded successfully! File name: {fileName}";
                        return View(new FileUploadModel());
                    }
                    else
                    {
                        ModelState.AddModelError("ProofOfPayment", "Please select a file to upload.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading file");
                    ModelState.AddModelError("", $"Error uploading file: {ex.Message}");
                }
            }

            return View(model);
        }
    }
}