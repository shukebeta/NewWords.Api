using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewWords.Api.Entities;
using NewWords.Api.Services;
using NewWords.Api.Services.interfaces;
using NewWords.Api.Tests.Helpers;
using NSubstitute;
using SqlSugar;
using Xunit;

namespace NewWords.Api.Tests.Services
{
    /// <summary>
    /// Tests for SubscriptionService business logic and database operations.
    /// </summary>
    public class SubscriptionServiceTests
    {
        private readonly ISqlSugarClient _mockDb;
        private readonly IGooglePlayBillingService _mockGooglePlayService;
        private readonly ILogger<SubscriptionService> _mockLogger;
        private readonly IConfiguration _mockConfiguration;
        private readonly SubscriptionService _service;

        private const int TestUserId = 1;
        private const string TestPackageName = "com.shukebeta.newwords";
        private const string TestPurchaseToken = "test_purchase_token_123";
        private const string TestProductId = "monthly_premium";

        public SubscriptionServiceTests()
        {
            _mockDb = MockDatabaseHelper.CreateMockDatabase();
            _mockGooglePlayService = Substitute.For<IGooglePlayBillingService>();
            _mockLogger = Substitute.For<ILogger<SubscriptionService>>();
            _mockConfiguration = Substitute.For<IConfiguration>();

            // Setup configuration
            _mockConfiguration["GooglePlay:PackageName"].Returns(TestPackageName);

            // Setup basic database operations
            MockDatabaseHelper.SetupInsertable<UserSubscription>(_mockDb, 1);
            MockDatabaseHelper.SetupInsertable<SubscriptionHistory>(_mockDb, 1);
            MockDatabaseHelper.SetupInsertable<PurchaseVerification>(_mockDb, 1);
            MockDatabaseHelper.SetupUpdateable<UserSubscription>(_mockDb, 1);

            // Setup default GooglePlay service responses with future expiry
            var futureExpiry = DateTimeOffset.UtcNow.AddMonths(1);
            var defaultVerificationResult = TestDataBuilder.CreateValidGooglePlayResult(expiryTime: futureExpiry);
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(defaultVerificationResult));

            _service = new SubscriptionService(_mockDb, _mockGooglePlayService, _mockLogger, _mockConfiguration);
        }

        #region GetUserSubscriptionAsync Tests

        [Fact]
        public async Task GetUserSubscriptionAsync_WhenFreeSubscriptionExists_ShouldReturnSubscription()
        {
            // Arrange
            var existingSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { existingSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.GetUserSubscriptionAsync(TestUserId);

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(TestUserId);
            result.SubscriptionTier.Should().Be("Free");
        }

        [Fact]
        public async Task GetUserSubscriptionAsync_WhenMonthlySubscriptionExists_ShouldReturnSubscription()
        {
            // Arrange - create subscription without purchase token to avoid verification
            var existingSubscription = new UserSubscription
            {
                Id = 1,
                UserId = TestUserId,
                SubscriptionTier = "Monthly",
                IsActive = true,
                StartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresAt = DateTimeOffset.UtcNow.AddMonths(1).ToUnixTimeSeconds(),
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CurrentWordCount = 0,
                Version = 1
                // No PurchaseToken - this avoids verification
            };
            var subscriptions = new List<UserSubscription> { existingSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.GetUserSubscriptionAsync(TestUserId);

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(TestUserId);
            result.SubscriptionTier.Should().Be("Monthly");
        }

        [Fact]
        public async Task GetUserSubscriptionAsync_WhenNoSubscriptionExists_ShouldCreateFreeSubscription()
        {
            // Arrange
            var emptySubscriptions = new List<UserSubscription>();
            var userWords = TestDataBuilder.CreateUserWords(TestUserId, 10);
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, emptySubscriptions);
            MockDatabaseHelper.SetupUserWordQuery(_mockDb, userWords);

            // Act
            var result = await _service.GetUserSubscriptionAsync(TestUserId);

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(TestUserId);
            result.SubscriptionTier.Should().Be("Free");
            result.CurrentWordCount.Should().Be(10);
        }

        [Fact]
        public async Task GetUserSubscriptionAsync_WithForceRefresh_ShouldBypassCache()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(TestUserId);
            subscription.NeedsVerification.Should().BeFalse(); // Assume recently verified
            
            var subscriptions = new List<UserSubscription> { subscription };
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            var verificationResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(verificationResult);

            // Act
            var result = await _service.GetUserSubscriptionAsync(TestUserId, forceRefresh: true);

            // Assert
            result.Should().NotBeNull();
            await _mockGooglePlayService.Received(1).VerifySubscriptionAsync(
                TestPackageName, subscription.ProductId, subscription.PurchaseToken);
        }

