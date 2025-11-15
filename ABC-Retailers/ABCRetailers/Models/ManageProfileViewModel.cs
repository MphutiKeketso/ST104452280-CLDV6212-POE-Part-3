using System.ComponentModel.DataAnnotations;

namespace ABCRetailers.Models
{
    public class ManageProfileViewModel
    {
        // Read-only properties
        [Display(Name = "Email (Cannot be changed)")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Member Since")]
        [DataType(DataType.Date)]
        public DateTime DateRegistered { get; set; }

        [Display(Name = "Last Login")]
        [DataType(DataType.DateTime)]
        public DateTime? LastLogin { get; set; }


        // Editable properties
        [Required(ErrorMessage = "First name is required")]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number format")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [StringLength(250)]
        [Display(Name = "Address")]
        public string? Address { get; set; }
    }
}