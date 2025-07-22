using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.Subscription
{
    /// <summary>
    /// Request to process a subscription purchase from mobile app.
    /// </summary>
    public class ProcessPurchaseRequest
    {
        /// <summary>
        /// Google Play purchase token received from the mobile app.
        /// </summary>
        [Required]
        [StringLength(2048)] // Google Play tokens can be quite long
        public string PurchaseToken { get; set; } = string.Empty;

        /// <summary>
        /// Google Play product ID (e.g., "monthly_premium", "yearly_premium").
        /// </summary>
        [Required]
        [StringLength(100)]
        public string ProductId { get; set; } = string.Empty;
    }
}