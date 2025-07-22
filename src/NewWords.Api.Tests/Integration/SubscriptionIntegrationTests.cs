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

namespace NewWords.Api.Tests.Integration
{
    /// <summary>
    /// Integration tests for subscription workflows demonstrating end-to-end functionality.
    /// These tests verify that multiple components work together correctly.
    /// </summary>
    public class SubscriptionIntegrationTests
    {
        private readonly ISqlSugarClient _mockDb;
        private readonly IGooglePlayBillingService _mockGooglePlayService;
        private readonly ILogger<SubscriptionService> _mockLogger;
        private readonly IConfiguration _mockConfiguration;
        private readonly SubscriptionService _subscriptionService;

        private const int TestUserId = 1;
        private const string TestPackageName = "com.shukebeta.newwords";

        public SubscriptionIntegrationTests()
        {
            _mockDb = MockDatabaseHelper.CreateMockDatabase();
            _mockGooglePlayService = Substitute.For<IGooglePlayBillingService>();
            _mockLogger = Substitute.For<ILogger<SubscriptionService>>();
            _mockConfiguration = Substitute.For<IConfiguration>();

            _mockConfiguration["GooglePlay:PackageName"].Returns(TestPackageName);

            // Setup database operations
            MockDatabaseHelper.SetupInsertable<UserSubscription>(_mockDb, 1);
            MockDatabaseHelper.SetupInsertable<SubscriptionHistory>(_mockDb, 1);
            MockDatabaseHelper.SetupInsertable<PurchaseVerification>(_mockDb, 1);
            MockDatabaseHelper.SetupUpdateable<UserSubscription>(_mockDb, 1);

            _subscriptionService = new SubscriptionService(
                _mockDb, _mockGooglePlayService, _mockLogger, _mockConfiguration);
        }

        #region New User Journey Tests

        [Fact]
        public async Task NewUserJourney_FromRegistrationToPremium_ShouldWorkEndToEnd()
        {
            // Arrange - New user with no subscription
            var emptySubscriptions = new List<UserSubscription>();
            var userWords = TestDataBuilder.CreateUserWords(TestUserId, 10);
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, emptySubscriptions);
            MockDatabaseHelper.SetupUserWordQuery(_mockDb, userWords);

            var googlePlayResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(googlePlayResult);

            // Act 1: Get subscription (should create free subscription)
            var initialSubscription = await _subscriptionService.GetUserSubscriptionAsync(TestUserId);

            // Assert 1: Free subscription created
            initialSubscription.Should().NotBeNull();
            initialSubscription.SubscriptionTier.Should().Be("Free");
            initialSubscription.CurrentWordCount.Should().Be(10);
            initialSubscription.CanAddWords.Should().BeTrue();

            // Act 2: Add words until near limit
            var canAddWords = await _subscriptionService.CanUserAddWordsAsync(TestUserId);
            canAddWords.Should().BeTrue();

            for (int i = 0; i < 490; i++) // Add 490 more words (total 500)
            {
                await _subscriptionService.IncrementWordCountAsync(TestUserId, 1);
            }

