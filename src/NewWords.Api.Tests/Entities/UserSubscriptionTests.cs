using FluentAssertions;
using NewWords.Api.Tests.Helpers;
using Xunit;

namespace NewWords.Api.Tests.Entities
{
    /// <summary>
    /// Tests for UserSubscription entity computed properties and business logic.
    /// </summary>
    public class UserSubscriptionTests
    {
        #region IsExpired Property Tests

        [Fact]
        public void IsExpired_WhenExpiresAtIsInPast_ShouldReturnTrue()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(
                expiresAt: TestDataBuilder.GetPastDate(1));

            // Act
            var result = subscription.IsExpired;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsExpired_WhenExpiresAtIsInFuture_ShouldReturnFalse()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(
                expiresAt: DateTimeOffset.UtcNow.AddDays(30));

            // Act
            var result = subscription.IsExpired;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsExpired_WhenExpiresAtIsNull_ShouldReturnFalse()
        {
            // Arrange - Lifetime subscription
            var subscription = TestDataBuilder.CreateLifetimeSubscription();

            // Act
            var result = subscription.IsExpired;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsExpired_WhenExpiresAtIsExactlyNow_ShouldReturnTrue()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(
                expiresAt: DateTimeOffset.UtcNow);

            // Act
            var result = subscription.IsExpired;

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region WillExpireSoon Property Tests

        [Fact]
        public void WillExpireSoon_WhenExpiryIsWithin3Days_ShouldReturnTrue()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(
                expiresAt: DateTimeOffset.UtcNow.AddDays(2));

            // Act
            var result = subscription.WillExpireSoon;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void WillExpireSoon_WhenExpiryIsBeyond3Days_ShouldReturnFalse()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(
                expiresAt: DateTimeOffset.UtcNow.AddDays(4));

            // Act
            var result = subscription.WillExpireSoon;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void WillExpireSoon_WhenExpiresAtIsNull_ShouldReturnFalse()
        {
            // Arrange - Lifetime subscription
            var subscription = TestDataBuilder.CreateLifetimeSubscription();

            // Act
            var result = subscription.WillExpireSoon;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void WillExpireSoon_WhenExpiryIsExactly3Days_ShouldReturnTrue()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(
                expiresAt: DateTimeOffset.UtcNow.AddDays(3));

            // Act
            var result = subscription.WillExpireSoon;

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region NeedsVerification Property Tests

        [Fact]
        public void NeedsVerification_WhenLastVerifiedAtIsNull_ShouldReturnTrue()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription();
            subscription.LastVerifiedAt = null;

            // Act
            var result = subscription.NeedsVerification;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void NeedsVerification_WhenLastVerifiedAtIsOlderThan24Hours_ShouldReturnTrue()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription();
            subscription.LastVerifiedAt = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeSeconds();

            // Act
            var result = subscription.NeedsVerification;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void NeedsVerification_WhenLastVerifiedAtIsWithin24Hours_ShouldReturnFalse()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription();
            subscription.LastVerifiedAt = DateTimeOffset.UtcNow.AddHours(-12).ToUnixTimeSeconds();

            // Act
            var result = subscription.NeedsVerification;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void NeedsVerification_WhenLastVerifiedAtIsExactly24HoursAgo_ShouldReturnTrue()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription();
            subscription.LastVerifiedAt = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();

            // Act
            var result = subscription.NeedsVerification;

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region WordLimit Property Tests

        [Fact]
        public void WordLimit_ForFreeTier_ShouldReturn500()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription();

            // Act
            var result = subscription.WordLimit;

            // Assert
            result.Should().Be(500);
        }

        [Theory]
        [InlineData("Monthly")]
        [InlineData("Yearly")]
        [InlineData("Lifetime")]
        public void WordLimit_ForPremiumTiers_ShouldReturnNegativeOne(string tier)
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription();
            subscription.SubscriptionTier = tier;

            // Act
            var result = subscription.WordLimit;

            // Assert
            result.Should().Be(-1);
        }

        [Fact]
        public void WordLimit_ForUnknownTier_ShouldDefaultTo500()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription();
            subscription.SubscriptionTier = "UnknownTier";

            // Act
            var result = subscription.WordLimit;

            // Assert
            result.Should().Be(500);
        }

        #endregion

        #region CanAddWords Property Tests

        [Theory]
        [InlineData("Monthly")]
        [InlineData("Yearly")]
        [InlineData("Lifetime")]
        public void CanAddWords_ForPremiumTiers_ShouldReturnTrueRegardlessOfWordCount(string tier)
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: 1000);
            subscription.SubscriptionTier = tier;

            // Act
            var result = subscription.CanAddWords;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void CanAddWords_ForFreeTierUnderLimit_ShouldReturnTrue()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: 400);

            // Act
            var result = subscription.CanAddWords;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void CanAddWords_ForFreeTierAtLimit_ShouldReturnFalse()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: 500);

            // Act
            var result = subscription.CanAddWords;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void CanAddWords_ForFreeTierOverLimit_ShouldReturnFalse()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: 600);

            // Act
            var result = subscription.CanAddWords;

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region RemainingWords Property Tests

        [Fact]
        public void RemainingWords_ForFreeTierUnderLimit_ShouldReturnCorrectCount()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: 300);

            // Act
            var result = subscription.RemainingWords;

            // Assert
            result.Should().Be(200);
        }

        [Fact]
        public void RemainingWords_ForFreeTierAtLimit_ShouldReturnZero()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: 500);

            // Act
            var result = subscription.RemainingWords;

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public void RemainingWords_ForFreeTierOverLimit_ShouldReturnZero()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: 600);

            // Act
            var result = subscription.RemainingWords;

            // Assert
            result.Should().Be(0);
        }

        [Theory]
        [InlineData("Monthly")]
        [InlineData("Yearly")]
        [InlineData("Lifetime")]
        public void RemainingWords_ForPremiumTiers_ShouldReturnNegativeOne(string tier)
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: 1000);
            subscription.SubscriptionTier = tier;

            // Act
            var result = subscription.RemainingWords;

            // Assert
            result.Should().Be(-1);
        }

        #endregion

        #region Edge Cases and Boundary Tests

        [Fact]
        public void Subscription_WithZeroWordCount_ShouldWorkCorrectly()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: 0);

            // Act & Assert
            subscription.CanAddWords.Should().BeTrue();
            subscription.RemainingWords.Should().Be(500);
            subscription.WordLimit.Should().Be(500);
        }

        [Fact]
        public void Subscription_WithNegativeWordCount_ShouldHandleGracefully()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateFreeSubscription(currentWordCount: -10);

            // Act & Assert
            subscription.CanAddWords.Should().BeTrue();
            subscription.RemainingWords.Should().Be(510); // Math.Max(0, 500 - (-10))
        }

        [Fact]
        public void Subscription_CreatedAtCurrentTime_ShouldNotNeedVerificationImmediately()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription();
            subscription.LastVerifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Act
            var result = subscription.NeedsVerification;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Subscription_AllPropertiesWork_WhenExpiresAtIsVeryFarInFuture()
        {
            // Arrange
            var subscription = TestDataBuilder.CreateMonthlySubscription(
                expiresAt: DateTimeOffset.UtcNow.AddYears(10));

            // Act & Assert
            subscription.IsExpired.Should().BeFalse();
            subscription.WillExpireSoon.Should().BeFalse();
        }

        #endregion
    }
}