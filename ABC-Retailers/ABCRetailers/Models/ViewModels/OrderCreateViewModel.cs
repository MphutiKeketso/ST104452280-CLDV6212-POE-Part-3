using System.ComponentModel.DataAnnotations;

namespace ABCRetailers.Models.ViewModels
{
    public class OrderCreateViewModel
    {
        [Required(ErrorMessage = "Please select a customer")]
        [Display(Name = "Customer")]
        public string CustomerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a product")]
        [Display(Name = "Product")]
        public string ProductId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Quantity is required")]
        [Display(Name = "Quantity")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; } = 1;

        [Required(ErrorMessage = "Order date is required")]
        [Display(Name = "Order Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy/MM/dd}", ApplyFormatInEditMode = true)]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public List<Customer> Customers { get; set; } = new();
        public List<Product> Products { get; set; } = new();
        public string Status { get; set; } = "Submitted";
    }
}