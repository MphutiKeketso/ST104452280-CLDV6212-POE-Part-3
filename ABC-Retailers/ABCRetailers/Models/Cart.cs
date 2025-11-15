using System.ComponentModel.DataAnnotations;

namespace ABCRetailers.Models
{
    /// <summary>
    /// Represents a shopping cart for a user session
    /// </summary>
    public class Cart
    {
        public List<CartItem> Items { get; set; } = new();

        public int TotalItems => Items.Sum(i => i.Quantity);

        public decimal Subtotal => Items.Sum(i => i.TotalPrice);

        public decimal Tax => Subtotal * 0.15m; // 15% tax rate

        public decimal ShippingCost
        {
            get
            {
                if (Subtotal == 0) return 0;
                if (Subtotal >= 100) return 0; // Free shipping over $100
                return 10.00m; // Flat rate shipping
            }
        }

        public decimal Total => Subtotal + Tax + ShippingCost;

        public void AddItem(Product product, int quantity)
        {
            var existingItem = Items.FirstOrDefault(i => i.ProductId == product.ProductId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                existingItem.TotalPrice = existingItem.Quantity * existingItem.UnitPrice;
            }
            else
            {
                Items.Add(new CartItem
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    ProductImageUrl = product.ImageUrl,
                    UnitPrice = (decimal)product.Price,
                    Quantity = quantity,
                    TotalPrice = (decimal)product.Price * quantity,
                    StockAvailable = product.StockAvailable
                });
            }
        }

        public void UpdateQuantity(string productId, int quantity)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                if (quantity <= 0)
                {
                    Items.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                    item.TotalPrice = item.Quantity * item.UnitPrice;
                }
            }
        }

        public void RemoveItem(string productId)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                Items.Remove(item);
            }
        }

        public void Clear()
        {
            Items.Clear();
        }
    }

    /// <summary>
    /// Represents a single item in the shopping cart
    /// </summary>
    public class CartItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public int StockAvailable { get; set; }
    }

    /// <summary>
    /// ViewModel for checkout page
    /// </summary>
    public class CheckoutViewModel
    {
        public Cart Cart { get; set; } = new();

        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Shipping address is required")]
        [Display(Name = "Shipping Address")]
        public string ShippingAddress { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Order Notes")]
        public string? OrderNotes { get; set; }
    }
}