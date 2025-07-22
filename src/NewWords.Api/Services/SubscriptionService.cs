using System.Text.Json;
using Api.Framework;
using Api.Framework.Models;
using Api.Framework.Result;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewWords.Api.Entities;
using NewWords.Api.Services.interfaces;
using SqlSugar;

namespace NewWords.Api.Services
{
    /// <summary>
    /// Service for managing user subscriptions and premium features.
    /// Implements the mixed strategy: local persistence + smart verification.
    /// </summary>
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISqlSugarClient _db;
        private readonly IGooglePlayBillingService _googlePlayService;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _packageName;

        // Cache for subscription data (in-memory cache)
        private readonly Dictionary<int, (UserSubscription subscription, DateTime cacheTime)> _subscriptionCache = new();
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);

        public SubscriptionService(
            ISqlSugarClient db,
            IGooglePlayBillingService googlePlayService,
            ILogger<SubscriptionService> logger,
            IConfiguration configuration)
        {
            _db = db;
            _googlePlayService = googlePlayService;
            _logger = logger;
            _configuration = configuration;
            _packageName = _configuration["GooglePlay:PackageName"] ?? "com.shukebeta.newwords";
        }

        public async Task<UserSubscription> GetUserSubscriptionAsync(int userId, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            // Check cache first (unless force refresh)
            if (!forceRefresh && _subscriptionCache.TryGetValue(userId, out var cached))
            {
                if (DateTime.UtcNow - cached.cacheTime < _cacheExpiry)
                {
                    _logger.LogDebug("Returning cached subscription for user {UserId}", userId);
                    return cached.subscription;
                }
            }

            // Get from database
            var subscription = await _db.Queryable<UserSubscription>()
                .Where(s => s.UserId == userId && s.DeletedAt == null)
                .OrderBy(s => s.CreatedAt, OrderByType.Desc)
                .FirstAsync();

            // Create default free subscription if none exists
            if (subscription == null)
            {
                subscription = await CreateDefaultSubscriptionAsync(userId, cancellationToken);
            }

            // Check if we need verification
            var needsVerification = forceRefresh || subscription.NeedsVerification;
            if (needsVerification && !string.IsNullOrEmpty(subscription.PurchaseToken))
            {
                _logger.LogInformation("Verifying subscription for user {UserId}", userId);
                await VerifyAndUpdateSubscriptionAsync(subscription, cancellationToken);
            }

            // Update cache
            _subscriptionCache[userId] = (subscription, DateTime.UtcNow);
            
            return subscription;
        }

        public async Task<ApiResult<UserSubscription>> ProcessPurchaseAsync(
            int userId, 
            string purchaseToken, 
            string productId, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Processing purchase for user {UserId}, product {ProductId}", userId, productId);

                // Verify with Google Play
                var verificationResult = await _googlePlayService.VerifySubscriptionAsync(_packageName, productId, purchaseToken, cancellationToken);
                
                if (!verificationResult.IsValid)
                {
                    _logger.LogWarning("Purchase verification failed for user {UserId}: {Error}", userId, verificationResult.ErrorMessage);
                    return new FailedResult<UserSubscription>(null, verificationResult.ErrorMessage ?? "Purchase verification failed");
                }

                // Save verification record
                await SaveVerificationRecordAsync(userId, verificationResult, "Initial", cancellationToken);

                // Get or create subscription
                var subscription = await GetUserSubscriptionAsync(userId, forceRefresh: true, cancellationToken);
                
                // Update subscription with purchase details
                var subscriptionTier = GetSubscriptionTierFromProductId(productId);
                var expiryTime = verificationResult.ExpiryTimeMillis.HasValue 
                    ? verificationResult.ExpiryTimeMillis.Value / 1000 
                    : (long?)null;

                var previousTier = subscription.SubscriptionTier;
                subscription.SubscriptionTier = subscriptionTier;
                subscription.PurchaseToken = purchaseToken;
                subscription.ProductId = productId;
                subscription.OrderId = verificationResult.OrderId;
                subscription.ExpiresAt = expiryTime;
                subscription.IsActive = true;
                subscription.LastVerifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                subscription.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                subscription.Version++;

                await _db.Updateable(subscription).ExecuteCommandAsync();

                // Log subscription history
                await LogSubscriptionHistoryAsync(userId, subscription.Id, "Purchase", previousTier, subscriptionTier, verificationResult, cancellationToken);

                // Clear cache
                _subscriptionCache.Remove(userId);

                _logger.LogInformation("Successfully processed purchase for user {UserId}, tier {Tier}", userId, subscriptionTier);
                return new SuccessfulResult<UserSubscription>(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process purchase for user {UserId}: {Error}", userId, ex.Message);
                return new FailedResult<UserSubscription>(null, $"Failed to process purchase: {ex.Message}");
            }
        }

        public async Task<ApiResult<UserSubscription>> RestorePurchasesAsync(
            int userId, 
            IList<string> purchaseTokens, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Restoring purchases for user {UserId}, {Count} tokens", userId, purchaseTokens.Count);

                UserSubscription? latestSubscription = null;
                var restoredCount = 0;

                foreach (var purchaseToken in purchaseTokens)
                {
                    // Try to find product ID from existing records
                    var existingVerification = await _db.Queryable<PurchaseVerification>()
                        .Where(v => v.PurchaseToken == purchaseToken && v.Status == "Success")
                        .OrderBy(v => v.CreatedAt, OrderByType.Desc)
                        .FirstAsync();

                    var productId = existingVerification?.ProductId;
                    if (string.IsNullOrEmpty(productId))
                    {
                        // Try common product IDs
                        var commonProductIds = new[] { "monthly_premium", "yearly_premium", "lifetime_premium" };
                        foreach (var testProductId in commonProductIds)
                        {
                            var testResult = await _googlePlayService.VerifySubscriptionAsync(_packageName, testProductId, purchaseToken, cancellationToken);
                            if (testResult.IsValid)
                            {
                                productId = testProductId;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(productId))
                    {
                        var result = await ProcessPurchaseAsync(userId, purchaseToken, productId, cancellationToken);
                        if (result.Successful)
                        {
                            latestSubscription = result.Data;
                            restoredCount++;
                        }
                    }
                }

                if (restoredCount > 0)
                {
                    _logger.LogInformation("Successfully restored {Count} purchases for user {UserId}", restoredCount, userId);
                    return new SuccessfulResult<UserSubscription>(latestSubscription ?? await GetUserSubscriptionAsync(userId, cancellationToken: cancellationToken));
                }
                else
                {
                    return new FailedResult<UserSubscription>(null, "No valid purchases found to restore");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore purchases for user {UserId}: {Error}", userId, ex.Message);
                return new FailedResult<UserSubscription>(null, $"Failed to restore purchases: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> CancelSubscriptionAsync(
            int userId, 
            string reason = "User requested", 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var subscription = await GetUserSubscriptionAsync(userId, cancellationToken: cancellationToken);
                
                if (subscription.SubscriptionTier == "Free")
                {
                    return new FailedResult<bool>(false, "User is already on free tier");
                }

                var previousTier = subscription.SubscriptionTier;
                subscription.SubscriptionTier = "Free";
                subscription.IsActive = false;
                subscription.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                subscription.Version++;

                await _db.Updateable(subscription).ExecuteCommandAsync();

                // Log history
                await LogSubscriptionHistoryAsync(userId, subscription.Id, "Cancellation", previousTier, "Free", null, cancellationToken, reason);

                // Clear cache
                _subscriptionCache.Remove(userId);

                _logger.LogInformation("Successfully cancelled subscription for user {UserId}", userId);
                return new SuccessfulResult<bool>(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel subscription for user {UserId}: {Error}", userId, ex.Message);
                return new FailedResult<bool>(false, $"Failed to cancel subscription: {ex.Message}");
            }
        }

        public async Task<bool> CanUserAddWordsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var subscription = await GetUserSubscriptionAsync(userId, cancellationToken: cancellationToken);
            return subscription.CanAddWords;
        }

        public async Task<int> GetRemainingWordsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var subscription = await GetUserSubscriptionAsync(userId, cancellationToken: cancellationToken);
            return subscription.RemainingWords;
        }

        public async Task<bool> IncrementWordCountAsync(int userId, int count = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _db.Updateable<UserSubscription>()
                    .SetColumns(s => s.CurrentWordCount == s.CurrentWordCount + count)
                    .SetColumns(s => s.UpdatedAt == DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    .SetColumns(s => s.Version == s.Version + 1)
                    .Where(s => s.UserId == userId && s.DeletedAt == null)
                    .ExecuteCommandAsync();

                // Clear cache to force refresh
                _subscriptionCache.Remove(userId);

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to increment word count for user {UserId}: {Error}", userId, ex.Message);
                return false;
            }
        }

        public async Task<bool> DecrementWordCountAsync(int userId, int count = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _db.Updateable<UserSubscription>()
                    .SetColumns(s => s.CurrentWordCount == s.CurrentWordCount - count)
                    .SetColumns(s => s.UpdatedAt == DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    .SetColumns(s => s.Version == s.Version + 1)
                    .Where(s => s.UserId == userId && s.DeletedAt == null && s.CurrentWordCount >= count)
                    .ExecuteCommandAsync();

                // Clear cache to force refresh
                _subscriptionCache.Remove(userId);

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrement word count for user {UserId}: {Error}", userId, ex.Message);
                return false;
            }
        }

        public async Task<int> RefreshWordCountAsync(int userId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Count actual words from UserWords table
                var actualCount = await _db.Queryable<UserWord>()
                    .Where(uw => uw.UserId == userId)
                    .CountAsync();

                // Update subscription with actual count
                await _db.Updateable<UserSubscription>()
                    .SetColumns(s => s.CurrentWordCount == actualCount)
                    .SetColumns(s => s.UpdatedAt == DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    .SetColumns(s => s.Version == s.Version + 1)
                    .Where(s => s.UserId == userId && s.DeletedAt == null)
                    .ExecuteCommandAsync();

                // Clear cache
                _subscriptionCache.Remove(userId);

                return actualCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh word count for user {UserId}: {Error}", userId, ex.Message);
                return -1;
            }
        }

        public async Task<PageData<SubscriptionHistory>> GetSubscriptionHistoryAsync(
            int userId, 
            int pageSize = 20, 
            int pageNumber = 1, 
            CancellationToken cancellationToken = default)
        {
            RefAsync<int> totalCount = 0;
            var history = await _db.Queryable<SubscriptionHistory>()
                .Where(h => h.UserId == userId)
                .OrderBy(h => h.CreatedAt, OrderByType.Desc)
                .ToPageListAsync(pageNumber, pageSize, totalCount);

            return new PageData<SubscriptionHistory>
            {
                DataList = history,
                TotalCount = totalCount,
                PageIndex = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<ApiResult<UserSubscription>> ValidateSubscriptionAsync(int userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var subscription = await GetUserSubscriptionAsync(userId, forceRefresh: true, cancellationToken);
                return new SuccessfulResult<UserSubscription>(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate subscription for user {UserId}: {Error}", userId, ex.Message);
                return new FailedResult<UserSubscription>(null, $"Failed to validate subscription: {ex.Message}");
            }
        }

        public async Task<SubscriptionAnalytics> GetSubscriptionAnalyticsAsync(
            DateTimeOffset startDate, 
            DateTimeOffset endDate, 
            CancellationToken cancellationToken = default)
        {
            var startUnix = startDate.ToUnixTimeSeconds();
            var endUnix = endDate.ToUnixTimeSeconds();

            var activeSubscriptions = await _db.Queryable<UserSubscription>()
                .Where(s => s.IsActive && s.DeletedAt == null && 
                           (s.ExpiresAt == null || s.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
                .CountAsync();

            var newSubscriptions = await _db.Queryable<SubscriptionHistory>()
                .Where(h => h.EventType == "Purchase" && h.CreatedAt >= startUnix && h.CreatedAt <= endUnix)
                .CountAsync();

            var cancelledSubscriptions = await _db.Queryable<SubscriptionHistory>()
                .Where(h => h.EventType == "Cancellation" && h.CreatedAt >= startUnix && h.CreatedAt <= endUnix)
                .CountAsync();

            var subscriptionsByTier = await _db.Queryable<UserSubscription>()
                .Where(s => s.IsActive && s.DeletedAt == null)
                .GroupBy(s => s.SubscriptionTier)
                .Select(g => new { Tier = g.SubscriptionTier, Count = SqlFunc.AggregateCount(g.SubscriptionTier) })
                .ToListAsync();

            var freeUsers = await _db.Queryable<UserSubscription>()
                .Where(s => s.SubscriptionTier == "Free" && s.DeletedAt == null)
                .CountAsync();

            return new SubscriptionAnalytics
            {
                TotalActiveSubscriptions = activeSubscriptions,
                NewSubscriptionsInPeriod = newSubscriptions,
                CancelledSubscriptionsInPeriod = cancelledSubscriptions,
                SubscriptionsByTier = subscriptionsByTier.ToDictionary(x => x.Tier, x => x.Count),
                TotalFreeUsers = freeUsers
            };
        }

        // Private helper methods

        private async Task<UserSubscription> CreateDefaultSubscriptionAsync(int userId, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var subscription = new UserSubscription
            {
                UserId = userId,
                SubscriptionTier = "Free",
                IsActive = true,
                StartedAt = now,
                CreatedAt = now,
                CurrentWordCount = await GetActualWordCountAsync(userId)
            };

            var id = await _db.Insertable(subscription).ExecuteReturnIdentityAsync();
            subscription.Id = id;

            await LogSubscriptionHistoryAsync(userId, id, "Initial", null, "Free", null, cancellationToken);

            return subscription;
        }

        private async Task<int> GetActualWordCountAsync(int userId)
        {
            return await _db.Queryable<UserWord>()
                .Where(uw => uw.UserId == userId)
                .CountAsync();
        }

        private async Task VerifyAndUpdateSubscriptionAsync(UserSubscription subscription, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subscription.PurchaseToken) || string.IsNullOrEmpty(subscription.ProductId))
                return;

            var verificationResult = await _googlePlayService.VerifySubscriptionAsync(_packageName, subscription.ProductId, subscription.PurchaseToken, cancellationToken);
            
            // Handle case where verification service is unavailable
            if (verificationResult == null)
            {
                _logger.LogWarning("Google Play verification returned null for user {UserId}", subscription.UserId);
                return;
            }

            await SaveVerificationRecordAsync(subscription.UserId, verificationResult, "Periodic", cancellationToken, subscription.Id);

            if (verificationResult.IsValid)
            {
                var wasActive = subscription.IsActive;
                var previousTier = subscription.SubscriptionTier;

                // Update subscription based on Google Play response
                subscription.IsActive = verificationResult.ExpiryTimeMillis > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                subscription.ExpiresAt = verificationResult.ExpiryTimeMillis / 1000;
                subscription.LastVerifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                subscription.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (!subscription.IsActive && wasActive)
                {
                    subscription.SubscriptionTier = "Free";
                    await LogSubscriptionHistoryAsync(subscription.UserId, subscription.Id, "Expiration", previousTier, "Free", verificationResult, cancellationToken);
                }

                await _db.Updateable(subscription).ExecuteCommandAsync();
            }
        }

        private async Task SaveVerificationRecordAsync(
            int userId, 
            GooglePlaySubscriptionResult result, 
            string verificationType, 
            CancellationToken cancellationToken,
            int? subscriptionId = null)
        {
            var verification = new PurchaseVerification
            {
                UserId = userId,
                UserSubscriptionId = subscriptionId,
                PurchaseToken = "",
                ProductId = result.RawResponse ?? "",
                OrderId = result.OrderId,
                Status = result.IsValid ? "Success" : "Failed",
                PurchaseState = result.PurchaseState,
                PurchaseTimeMillis = result.PurchaseTimeMillis,
                StartTimeMillis = result.StartTimeMillis,
                ExpiryTimeMillis = result.ExpiryTimeMillis,
                AutoRenewing = result.AutoRenewing,
                PriceAmountMicros = result.PriceAmountMicros,
                PriceCurrencyCode = result.PriceCurrencyCode,
                CountryCode = result.CountryCode,
                GooglePlayResponse = result.RawResponse,
                ErrorMessage = result.ErrorMessage,
                HttpStatusCode = result.HttpStatusCode,
                VerificationType = verificationType,
                StartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            await _db.Insertable(verification).ExecuteCommandAsync();
        }

        private async Task LogSubscriptionHistoryAsync(
            int userId,
            int subscriptionId,
            string eventType,
            string? previousTier,
            string? newTier,
            GooglePlaySubscriptionResult? verificationResult,
            CancellationToken cancellationToken,
            string? errorMessage = null)
        {
            var history = new SubscriptionHistory
            {
                UserId = userId,
                UserSubscriptionId = subscriptionId,
                EventType = eventType,
                PreviousTier = previousTier,
                NewTier = newTier,
                PurchaseToken = verificationResult?.RawResponse,
                ProductId = verificationResult?.RawResponse,
                OrderId = verificationResult?.OrderId,
                AmountPaid = verificationResult?.PriceAmountMicros.HasValue == true ? (int)(verificationResult.PriceAmountMicros.Value / 10000) : null,
                Currency = verificationResult?.PriceCurrencyCode,
                NewExpiresAt = verificationResult?.ExpiryTimeMillis / 1000,
                Metadata = verificationResult != null ? JsonSerializer.Serialize(verificationResult) : null,
                ErrorMessage = errorMessage,
                EventSource = "App",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            await _db.Insertable(history).ExecuteCommandAsync();
        }

        private static string GetSubscriptionTierFromProductId(string productId)
        {
            return productId.ToLower() switch
            {
                var id when id.Contains("monthly") => "Monthly",
                var id when id.Contains("yearly") => "Yearly", 
                var id when id.Contains("lifetime") => "Lifetime",
                _ => "Monthly"
            };
        }
    }
}