using NewWords.Api.Entities;

namespace NewWords.Api.Services.interfaces
{
    /// <summary>
    /// Service for interacting with Google Play Billing API.
    /// Handles purchase verification, subscription status checks, and billing operations.
    /// </summary>
    public interface IGooglePlayBillingService
    {
        /// <summary>
        /// Verifies a purchase token with Google Play.
        /// </summary>
        /// <param name="packageName">The package name of the app</param>
        /// <param name="productId">The product ID of the purchased item</param>
        /// <param name="purchaseToken">The purchase token to verify</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Purchase verification result</returns>
        Task<GooglePlayVerificationResult> VerifyPurchaseAsync(
            string packageName, 
            string productId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a subscription purchase with Google Play.
        /// </summary>
        /// <param name="packageName">The package name of the app</param>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="purchaseToken">The purchase token to verify</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Subscription verification result</returns>
        Task<GooglePlaySubscriptionResult> VerifySubscriptionAsync(
            string packageName, 
            string subscriptionId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Acknowledges a purchase with Google Play.
        /// </summary>
        /// <param name="packageName">The package name of the app</param>
        /// <param name="productId">The product ID of the purchased item</param>
        /// <param name="purchaseToken">The purchase token to acknowledge</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if acknowledgement was successful</returns>
        Task<bool> AcknowledgePurchaseAsync(
            string packageName, 
            string productId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the subscription status for a given purchase token.
        /// </summary>
        /// <param name="packageName">The package name of the app</param>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="purchaseToken">The purchase token</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current subscription status</returns>
        Task<GooglePlaySubscriptionStatus> GetSubscriptionStatusAsync(
            string packageName, 
            string subscriptionId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Refunds a purchase through Google Play.
        /// </summary>
        /// <param name="packageName">The package name of the app</param>
        /// <param name="productId">The product ID of the purchased item</param>
        /// <param name="purchaseToken">The purchase token to refund</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if refund was successful</returns>
        Task<bool> RefundPurchaseAsync(
            string packageName, 
            string productId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a subscription through Google Play.
        /// </summary>
        /// <param name="packageName">The package name of the app</param>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="purchaseToken">The purchase token</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if cancellation was successful</returns>
        Task<bool> CancelSubscriptionAsync(
            string packageName, 
            string subscriptionId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of Google Play purchase verification.
    /// </summary>
    public class GooglePlayVerificationResult
    {
        public bool IsValid { get; set; }
        public string? OrderId { get; set; }
        public string? PurchaseState { get; set; }
        public string? AcknowledgementState { get; set; }
        public long? PurchaseTimeMillis { get; set; }
        public string? DeveloperPayload { get; set; }
        public string? CountryCode { get; set; }
        public string? ErrorMessage { get; set; }
        public int? HttpStatusCode { get; set; }
        public string? RawResponse { get; set; }
    }

    /// <summary>
    /// Result of Google Play subscription verification.
    /// </summary>
    public class GooglePlaySubscriptionResult : GooglePlayVerificationResult
    {
        public long? StartTimeMillis { get; set; }
        public long? ExpiryTimeMillis { get; set; }
        public bool? AutoRenewing { get; set; }
        public long? PriceAmountMicros { get; set; }
        public string? PriceCurrencyCode { get; set; }
        public string? PaymentState { get; set; }
        public string? CancelReason { get; set; }
        public string? UserCancellationTimeMillis { get; set; }
    }

    /// <summary>
    /// Google Play subscription status information.
    /// </summary>
    public class GooglePlaySubscriptionStatus
    {
        public bool IsActive { get; set; }
        public bool IsExpired { get; set; }
        public bool IsCancelled { get; set; }
        public bool WillAutoRenew { get; set; }
        public DateTimeOffset? ExpiryTime { get; set; }
        public DateTimeOffset? CancellationTime { get; set; }
        public string? CancelReason { get; set; }
        public string? PaymentState { get; set; }
    }
}