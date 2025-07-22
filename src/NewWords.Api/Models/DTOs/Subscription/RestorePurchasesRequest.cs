using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.Subscription
{
    /// <summary>
    /// Request to restore user's purchases from Google Play.
    /// Used when user reinstalls app or logs in on new device.
    /// </summary>
    public class RestorePurchasesRequest
    {
        /// <summary>
        /// List of Google Play purchase tokens to restore.
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "At least one purchase token is required")]
        public IList<string> PurchaseTokens { get; set; } = new List<string>();
    }
}