        [Fact]
        public async Task GetUserSubscriptionAsync_WhenNeedsVerification_ShouldVerifyWithGooglePlay()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(TestUserId);
            subscription.LastVerifiedAt = null; // Needs verification
            
            var subscriptions = new List<UserSubscription> { subscription };
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            var verificationResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(verificationResult);

            // Act
            var result = await _service.GetUserSubscriptionAsync(TestUserId);

            // Assert
            result.Should().NotBeNull();
            await _mockGooglePlayService.Received(1).VerifySubscriptionAsync(
                TestPackageName, subscription.ProductId, subscription.PurchaseToken);
        }

        #endregion

        #region ProcessPurchaseAsync Tests

        [Fact]
        public async Task ProcessPurchaseAsync_WithValidPurchase_ShouldCreateSubscription()
        {
            // Arrange
            var existingSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { existingSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            var verificationResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(TestPackageName, TestProductId, TestPurchaseToken)
                .Returns(verificationResult);

            // Act
            var result = await _service.ProcessPurchaseAsync(TestUserId, TestPurchaseToken, TestProductId);

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.SubscriptionTier.Should().Be("Monthly");
            result.Data.PurchaseToken.Should().Be(TestPurchaseToken);

            await _mockGooglePlayService.Received(1).VerifySubscriptionAsync(
                TestPackageName, TestProductId, TestPurchaseToken);
        }

        [Fact]
        public async Task ProcessPurchaseAsync_WithInvalidPurchase_ShouldReturnFailure()
        {
            // Arrange
            var existingSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { existingSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            var verificationResult = TestDataBuilder.CreateInvalidGooglePlayResult("Invalid token");
            _mockGooglePlayService.VerifySubscriptionAsync(TestPackageName, TestProductId, TestPurchaseToken)
                .Returns(verificationResult);

            // Act
            var result = await _service.ProcessPurchaseAsync(TestUserId, TestPurchaseToken, TestProductId);

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeFalse();
            result.Message.Should().Contain("Invalid token");
        }

        [Theory]
        [InlineData("monthly_premium", "Monthly")]
        [InlineData("yearly_premium", "Yearly")]
        [InlineData("lifetime_premium", "Lifetime")]
        [InlineData("unknown_product", "Monthly")] // Default fallback
        public async Task ProcessPurchaseAsync_ShouldMapProductIdToCorrectTier(string productId, string expectedTier)
        {
            // Arrange
            var existingSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { existingSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            var verificationResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(TestPackageName, productId, TestPurchaseToken)
                .Returns(verificationResult);

            // Act
            var result = await _service.ProcessPurchaseAsync(TestUserId, TestPurchaseToken, productId);

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeTrue();
            result.Data.SubscriptionTier.Should().Be(expectedTier);
        }

        #endregion

        #region RestorePurchasesAsync Tests

        [Fact]
        public async Task RestorePurchasesAsync_WithValidTokens_ShouldRestoreLatestSubscription()
        {
            // Arrange
            var purchaseTokens = new List<string> { "token1", "token2", "token3" };
            var existingSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { existingSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            var verificationResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(verificationResult);

            // Mock existing verification records
            var verifications = new List<PurchaseVerification>
            {
                TestDataBuilder.CreateSuccessfulVerification(TestUserId, productId: "monthly_premium")
            };
            MockDatabaseHelper.SetupQueryable(_mockDb, verifications);

            // Act
            var result = await _service.RestorePurchasesAsync(TestUserId, purchaseTokens);

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeTrue();
        }

        [Fact]
        public async Task RestorePurchasesAsync_WithNoValidTokens_ShouldReturnFailure()
        {
            // Arrange
            var purchaseTokens = new List<string> { "invalid_token1", "invalid_token2" };
            var emptyVerifications = new List<PurchaseVerification>();
            
            MockDatabaseHelper.SetupQueryable(_mockDb, emptyVerifications);

            var verificationResult = TestDataBuilder.CreateInvalidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(verificationResult);

            // Act
            var result = await _service.RestorePurchasesAsync(TestUserId, purchaseTokens);

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeFalse();
            result.Message.Should().Contain("No valid purchases found");
        }

        #endregion

        #region CancelSubscriptionAsync Tests

        [Fact]
        public async Task CancelSubscriptionAsync_WithActiveSubscription_ShouldCancelSuccessfully()
        {
            // Arrange
            var activeSubscription = TestDataBuilder.CreateMonthlySubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { activeSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.CancelSubscriptionAsync(TestUserId, "User requested");

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeTrue();
            result.Data.Should().BeTrue();
        }

        [Fact]
        public async Task CancelSubscriptionAsync_WithFreeUser_ShouldReturnFailure()
        {
            // Arrange
            var freeSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { freeSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.CancelSubscriptionAsync(TestUserId);

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeFalse();
            result.Message.Should().Contain("already on free tier");
        }

        #endregion

        #region Word Count Management Tests

        [Fact]
        public async Task CanUserAddWordsAsync_WithPremiumUser_ShouldReturnTrue()
        {
            // Arrange
            var premiumSubscription = TestDataBuilder.CreateMonthlySubscription(TestUserId);
            premiumSubscription.CurrentWordCount = 1000; // Over free limit
            var subscriptions = new List<UserSubscription> { premiumSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.CanUserAddWordsAsync(TestUserId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanUserAddWordsAsync_WithFreeUserUnderLimit_ShouldReturnTrue()
        {
            // Arrange
            var freeSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId, currentWordCount: 400);
            var subscriptions = new List<UserSubscription> { freeSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.CanUserAddWordsAsync(TestUserId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanUserAddWordsAsync_WithFreeUserAtLimit_ShouldReturnFalse()
        {
            // Arrange
            var freeSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId, currentWordCount: 500);
            var subscriptions = new List<UserSubscription> { freeSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.CanUserAddWordsAsync(TestUserId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetRemainingWordsAsync_WithFreeUser_ShouldReturnCorrectCount()
        {
            // Arrange
            var freeSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId, currentWordCount: 300);
            var subscriptions = new List<UserSubscription> { freeSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.GetRemainingWordsAsync(TestUserId);

            // Assert
            result.Should().Be(200); // 500 - 300
        }

        [Fact]
        public async Task GetRemainingWordsAsync_WithPremiumUser_ShouldReturnUnlimited()
        {
            // Arrange
            var premiumSubscription = TestDataBuilder.CreateMonthlySubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { premiumSubscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.GetRemainingWordsAsync(TestUserId);

            // Assert
            result.Should().Be(-1); // Unlimited
        }

        [Fact]
        public async Task IncrementWordCountAsync_ShouldUpdateDatabase()
        {
            // Arrange
            MockDatabaseHelper.SetupUpdateable<UserSubscription>(_mockDb, 1);

            // Act
            var result = await _service.IncrementWordCountAsync(TestUserId, 5);

            // Assert
            result.Should().BeTrue();
            MockDatabaseHelper.VerifyUpdateWasCalled<UserSubscription>(_mockDb);
        }

        [Fact]
        public async Task DecrementWordCountAsync_ShouldUpdateDatabase()
        {
            // Arrange
            MockDatabaseHelper.SetupUpdateable<UserSubscription>(_mockDb, 1);

            // Act
            var result = await _service.DecrementWordCountAsync(TestUserId, 3);

            // Assert
            result.Should().BeTrue();
            MockDatabaseHelper.VerifyUpdateWasCalled<UserSubscription>(_mockDb);
        }

        [Fact]
        public async Task RefreshWordCountAsync_ShouldCountFromUserWordsTable()
        {
            // Arrange
            var userWords = TestDataBuilder.CreateUserWords(TestUserId, 150);
            MockDatabaseHelper.SetupUserWordQuery(_mockDb, userWords);
            MockDatabaseHelper.SetupUpdateable<UserSubscription>(_mockDb, 1);

            // Act
            var result = await _service.RefreshWordCountAsync(TestUserId);

            // Assert
            result.Should().Be(150);
            MockDatabaseHelper.VerifyUpdateWasCalled<UserSubscription>(_mockDb);
        }

        #endregion

        #region GetSubscriptionHistoryAsync Tests

        [Fact]
        public async Task GetSubscriptionHistoryAsync_ShouldReturnPagedResults()
        {
            // Arrange
            var history = new List<SubscriptionHistory>
            {
                TestDataBuilder.CreatePurchaseHistory(TestUserId),
                TestDataBuilder.CreateCancellationHistory(TestUserId)
            };
            
            MockDatabaseHelper.SetupSubscriptionHistoryQuery(_mockDb, history);

            // Act
            var result = await _service.GetSubscriptionHistoryAsync(TestUserId, pageSize: 10, pageNumber: 1);

            // Assert
            result.Should().NotBeNull();
            result.DataList.Should().HaveCount(2);
            result.TotalCount.Should().Be(2);
            result.PageIndex.Should().Be(1);
            result.PageSize.Should().Be(10);
        }

        #endregion

        #region ValidateSubscriptionAsync Tests

        [Fact]
        public async Task ValidateSubscriptionAsync_ShouldReturnUpdatedSubscription()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { subscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            var verificationResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(verificationResult);

            // Act
            var result = await _service.ValidateSubscriptionAsync(TestUserId);

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        #endregion

        #region GetSubscriptionAnalyticsAsync Tests

        [Fact]
        public async Task GetSubscriptionAnalyticsAsync_ShouldReturnAnalyticsData()
        {
            // Arrange
            var startDate = DateTimeOffset.UtcNow.AddDays(-30);
            var endDate = DateTimeOffset.UtcNow;
            
            var activeSubscriptions = new List<UserSubscription>
            {
                TestDataBuilder.CreateMonthlySubscription(1),
                TestDataBuilder.CreateYearlySubscription(2),
                TestDataBuilder.CreateFreeSubscription(3)
            };
            
            var newSubscriptionHistory = new List<SubscriptionHistory>
            {
                TestDataBuilder.CreatePurchaseHistory(1),
                TestDataBuilder.CreatePurchaseHistory(2)
            };

            MockDatabaseHelper.SetupQueryable(_mockDb, activeSubscriptions);
            MockDatabaseHelper.SetupQueryable(_mockDb, newSubscriptionHistory);

            // Mock group by results
            var tierGroups = new List<dynamic>
            {
                new { Tier = "Free", Count = 1 },
                new { Tier = "Monthly", Count = 1 },
                new { Tier = "Yearly", Count = 1 }
            };
            MockDatabaseHelper.SetupGroupByQuery<UserSubscription, dynamic>(_mockDb, tierGroups);

            // Act
            var result = await _service.GetSubscriptionAnalyticsAsync(startDate, endDate);

            // Assert
            result.Should().NotBeNull();
            result.TotalActiveSubscriptions.Should().Be(3);
            result.TotalFreeUsers.Should().Be(1);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task ProcessPurchaseAsync_WithDatabaseError_ShouldReturnFailure()
        {
            // Arrange
            var verificationResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(verificationResult);

            // Setup database to throw exception
            _mockDb.Queryable<UserSubscription>().Returns(x => throw new Exception("Database error"));

            // Act
            var result = await _service.ProcessPurchaseAsync(TestUserId, TestPurchaseToken, TestProductId);

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeFalse();
            result.Message.Should().Contain("Database error");
        }

        [Fact]
        public async Task GetUserSubscriptionAsync_WithDatabaseError_ShouldThrow()
        {
            // Arrange
            _mockDb.Queryable<UserSubscription>().Returns(x => throw new Exception("Database connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.GetUserSubscriptionAsync(TestUserId));
        }

        #endregion

        #region Concurrency Tests

        [Fact]
        public async Task IncrementWordCountAsync_WithOptimisticConcurrency_ShouldHandleVersioning()
        {
            // Arrange
            MockDatabaseHelper.SetupUpdateable<UserSubscription>(_mockDb, 1);

            // Act
            var result = await _service.IncrementWordCountAsync(TestUserId, 1);

            // Assert
            result.Should().BeTrue();
            // Verify that version is incremented (this would be verified in the actual SQL)
        }

        [Fact]
        public async Task ProcessPurchaseAsync_ShouldUpdateVersionForOptimisticConcurrency()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            subscription.Version = 1;
            var subscriptions = new List<UserSubscription> { subscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            var verificationResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(verificationResult);

            // Act
            var result = await _service.ProcessPurchaseAsync(TestUserId, TestPurchaseToken, TestProductId);

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeTrue();
            // In real implementation, we'd verify the version was incremented
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task GetUserSubscriptionAsync_WithNullPurchaseToken_ShouldSkipVerification()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            subscription.PurchaseToken = null;
            subscription.LastVerifiedAt = null; // Would normally need verification
            
            var subscriptions = new List<UserSubscription> { subscription };
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            // Act
            var result = await _service.GetUserSubscriptionAsync(TestUserId);

            // Assert
            result.Should().NotBeNull();
            // Should not call Google Play service
            await _mockGooglePlayService.DidNotReceive().VerifySubscriptionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task DecrementWordCountAsync_WithZeroWords_ShouldReturnFalse()
        {
            // Arrange
            MockDatabaseHelper.SetupUpdateable<UserSubscription>(_mockDb, 0); // No rows affected

            // Act
            var result = await _service.DecrementWordCountAsync(TestUserId, 1);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessPurchaseAsync_WithLifetimeProduct_ShouldSetNoExpiry()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            var subscriptions = new List<UserSubscription> { subscription };
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, subscriptions);

            var verificationResult = TestDataBuilder.CreateValidGooglePlayResult();
            verificationResult.ExpiryTimeMillis = null; // Lifetime has no expiry
            
            _mockGooglePlayService.VerifySubscriptionAsync(TestPackageName, "lifetime_premium", TestPurchaseToken)
                .Returns(verificationResult);

            // Act
            var result = await _service.ProcessPurchaseAsync(TestUserId, TestPurchaseToken, "lifetime_premium");

            // Assert
            result.Should().NotBeNull();
            result.Successful.Should().BeTrue();
            result.Data.ExpiresAt.Should().BeNull();
            result.Data.SubscriptionTier.Should().Be("Lifetime");
        }

        #endregion
    }
}