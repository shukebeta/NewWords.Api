using NewWords.Api.Entities;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Tests.Helpers
{
    /// <summary>
    /// Builder class for creating test data objects with sensible defaults.
    /// </summary>
    public static class TestDataBuilder
    {
        private static readonly DateTimeOffset BaseTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        #region UserSubscription Builders

        public static UserSubscription CreateFreeSubscription(
            int userId = 1, 
            int currentWordCount = 0,
            DateTimeOffset? createdAt = null)
        {
            var created = createdAt ?? BaseTime;
            return new UserSubscription
            {
                Id = 1,
                UserId = userId,
                SubscriptionTier = "Free",
                IsActive = true,
                StartedAt = created.ToUnixTimeSeconds(),
                CreatedAt = created.ToUnixTimeSeconds(),
                CurrentWordCount = currentWordCount,
                Version = 1
            };
        }

        public static UserSubscription CreateMonthlySubscription(
            int userId = 1,
            DateTimeOffset? expiresAt = null,
            string? purchaseToken = "test_monthly_token",
            bool isActive = true)
        {
            var created = BaseTime;
            var expires = expiresAt ?? BaseTime.AddMonths(1);
            
            return new UserSubscription
            {
                Id = 1,
                UserId = userId,
                SubscriptionTier = "Monthly",
                IsActive = isActive,
                StartedAt = created.ToUnixTimeSeconds(),
                ExpiresAt = expires.ToUnixTimeSeconds(),
                PurchaseToken = purchaseToken,
                ProductId = "monthly_premium",
                OrderId = "ORDER_123_MONTHLY",
                LastVerifiedAt = created.ToUnixTimeSeconds(),
                CreatedAt = created.ToUnixTimeSeconds(),
                CurrentWordCount = 0,
                Version = 1
            };
        }

        public static UserSubscription CreateYearlySubscription(
            int userId = 1,
            DateTimeOffset? expiresAt = null,
            string? purchaseToken = "test_yearly_token",
            bool isActive = true)
        {
            var created = BaseTime;
            var expires = expiresAt ?? BaseTime.AddYears(1);
            
            return new UserSubscription
            {
                Id = 1,
                UserId = userId,
                SubscriptionTier = "Yearly",
                IsActive = isActive,
                StartedAt = created.ToUnixTimeSeconds(),
                ExpiresAt = expires.ToUnixTimeSeconds(),
                PurchaseToken = purchaseToken,
                ProductId = "yearly_premium",
                OrderId = "ORDER_123_YEARLY",
                LastVerifiedAt = created.ToUnixTimeSeconds(),
                CreatedAt = created.ToUnixTimeSeconds(),
                CurrentWordCount = 0,
                Version = 1
            };
        }

        public static UserSubscription CreateLifetimeSubscription(
            int userId = 1,
            string? purchaseToken = "test_lifetime_token",
            bool isActive = true)
        {
            var created = BaseTime;
            
            return new UserSubscription
            {
                Id = 1,
                UserId = userId,
                SubscriptionTier = "Lifetime",
                IsActive = isActive,
                StartedAt = created.ToUnixTimeSeconds(),
                ExpiresAt = null, // Lifetime never expires
                PurchaseToken = purchaseToken,
                ProductId = "lifetime_premium",
                OrderId = "ORDER_123_LIFETIME",
                LastVerifiedAt = created.ToUnixTimeSeconds(),
                CreatedAt = created.ToUnixTimeSeconds(),
                CurrentWordCount = 0,
                Version = 1
            };
        }

        public static UserSubscription CreateExpiredSubscription(
            int userId = 1,
            DateTimeOffset? expiredAt = null)
        {
            var expired = expiredAt ?? BaseTime.AddDays(-1);
            
            return new UserSubscription
            {
                Id = 1,
                UserId = userId,
                SubscriptionTier = "Monthly",
                IsActive = false,
                StartedAt = BaseTime.AddMonths(-1).ToUnixTimeSeconds(),
                ExpiresAt = expired.ToUnixTimeSeconds(),
                PurchaseToken = "expired_token",
                ProductId = "monthly_premium",
                LastVerifiedAt = expired.ToUnixTimeSeconds(),
                CreatedAt = BaseTime.AddMonths(-1).ToUnixTimeSeconds(),
                CurrentWordCount = 150,
                Version = 2
            };
        }

        #endregion

        #region Google Play Response Builders

        public static GooglePlaySubscriptionResult CreateValidGooglePlayResult(
            string? orderId = "TEST_ORDER_123",
            DateTimeOffset? expiryTime = null,
            bool autoRenewing = true,
            long? priceAmountMicros = 4990000) // $4.99
        {
            var expiry = expiryTime ?? BaseTime.AddMonths(1);
            
            return new GooglePlaySubscriptionResult
            {
                IsValid = true,
                OrderId = orderId,
                PurchaseState = "Purchased",
                AcknowledgementState = "Acknowledged",
                StartTimeMillis = BaseTime.ToUnixTimeMilliseconds(),
                ExpiryTimeMillis = expiry.ToUnixTimeMilliseconds(),
                AutoRenewing = autoRenewing,
                PriceAmountMicros = priceAmountMicros,
                PriceCurrencyCode = "USD",
                CountryCode = "US",
                PaymentState = "Paid",
                RawResponse = "{\"orderId\":\"TEST_ORDER_123\"}"
            };
        }

        public static GooglePlaySubscriptionResult CreateInvalidGooglePlayResult(
            string? errorMessage = "Invalid purchase token")
        {
            return new GooglePlaySubscriptionResult
            {
                IsValid = false,
                ErrorMessage = errorMessage,
                HttpStatusCode = 400
            };
        }

        public static GooglePlaySubscriptionResult CreateExpiredGooglePlayResult(
            string? orderId = "EXPIRED_ORDER_123")
        {
            return new GooglePlaySubscriptionResult
            {
                IsValid = true,
                OrderId = orderId,
                PurchaseState = "Purchased",
                StartTimeMillis = BaseTime.AddMonths(-2).ToUnixTimeMilliseconds(),
                ExpiryTimeMillis = BaseTime.AddDays(-1).ToUnixTimeMilliseconds(),
                AutoRenewing = false,
                PriceAmountMicros = 4990000,
                PriceCurrencyCode = "USD"
            };
        }

        public static GooglePlayVerificationResult CreateValidPurchaseResult(
            string? orderId = "PURCHASE_ORDER_123")
        {
            return new GooglePlayVerificationResult
            {
                IsValid = true,
                OrderId = orderId,
                PurchaseState = "Purchased",
                AcknowledgementState = "Acknowledged",
                PurchaseTimeMillis = BaseTime.ToUnixTimeMilliseconds(),
                CountryCode = "US",
                RawResponse = "{\"orderId\":\"PURCHASE_ORDER_123\"}"
            };
        }

        #endregion

        #region Subscription History Builders

        public static SubscriptionHistory CreatePurchaseHistory(
            int userId = 1,
            int? subscriptionId = 1,
            string eventType = "Purchase",
            string? previousTier = "Free",
            string? newTier = "Monthly")
        {
            return new SubscriptionHistory
            {
                Id = 1,
                UserId = userId,
                UserSubscriptionId = subscriptionId,
                EventType = eventType,
                PreviousTier = previousTier,
                NewTier = newTier,
                OrderId = "ORDER_123",
                AmountPaid = 499, // $4.99 in cents
                Currency = "USD",
                EventSource = "App",
                CreatedAt = BaseTime.ToUnixTimeSeconds()
            };
        }

        public static SubscriptionHistory CreateCancellationHistory(
            int userId = 1,
            int? subscriptionId = 1,
            string? reason = "User requested")
        {
            return new SubscriptionHistory
            {
                Id = 2,
                UserId = userId,
                UserSubscriptionId = subscriptionId,
                EventType = "Cancellation",
                PreviousTier = "Monthly",
                NewTier = "Free",
                ErrorMessage = reason,
                EventSource = "App",
                CreatedAt = BaseTime.AddDays(15).ToUnixTimeSeconds()
            };
        }

        #endregion

        #region Purchase Verification Builders

        public static PurchaseVerification CreateSuccessfulVerification(
            int userId = 1,
            int? subscriptionId = 1,
            string purchaseToken = "test_token",
            string productId = "monthly_premium")
        {
            return new PurchaseVerification
            {
                Id = 1,
                UserId = userId,
                UserSubscriptionId = subscriptionId,
                PurchaseToken = purchaseToken,
                ProductId = productId,
                OrderId = "ORDER_123",
                Status = "Success",
                PurchaseState = "Purchased",
                ExpiryTimeMillis = BaseTime.AddMonths(1).ToUnixTimeMilliseconds(),
                PriceAmountMicros = 4990000,
                PriceCurrencyCode = "USD",
                VerificationType = "Initial",
                StartedAt = BaseTime.ToUnixTimeSeconds(),
                CompletedAt = BaseTime.ToUnixTimeSeconds(),
                CreatedAt = BaseTime.ToUnixTimeSeconds(),
                VerificationDurationMs = 250
            };
        }

        public static PurchaseVerification CreateFailedVerification(
            int userId = 1,
            string purchaseToken = "invalid_token",
            string errorMessage = "Token not found")
        {
            return new PurchaseVerification
            {
                Id = 2,
                UserId = userId,
                PurchaseToken = purchaseToken,
                ProductId = "monthly_premium",
                Status = "Failed",
                ErrorMessage = errorMessage,
                HttpStatusCode = 404,
                VerificationType = "Initial",
                StartedAt = BaseTime.ToUnixTimeSeconds(),
                CompletedAt = BaseTime.ToUnixTimeSeconds(),
                CreatedAt = BaseTime.ToUnixTimeSeconds(),
                VerificationDurationMs = 1500
            };
        }

        #endregion

        #region User and UserWord Builders

        public static User CreateTestUser(
            int id = 1,
            string email = "test@example.com",
            string nativeLanguage = "en",
            string learningLanguage = "es")
        {
            return new User
            {
                Id = id,
                Email = email,
                NativeLanguage = nativeLanguage,
                CurrentLearningLanguage = learningLanguage,
                Salt = "test_salt",
                PasswordHash = "test_hash",
                CreatedAt = BaseTime.ToUnixTimeSeconds()
            };
        }

        public static UserWord CreateUserWord(
            int userId = 1,
            long wordExplanationId = 1,
            DateTimeOffset? createdAt = null)
        {
            return new UserWord
            {
                Id = 1,
                UserId = userId,
                WordExplanationId = wordExplanationId,
                Status = NewWords.Api.Enums.FamiliarityLevel.New,
                CreatedAt = (createdAt ?? BaseTime).ToUnixTimeSeconds()
            };
        }

        public static List<UserWord> CreateUserWords(int userId, int count)
        {
            var words = new List<UserWord>();
            for (int i = 1; i <= count; i++)
            {
                words.Add(CreateUserWord(userId, i, BaseTime.AddDays(-count + i)));
            }
            return words;
        }

        #endregion

        #region Time Helpers

        public static DateTimeOffset GetPastDate(int daysAgo) => BaseTime.AddDays(-daysAgo);
        public static DateTimeOffset GetFutureDate(int daysFromNow) => BaseTime.AddDays(daysFromNow);
        public static DateTimeOffset GetCurrentTime() => BaseTime;

        public static long GetUnixTimestamp(DateTimeOffset dateTime) => dateTime.ToUnixTimeSeconds();
        public static long GetUnixTimestampMillis(DateTimeOffset dateTime) => dateTime.ToUnixTimeMilliseconds();

        #endregion
    }
}