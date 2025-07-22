using FluentAssertions;
using NewWords.Api.Tests.Helpers;
using Xunit;

namespace NewWords.Api.Tests.Entities
{
    /// <summary>
    /// Tests for PurchaseVerification entity computed properties and business logic.
    /// </summary>
    public class PurchaseVerificationTests
    {
        #region IsSuccessful Property Tests

        [Fact]
        public void IsSuccessful_WhenStatusIsSuccessAndNoError_ShouldReturnTrue()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.Status = "Success";
            verification.ErrorMessage = null;

            // Act
            var result = verification.IsSuccessful;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsSuccessful_WhenStatusIsSuccessButHasError_ShouldReturnFalse()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.Status = "Success";
            verification.ErrorMessage = "Some warning";

            // Act
            var result = verification.IsSuccessful;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsSuccessful_WhenStatusIsNotSuccess_ShouldReturnFalse()
        {
            // Arrange
            var verification = TestDataBuilder.CreateFailedVerification();
            verification.Status = "Failed";
            verification.ErrorMessage = null;

            // Act
            var result = verification.IsSuccessful;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsSuccessful_WhenStatusIsSuccessAndEmptyError_ShouldReturnTrue()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.Status = "Success";
            verification.ErrorMessage = "";

            // Act
            var result = verification.IsSuccessful;

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region IsPending Property Tests

        [Fact]
        public void IsPending_WhenStatusIsPendingAndNotCompleted_ShouldReturnTrue()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.Status = "Pending";
            verification.CompletedAt = null;

            // Act
            var result = verification.IsPending;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsPending_WhenStatusIsPendingButCompleted_ShouldReturnFalse()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.Status = "Pending";
            verification.CompletedAt = TestDataBuilder.GetUnixTimestamp(TestDataBuilder.GetCurrentTime());

            // Act
            var result = verification.IsPending;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsPending_WhenStatusIsNotPending_ShouldReturnFalse()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.Status = "Success";
            verification.CompletedAt = null;

            // Act
            var result = verification.IsPending;

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region IsPurchaseActive Property Tests

        [Fact]
        public void IsPurchaseActive_WhenPurchasedAndNotExpired_ShouldReturnTrue()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PurchaseState = "Purchased";
            verification.ExpiryTimeMillis = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds();

            // Act
            var result = verification.IsPurchaseActive;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsPurchaseActive_WhenPurchasedAndNoExpiry_ShouldReturnTrue()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PurchaseState = "Purchased";
            verification.ExpiryTimeMillis = null; // Lifetime purchase

            // Act
            var result = verification.IsPurchaseActive;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsPurchaseActive_WhenPurchasedButExpired_ShouldReturnFalse()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PurchaseState = "Purchased";
            verification.ExpiryTimeMillis = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();

            // Act
            var result = verification.IsPurchaseActive;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsPurchaseActive_WhenNotPurchased_ShouldReturnFalse()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PurchaseState = "Cancelled";
            verification.ExpiryTimeMillis = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds();

            // Act
            var result = verification.IsPurchaseActive;

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region VerificationAgeHours Property Tests

        [Fact]
        public void VerificationAgeHours_WhenCompletedAtIsNull_ShouldReturnZero()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.CompletedAt = null;

            // Act
            var result = verification.VerificationAgeHours;

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public void VerificationAgeHours_WhenCompletedOneHourAgo_ShouldReturnOne()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.CompletedAt = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();

            // Act
            var result = verification.VerificationAgeHours;

            // Assert
            result.Should().BeApproximately(1.0, 0.1);
        }

        [Fact]
        public void VerificationAgeHours_WhenCompletedOneDayAgo_ShouldReturn24()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.CompletedAt = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();

            // Act
            var result = verification.VerificationAgeHours;

            // Assert
            result.Should().BeApproximately(24.0, 0.1);
        }

        #endregion

        #region IsStale Property Tests

        [Fact]
        public void IsStale_WhenVerificationIsOlderThan24Hours_ShouldReturnTrue()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.CompletedAt = DateTimeOffset.UtcNow.AddHours(-25).ToUnixTimeSeconds();

            // Act
            var result = verification.IsStale;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsStale_WhenVerificationIsWithin24Hours_ShouldReturnFalse()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.CompletedAt = DateTimeOffset.UtcNow.AddHours(-12).ToUnixTimeSeconds();

            // Act
            var result = verification.IsStale;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsStale_WhenCompletedAtIsNull_ShouldReturnFalse()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.CompletedAt = null;

            // Act
            var result = verification.IsStale;

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region PriceAmount Property Tests

        [Fact]
        public void PriceAmount_WhenPriceAmountMicrosIsNull_ShouldReturnNull()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PriceAmountMicros = null;