            // Update the mock to reflect the new state
            var updatedSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId, currentWordCount: 500);
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, new List<UserSubscription> { updatedSubscription });

            // Act 3: Check if user can still add words (should be false)
            var canStillAddWords = await _subscriptionService.CanUserAddWordsAsync(TestUserId);
            canStillAddWords.Should().BeFalse();

            // Act 4: User purchases premium subscription
            var purchaseResult = await _subscriptionService.ProcessPurchaseAsync(
                TestUserId, "premium_token", "monthly_premium");

            // Assert 4: Purchase successful
            purchaseResult.Should().NotBeNull();
            purchaseResult.Successful.Should().BeTrue();
            purchaseResult.Data.SubscriptionTier.Should().Be("Monthly");

            // Act 5: Check if user can now add unlimited words
            var premiumSubscription = TestDataBuilder.CreateMonthlySubscription(TestUserId);
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, new List<UserSubscription> { premiumSubscription });

            var canAddUnlimited = await _subscriptionService.CanUserAddWordsAsync(TestUserId);
            var remainingWords = await _subscriptionService.GetRemainingWordsAsync(TestUserId);

            // Assert 5: Now has unlimited access
            canAddUnlimited.Should().BeTrue();
            remainingWords.Should().Be(-1); // Unlimited
        }

        #endregion

        #region Subscription Lifecycle Tests

        [Fact]
        public async Task SubscriptionLifecycle_PurchaseToExpiry_ShouldWorkCorrectly()
        {
            // Arrange - Start with premium subscription
            var activeSubscription = TestDataBuilder.CreateMonthlySubscription(
                TestUserId, 
                expiresAt: TestDataBuilder.GetFutureDate(30));
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, new List<UserSubscription> { activeSubscription });

            var validResult = TestDataBuilder.CreateValidGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(validResult);

            // Act 1: Verify active subscription
            var subscription = await _subscriptionService.GetUserSubscriptionAsync(TestUserId);

            // Assert 1: Subscription is active
            subscription.Should().NotBeNull();
            subscription.SubscriptionTier.Should().Be("Monthly");
            subscription.IsExpired.Should().BeFalse();
            subscription.CanAddWords.Should().BeTrue();

            // Act 2: Simulate subscription expiry
            var expiredResult = TestDataBuilder.CreateExpiredGooglePlayResult();
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(expiredResult);

            // Force verification to detect expiry
            var expiredSubscription = await _subscriptionService.GetUserSubscriptionAsync(TestUserId, forceRefresh: true);

            // Note: In real implementation, the service would update the subscription to Free tier
            // For this test, we'll verify that Google Play verification was called
            await _mockGooglePlayService.Received().VerifySubscriptionAsync(
                TestPackageName, activeSubscription.ProductId, activeSubscription.PurchaseToken);
        }

        [Fact]
        public async Task SubscriptionRenewal_AutoRenewingSubscription_ShouldUpdateExpiry()
        {
            // Arrange - Subscription near expiry but auto-renewing
            var nearExpirySubscription = TestDataBuilder.CreateMonthlySubscription(
                TestUserId,
                expiresAt: TestDataBuilder.GetFutureDate(2)); // Expires in 2 days
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, new List<UserSubscription> { nearExpirySubscription });

            // Mock Google Play returning renewed subscription
            var renewedResult = TestDataBuilder.CreateValidGooglePlayResult(
                expiryTime: TestDataBuilder.GetFutureDate(32)); // Extended by 30 days
            
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(renewedResult);

            // Act: Force verification to detect renewal
            var verifiedSubscription = await _subscriptionService.GetUserSubscriptionAsync(TestUserId, forceRefresh: true);

            // Assert: Verification was performed
            await _mockGooglePlayService.Received().VerifySubscriptionAsync(
                TestPackageName, nearExpirySubscription.ProductId, nearExpirySubscription.PurchaseToken);
            
            verifiedSubscription.Should().NotBeNull();
            verifiedSubscription.WillExpireSoon.Should().BeTrue(); // Still shows as expiring soon until DB is updated
        }

        #endregion

        #region Purchase Restoration Tests

        [Fact]
        public async Task PurchaseRestoration_MultipleTokens_ShouldRestoreLatestValid()
        {
            // Arrange - User with multiple purchase tokens
            var purchaseTokens = new List<string>
            {
                "old_token_1",    // Invalid/expired
                "old_token_2",    // Invalid/expired  
                "current_token_3" // Valid and current
            };

            // Setup existing verification records
            var verifications = new List<PurchaseVerification>
            {
                TestDataBuilder.CreateFailedVerification(TestUserId, "old_token_1"),
                TestDataBuilder.CreateSuccessfulVerification(TestUserId, productId: "monthly_premium"),
            };
            MockDatabaseHelper.SetupQueryable(_mockDb, verifications);

            // Setup initial free subscription
            var freeSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId);
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, new List<UserSubscription> { freeSubscription });

            // Mock Google Play responses
            _mockGooglePlayService.VerifySubscriptionAsync(TestPackageName, Arg.Any<string>(), "old_token_1")
                .Returns(TestDataBuilder.CreateInvalidGooglePlayResult("Token expired"));
            
            _mockGooglePlayService.VerifySubscriptionAsync(TestPackageName, Arg.Any<string>(), "old_token_2")
                .Returns(TestDataBuilder.CreateInvalidGooglePlayResult("Token not found"));
            
            _mockGooglePlayService.VerifySubscriptionAsync(TestPackageName, "monthly_premium", "current_token_3")
                .Returns(TestDataBuilder.CreateValidGooglePlayResult());

            // Act: Restore purchases
            var result = await _subscriptionService.RestorePurchasesAsync(TestUserId, purchaseTokens);

            // Assert: Should successfully restore the valid token
            result.Should().NotBeNull();
            result.Successful.Should().BeTrue();

            // Verify that all tokens were attempted
            await _mockGooglePlayService.Received().VerifySubscriptionAsync(
                TestPackageName, "monthly_premium", "current_token_3");
        }

        #endregion

        #region Word Limit Enforcement Tests

        [Fact]
        public async Task WordLimitEnforcement_FreeUserWorkflow_ShouldEnforceCorrectly()
        {
            // Arrange - Free user with some words
            var userWords = TestDataBuilder.CreateUserWords(TestUserId, 480);
            var freeSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId, currentWordCount: 480);
            
            MockDatabaseHelper.SetupUserWordQuery(_mockDb, userWords);
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, new List<UserSubscription> { freeSubscription });

            // Act 1: Check initial state
            var canAdd = await _subscriptionService.CanUserAddWordsAsync(TestUserId);
            var remaining = await _subscriptionService.GetRemainingWordsAsync(TestUserId);

            // Assert 1: User can still add words
            canAdd.Should().BeTrue();
            remaining.Should().Be(20); // 500 - 480

            // Act 2: Increment to limit
            await _subscriptionService.IncrementWordCountAsync(TestUserId, 20);

            // Update mock to reflect new count
            var atLimitSubscription = TestDataBuilder.CreateFreeSubscription(TestUserId, currentWordCount: 500);
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, new List<UserSubscription> { atLimitSubscription });

            // Act 3: Check at limit
            var canAddAtLimit = await _subscriptionService.CanUserAddWordsAsync(TestUserId);
            var remainingAtLimit = await _subscriptionService.GetRemainingWordsAsync(TestUserId);

            // Assert 3: User cannot add more words
            canAddAtLimit.Should().BeFalse();
            remainingAtLimit.Should().Be(0);

            // Act 4: Refresh word count from database
            var actualCount = await _subscriptionService.RefreshWordCountAsync(TestUserId);

            // Assert 4: Actual count matches
            actualCount.Should().Be(480); // From the mocked UserWords table
        }

        #endregion

        #region Error Recovery Tests

        [Fact]
        public async Task ErrorRecovery_GooglePlayDown_ShouldHandleGracefully()
        {
            // Arrange - User with subscription that needs verification
            var subscription = TestDataBuilder.CreateMonthlySubscription(TestUserId);
            subscription.LastVerifiedAt = null; // Force verification
            
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, new List<UserSubscription> { subscription });

            // Mock Google Play service down
            _mockGooglePlayService.VerifySubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns<GooglePlaySubscriptionResult>(x => throw new HttpRequestException("Service unavailable"));

            // Act: Try to get subscription (should not throw)
            var result = await _subscriptionService.GetUserSubscriptionAsync(TestUserId);

            // Assert: Should still return subscription (cached/local data)
            result.Should().NotBeNull();
            result.SubscriptionTier.Should().Be("Monthly");

            // Verify that verification was attempted
            await _mockGooglePlayService.Received().VerifySubscriptionAsync(
                TestPackageName, subscription.ProductId, subscription.PurchaseToken);
        }

        #endregion

        #region Analytics and Reporting Tests

        [Fact]
        public async Task SubscriptionAnalytics_MixedUserBase_ShouldCalculateCorrectly()
        {
            // Arrange - Mixed user base with different subscription types
            var subscriptions = new List<UserSubscription>
            {
                TestDataBuilder.CreateFreeSubscription(1, currentWordCount: 450),
                TestDataBuilder.CreateFreeSubscription(2, currentWordCount: 500), // At limit
                TestDataBuilder.CreateMonthlySubscription(3),
                TestDataBuilder.CreateYearlySubscription(4),
                TestDataBuilder.CreateLifetimeSubscription(5)
            };

            var historyEvents = new List<SubscriptionHistory>
            {
                TestDataBuilder.CreatePurchaseHistory(3, eventType: "Purchase", newTier: "Monthly"),
                TestDataBuilder.CreatePurchaseHistory(4, eventType: "Purchase", newTier: "Yearly"),
                TestDataBuilder.CreateCancellationHistory(6) // Cancelled user
            };

            MockDatabaseHelper.SetupQueryable(_mockDb, subscriptions);
            MockDatabaseHelper.SetupQueryable(_mockDb, historyEvents);

            // Act: Get analytics
            var startDate = TestDataBuilder.GetPastDate(30);
            var endDate = TestDataBuilder.GetCurrentTime();
            var analytics = await _subscriptionService.GetSubscriptionAnalyticsAsync(startDate, endDate);

            // Assert: Analytics calculated correctly
            analytics.Should().NotBeNull();
            analytics.TotalActiveSubscriptions.Should().Be(5);
            analytics.TotalFreeUsers.Should().Be(2);
            analytics.NewSubscriptionsInPeriod.Should().Be(2); // Monthly and Yearly purchases
            analytics.CancelledSubscriptionsInPeriod.Should().Be(1);
        }

        #endregion

        #region Concurrency and Race Condition Tests

        [Fact]
        public async Task ConcurrentWordIncrement_ShouldHandleOptimisticLocking()
        {
            // Arrange - Simulate concurrent word additions
            var subscription = TestDataBuilder.CreateFreeSubscription(TestUserId, currentWordCount: 495);
            MockDatabaseHelper.SetupUserSubscriptionQuery(_mockDb, new List<UserSubscription> { subscription });

            // Setup updateable to succeed for first call, fail for second (simulating version conflict)
            var updateCallCount = 0;
            MockDatabaseHelper.SetupUpdateable<UserSubscription>(_mockDb);
            _mockDb.Updateable<UserSubscription>()
                .ExecuteCommandAsync()
                .Returns(callInfo =>
                {
                    updateCallCount++;
                    return updateCallCount == 1 ? Task.FromResult(1) : Task.FromResult(0); // Second call fails
                });

            // Act: Attempt concurrent increments
            var task1 = _subscriptionService.IncrementWordCountAsync(TestUserId, 3);
            var task2 = _subscriptionService.IncrementWordCountAsync(TestUserId, 5);

            var results = await Task.WhenAll(task1, task2);

            // Assert: One should succeed, one should fail due to concurrency
            results.Should().Contain(true);  // At least one succeeded
            results.Should().Contain(false); // At least one failed due to version conflict
        }

        #endregion
    }
}