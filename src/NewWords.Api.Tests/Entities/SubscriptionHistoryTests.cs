using FluentAssertions;
using NewWords.Api.Tests.Helpers;
using Xunit;

namespace NewWords.Api.Tests.Entities
{
    /// <summary>
    /// Tests for SubscriptionHistory entity computed properties and business logic.
    /// </summary>
    public class SubscriptionHistoryTests
    {
        #region IsSuccessful Property Tests

        [Fact]
        public void IsSuccessful_WhenErrorMessageIsNull_ShouldReturnTrue()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.ErrorMessage = null;

            // Act
            var result = history.IsSuccessful;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsSuccessful_WhenErrorMessageIsEmpty_ShouldReturnTrue()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.ErrorMessage = string.Empty;

            // Act
            var result = history.IsSuccessful;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsSuccessful_WhenErrorMessageHasValue_ShouldReturnFalse()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.ErrorMessage = "Payment failed";

            // Act
            var result = history.IsSuccessful;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsSuccessful_WhenErrorMessageIsWhitespace_ShouldReturnFalse()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.ErrorMessage = "   ";

            // Act
            var result = history.IsSuccessful;

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region IsPaymentEvent Property Tests

        [Theory]
        [InlineData("Purchase")]
        [InlineData("Renewal")]
        [InlineData("Upgrade")]
        public void IsPaymentEvent_WhenEventTypeIsPaymentRelatedAndHasAmount_ShouldReturnTrue(string eventType)
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.EventType = eventType;
            history.AmountPaid = 999; // $9.99

            // Act
            var result = history.IsPaymentEvent;

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData("Purchase")]
        [InlineData("Renewal")]
        [InlineData("Upgrade")]
        public void IsPaymentEvent_WhenEventTypeIsPaymentRelatedButNoAmount_ShouldReturnFalse(string eventType)
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.EventType = eventType;
            history.AmountPaid = null;