            // Act
            var result = verification.PriceAmount;

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void PriceAmount_WhenPriceAmountMicrosHasValue_ShouldConvertCorrectly()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PriceAmountMicros = 4990000; // $4.99 in micros

            // Act
            var result = verification.PriceAmount;

            // Assert
            result.Should().Be(4.99m);
        }

        [Fact]
        public void PriceAmount_WhenPriceAmountMicrosIsZero_ShouldReturnZero()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PriceAmountMicros = 0;

            // Act
            var result = verification.PriceAmount;

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public void PriceAmount_WhenPriceAmountMicrosIsOne_ShouldReturnOneMicro()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PriceAmountMicros = 1;

            // Act
            var result = verification.PriceAmount;

            // Assert
            result.Should().Be(0.000001m);
        }

        #endregion

        #region DateTime Conversion Properties Tests

        [Fact]
        public void PurchaseTime_WhenPurchaseTimeMillisIsNull_ShouldReturnNull()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PurchaseTimeMillis = null;

            // Act
            var result = verification.PurchaseTime;

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void PurchaseTime_WhenPurchaseTimeMillisHasValue_ShouldConvertCorrectly()
        {
            // Arrange
            var expectedTime = DateTimeOffset.UtcNow;
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PurchaseTimeMillis = expectedTime.ToUnixTimeMilliseconds();

            // Act
            var result = verification.PurchaseTime;

            // Assert
            result.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void ExpiryTime_WhenExpiryTimeMillisIsNull_ShouldReturnNull()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.ExpiryTimeMillis = null;

            // Act
            var result = verification.ExpiryTime;

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ExpiryTime_WhenExpiryTimeMillisHasValue_ShouldConvertCorrectly()
        {
            // Arrange
            var expectedTime = DateTimeOffset.UtcNow.AddDays(30);
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.ExpiryTimeMillis = expectedTime.ToUnixTimeMilliseconds();

            // Act
            var result = verification.ExpiryTime;

            // Assert
            result.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1));
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void PurchaseVerification_SuccessfulAndActivePurchase_ShouldHaveCorrectProperties()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.Status = "Success";
            verification.PurchaseState = "Purchased";
            verification.ExpiryTimeMillis = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds();
            verification.CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds();

            // Act & Assert
            verification.IsSuccessful.Should().BeTrue();
            verification.IsPending.Should().BeFalse();
            verification.IsPurchaseActive.Should().BeTrue();
            verification.IsStale.Should().BeFalse();
            verification.VerificationAgeHours.Should().BeApproximately(0.5, 0.1);
        }

        [Fact]
        public void PurchaseVerification_FailedVerification_ShouldHaveCorrectProperties()
        {
            // Arrange
            var verification = TestDataBuilder.CreateFailedVerification();

            // Act & Assert
            verification.IsSuccessful.Should().BeFalse();
            verification.IsPending.Should().BeFalse();
            verification.IsPurchaseActive.Should().BeFalse();
        }

        [Fact]
        public void PurchaseVerification_PendingVerification_ShouldHaveCorrectProperties()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.Status = "Pending";
            verification.CompletedAt = null;

            // Act & Assert
            verification.IsSuccessful.Should().BeFalse();
            verification.IsPending.Should().BeTrue();
            verification.VerificationAgeHours.Should().Be(0);
            verification.IsStale.Should().BeFalse();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void PurchaseVerification_WithExtremeValues_ShouldHandleGracefully()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PriceAmountMicros = long.MaxValue;
            verification.ExpiryTimeMillis = long.MaxValue;

            // Act & Assert
            verification.PriceAmount.Should().NotBeNull(); // PriceAmount calculation should work
            verification.ExpiryTime.Should().BeNull(); // ExpiryTime should return null for out-of-range values
            verification.IsPurchaseActive.Should().BeTrue(); // Max value is far in future (uses raw milliseconds)
        }

        [Fact]
        public void PurchaseVerification_WithMinValues_ShouldHandleGracefully()
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.PriceAmountMicros = 0;
            verification.ExpiryTimeMillis = 0; // Unix epoch

            // Act & Assert
            verification.PriceAmount.Should().Be(0);
            verification.ExpiryTime.Should().Be(DateTimeOffset.UnixEpoch);
            verification.IsPurchaseActive.Should().BeFalse(); // Epoch is in the past
        }

        [Theory]
        [InlineData("success")]
        [InlineData("SUCCESS")]
        [InlineData("Success")]
        public void IsSuccessful_ShouldBeCaseSensitive(string status)
        {
            // Arrange
            var verification = TestDataBuilder.CreateSuccessfulVerification();
            verification.Status = status;
            verification.ErrorMessage = null;

            // Act
            var result = verification.IsSuccessful;

            // Assert
            if (status == "Success")
            {
                result.Should().BeTrue();
            }
            else
            {
                result.Should().BeFalse();
            }
        }

        #endregion
    }
}