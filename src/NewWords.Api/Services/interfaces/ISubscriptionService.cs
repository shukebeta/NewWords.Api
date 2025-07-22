using Api.Framework.Models;
using Api.Framework.Result;
using NewWords.Api.Entities;

namespace NewWords.Api.Services.interfaces
{
    /// <summary>
    /// Service for managing user subscriptions and premium features.
    /// Handles the business logic for subscription verification, caching, and limits.
    /// </summary>
    public interface ISubscriptionService
    {
        /// <summary>
        /// Gets the current subscription status for a user.
        /// Uses cached data when available, with smart verification.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="forceRefresh">Force verification with Google Play</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current subscription status</returns>
        Task<UserSubscription> GetUserSubscriptionAsync(int userId, bool forceRefresh = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes a new purchase from the mobile app.
        /// Verifies with Google Play and creates/updates subscription.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="purchaseToken">Google Play purchase token</param>
        /// <param name="productId">Google Play product ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated subscription status</returns>
        Task<ApiResult<UserSubscription>> ProcessPurchaseAsync(
            int userId, 
            string purchaseToken, 
            string productId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores purchases for a user from Google Play.
        /// Used when user reinstalls app or logs in on new device.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="purchaseTokens">List of purchase tokens to restore</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Restored subscription status</returns>
        Task<ApiResult<UserSubscription>> RestorePurchasesAsync(
            int userId, 
            IList<string> purchaseTokens, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a user's subscription.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="reason">Cancellation reason</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if cancellation was successful</returns>
        Task<ApiResult<bool>> CancelSubscriptionAsync(
            int userId, 
            string reason = "User requested", 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a user can add more words based on their subscription.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if user can add words</returns>
        Task<bool> CanUserAddWordsAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the remaining word count for a user.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Remaining words (-1 for unlimited)</returns>
        Task<int> GetRemainingWordsAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Increments the word count for a user.
        /// Called when user adds a new word.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="count">Number of words to add (default 1)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if increment was successful</returns>
        Task<bool> IncrementWordCountAsync(int userId, int count = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrements the word count for a user.
        /// Called when user removes a word.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="count">Number of words to remove (default 1)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if decrement was successful</returns>
        Task<bool> DecrementWordCountAsync(int userId, int count = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes the actual word count from the database.
        /// Used to sync the cached count with reality.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Actual word count</returns>
        Task<int> RefreshWordCountAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets subscription history for a user.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="pageSize">Page size for pagination</param>
        /// <param name="pageNumber">Page number for pagination</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paginated subscription history</returns>
        Task<PageData<SubscriptionHistory>> GetSubscriptionHistoryAsync(
            int userId, 
            int pageSize = 20, 
            int pageNumber = 1, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that a subscription is still valid with Google Play.
        /// Used for periodic verification.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated subscription status</returns>
        Task<ApiResult<UserSubscription>> ValidateSubscriptionAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets subscription analytics for admin purposes.
        /// </summary>
        /// <param name="startDate">Start date for analytics</param>
        /// <param name="endDate">End date for analytics</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Subscription analytics data</returns>
        Task<SubscriptionAnalytics> GetSubscriptionAnalyticsAsync(
            DateTimeOffset startDate, 
            DateTimeOffset endDate, 
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Subscription analytics data for admin dashboard.
    /// </summary>
    public class SubscriptionAnalytics
    {
        public int TotalActiveSubscriptions { get; set; }
        public int NewSubscriptionsInPeriod { get; set; }
        public int CancelledSubscriptionsInPeriod { get; set; }
        public int ExpiredSubscriptionsInPeriod { get; set; }
        public decimal TotalRevenueInPeriod { get; set; }
        public Dictionary<string, int> SubscriptionsByTier { get; set; } = new();
        public Dictionary<string, int> PurchasesByCountry { get; set; } = new();
        public int TotalFreeUsers { get; set; }
        public int UsersAtWordLimit { get; set; }
        public double AverageWordsPerFreeUser { get; set; }
        public double AverageWordsPerPremiumUser { get; set; }
    }
}