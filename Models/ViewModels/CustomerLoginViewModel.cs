using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.ViewModels
{
    public class CustomerLoginViewModel
    {
        [Required(ErrorMessage = "Email or phone is required.")]
        public string EmailOrPhone { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