            // Act
            var result = history.IsPaymentEvent;

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("Cancellation")]
        [InlineData("Expiration")]
        [InlineData("Verification")]
        public void IsPaymentEvent_WhenEventTypeIsNotPaymentRelated_ShouldReturnFalse(string eventType)
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.EventType = eventType;
            history.AmountPaid = 999;

            // Act
            var result = history.IsPaymentEvent;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsPaymentEvent_WhenAmountIsZero_ShouldReturnFalse()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.EventType = "Purchase";
            history.AmountPaid = 0;

            // Act
            var result = history.IsPaymentEvent;

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ChangeDirection Property Tests

        [Fact]
        public void ChangeDirection_WhenPreviousIsNullAndNewHasValue_ShouldReturnInitial()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.PreviousTier = null;
            history.NewTier = "Monthly";

            // Act
            var result = history.ChangeDirection;

            // Assert
            result.Should().Be("Initial");
        }

        [Fact]
        public void ChangeDirection_WhenPreviousHasValueAndNewIsNull_ShouldReturnCancellation()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.PreviousTier = "Monthly";
            history.NewTier = null;

            // Act
            var result = history.ChangeDirection;

            // Assert
            result.Should().Be("Cancellation");
        }

        [Fact]
        public void ChangeDirection_WhenPreviousAndNewAreEqual_ShouldReturnNoChange()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.PreviousTier = "Monthly";
            history.NewTier = "Monthly";

            // Act
            var result = history.ChangeDirection;

            // Assert
            result.Should().Be("No Change");
        }

        [Theory]
        [InlineData("Free", "Monthly")]
        [InlineData("Free", "Yearly")]
        [InlineData("Free", "Lifetime")]
        [InlineData("Monthly", "Yearly")]
        [InlineData("Monthly", "Lifetime")]
        [InlineData("Yearly", "Lifetime")]
        public void ChangeDirection_WhenUpgrading_ShouldReturnUpgrade(string previousTier, string newTier)
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.PreviousTier = previousTier;
            history.NewTier = newTier;

            // Act
            var result = history.ChangeDirection;

            // Assert
            result.Should().Be("Upgrade");
        }

        [Theory]
        [InlineData("Monthly", "Free")]
        [InlineData("Yearly", "Free")]
        [InlineData("Lifetime", "Free")]
        [InlineData("Yearly", "Monthly")]
        [InlineData("Lifetime", "Monthly")]
        [InlineData("Lifetime", "Yearly")]
        public void ChangeDirection_WhenDowngrading_ShouldReturnDowngrade(string previousTier, string newTier)
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.PreviousTier = previousTier;
            history.NewTier = newTier;

            // Act
            var result = history.ChangeDirection;

            // Assert
            result.Should().Be("Downgrade");
        }

        [Theory]
        [InlineData("Unknown", "Free")]
        [InlineData("Free", "Unknown")]
        [InlineData("Unknown", "Unknown")]
        public void ChangeDirection_WhenUnknownTiers_ShouldReturnChange(string previousTier, string newTier)
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.PreviousTier = previousTier;
            history.NewTier = newTier;

            // Act
            var result = history.ChangeDirection;

            // Assert
            result.Should().Be("Change");
        }

        #endregion

        #region Integration Tests for Multiple Properties

        [Fact]
        public void SubscriptionHistory_SuccessfulPurchase_ShouldHaveCorrectProperties()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory(
                eventType: "Purchase",
                previousTier: "Free",
                newTier: "Monthly");
            history.AmountPaid = 499;
            history.ErrorMessage = null;

            // Act & Assert
            history.IsSuccessful.Should().BeTrue();
            history.IsPaymentEvent.Should().BeTrue();
            history.ChangeDirection.Should().Be("Upgrade");
        }

        [Fact]
        public void SubscriptionHistory_FailedPurchase_ShouldHaveCorrectProperties()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory(
                eventType: "Purchase",
                previousTier: "Free",
                newTier: "Free"); // No change due to failure
            history.AmountPaid = null;
            history.ErrorMessage = "Payment declined";

            // Act & Assert
            history.IsSuccessful.Should().BeFalse();
            history.IsPaymentEvent.Should().BeFalse();
            history.ChangeDirection.Should().Be("No Change");
        }

        [Fact]
        public void SubscriptionHistory_Cancellation_ShouldHaveCorrectProperties()
        {
            // Arrange
            var history = TestDataBuilder.CreateCancellationHistory();
            history.AmountPaid = null;
            history.ErrorMessage = null;

            // Act & Assert
            history.IsSuccessful.Should().BeTrue();
            history.IsPaymentEvent.Should().BeFalse();
            history.ChangeDirection.Should().Be("Downgrade");
        }

        [Fact]
        public void SubscriptionHistory_Verification_ShouldHaveCorrectProperties()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory(
                eventType: "Verification",
                previousTier: "Monthly",
                newTier: "Monthly");
            history.AmountPaid = null;
            history.ErrorMessage = null;

            // Act & Assert
            history.IsSuccessful.Should().BeTrue();
            history.IsPaymentEvent.Should().BeFalse();
            history.ChangeDirection.Should().Be("No Change");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void ChangeDirection_WithNullValues_ShouldHandleGracefully()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.PreviousTier = null;
            history.NewTier = null;

            // Act
            var result = history.ChangeDirection;

            // Assert
            result.Should().Be("No Change"); // null -> null means no change occurred
        }

        [Fact]
        public void ChangeDirection_WithEmptyStrings_ShouldTreatAsChange()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.PreviousTier = "";
            history.NewTier = "";

            // Act
            var result = history.ChangeDirection;

            // Assert
            result.Should().Be("No Change");
        }

        [Fact]
        public void IsPaymentEvent_WithNegativeAmount_ShouldReturnFalse()
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.EventType = "Purchase";
            history.AmountPaid = -100;

            // Act
            var result = history.IsPaymentEvent;

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("purchase")]
        [InlineData("PURCHASE")]
        [InlineData("Purchase")]
        public void IsPaymentEvent_ShouldBeCaseSensitive(string eventType)
        {
            // Arrange
            var history = TestDataBuilder.CreatePurchaseHistory();
            history.EventType = eventType;
            history.AmountPaid = 999;

            // Act
            var result = history.IsPaymentEvent;

            // Assert
            if (eventType == "Purchase")
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