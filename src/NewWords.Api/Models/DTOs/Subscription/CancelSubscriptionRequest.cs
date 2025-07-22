using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.Subscription
{
    /// <summary>
    /// Request to cancel a user's subscription.
    /// </summary>
    public class CancelSubscriptionRequest
    {
        /// <summary>
        /// Optional reason for cancellation (for analytics and feedback).
        /// </summary>
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string? Reason { get; set; }
    }
}