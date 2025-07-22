using FluentAssertions;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewWords.Api.Services;
using NewWords.Api.Services.interfaces;
using NewWords.Api.Tests.Helpers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using Xunit;

namespace NewWords.Api.Tests.Services
{
    /// <summary>
    /// Tests for GooglePlayBillingService functionality.
    /// </summary>
    public class GooglePlayBillingServiceTests : IDisposable
    {
        private readonly ILogger<GooglePlayBillingService> _mockLogger;
        private readonly IConfiguration _mockConfiguration;
        private readonly AndroidPublisherService _mockAndroidPublisherService;
        private readonly PurchasesResource _mockPurchasesResource;
        private readonly PurchasesResource.ProductsResource _mockProductsResource;
        private readonly PurchasesResource.SubscriptionsResource _mockSubscriptionsResource;
        private readonly GooglePlayBillingService _service;

        private const string TestPackageName = "com.shukebeta.newwords";
        private const string TestProductId = "monthly_premium";
        private const string TestSubscriptionId = "monthly_premium";
        private const string TestPurchaseToken = "test_purchase_token_123";

        public GooglePlayBillingServiceTests()
        {
            _mockLogger = Substitute.For<ILogger<GooglePlayBillingService>>();
            _mockConfiguration = Substitute.For<IConfiguration>();
            
            // Setup configuration
            _mockConfiguration["GooglePlay:PackageName"].Returns(TestPackageName);
            _mockConfiguration["GooglePlay:ServiceAccountKeyJson"].Returns(GetValidFakeServiceAccountJson());

            // Create mock Android Publisher Service and resources
            _mockAndroidPublisherService = Substitute.For<AndroidPublisherService>();
            _mockPurchasesResource = Substitute.For<PurchasesResource>(_mockAndroidPublisherService);
            _mockProductsResource = Substitute.For<PurchasesResource.ProductsResource>(_mockAndroidPublisherService);
            _mockSubscriptionsResource = Substitute.For<PurchasesResource.SubscriptionsResource>(_mockAndroidPublisherService);

            // Wire up the resource hierarchy
            _mockAndroidPublisherService.Purchases.Returns(_mockPurchasesResource);
            _mockPurchasesResource.Products.Returns(_mockProductsResource);
            _mockPurchasesResource.Subscriptions.Returns(_mockSubscriptionsResource);

            // Note: In a real implementation, we'd need to inject the AndroidPublisherService
            // For now, we'll test the logic that we can control
            _service = new GooglePlayBillingService(_mockLogger, _mockConfiguration);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        private static string GetValidFakeServiceAccountJson()
        {
            // Return a valid (but fake) service account JSON structure
            return """
            {
                "type": "service_account",
                "project_id": "test-project",
                "private_key_id": "test-key-id",
                "private_key": "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDGGPJQJ8h9Zfo1\n...(fake key)...\n-----END PRIVATE KEY-----\n",
                "client_email": "test@test-project.iam.gserviceaccount.com",
                "client_id": "123456789",
                "auth_uri": "https://accounts.google.com/o/oauth2/auth",
                "token_uri": "https://oauth2.googleapis.com/token",
                "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
                "client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/test%40test-project.iam.gserviceaccount.com"
            }
            """;
        }

        #region VerifyPurchaseAsync Tests

        [Fact]
        public async Task VerifyPurchaseAsync_WithValidPurchase_ShouldReturnValidResult()
        {
            // Arrange
            var mockPurchase = MockGooglePlayHelper.CreateMockProductPurchase(
                orderId: "ORDER_123",
                purchaseState: 0, // Purchased
                acknowledgementState: 1 // Acknowledged
            );

            var mockRequest = MockGooglePlayHelper.CreateMockProductGetRequest(mockPurchase);
            _mockProductsResource.Get(TestPackageName, TestProductId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.VerifyPurchaseAsync(TestPackageName, TestProductId, TestPurchaseToken);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.OrderId.Should().Be("ORDER_123");
            result.PurchaseState.Should().Be("0");
            result.AcknowledgementState.Should().Be("1");
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task VerifyPurchaseAsync_WithInvalidToken_ShouldReturnInvalidResult()
        {
            // Arrange
            var exception = MockGooglePlayHelper.CreateGoogleApiException(
                HttpStatusCode.NotFound, 
                "Purchase token not found");

            var mockRequest = Substitute.For<PurchasesResource.ProductsResource.GetRequest>(
                _mockAndroidPublisherService, TestPackageName, TestProductId, "invalid_token");
            
            mockRequest.ExecuteAsync(Arg.Any<CancellationToken>()).Throws(exception);
            
            _mockProductsResource.Get(TestPackageName, TestProductId, "invalid_token")
                .Returns(mockRequest);

            // Act
            var result = await _service.VerifyPurchaseAsync(TestPackageName, TestProductId, "invalid_token");

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Purchase token not found");
            result.HttpStatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task VerifyPurchaseAsync_WithNetworkError_ShouldReturnInvalidResult()
        {
            // Arrange
            var mockRequest = Substitute.For<PurchasesResource.ProductsResource.GetRequest>(
                _mockAndroidPublisherService, TestPackageName, TestProductId, TestPurchaseToken);
            
            mockRequest.ExecuteAsync(Arg.Any<CancellationToken>())
                .Throws(new HttpRequestException("Network error"));
            
            _mockProductsResource.Get(TestPackageName, TestProductId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.VerifyPurchaseAsync(TestPackageName, TestProductId, TestPurchaseToken);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Network error");
            result.HttpStatusCode.Should().BeNull();
        }

        #endregion

        #region VerifySubscriptionAsync Tests

        [Fact]
        public async Task VerifySubscriptionAsync_WithValidSubscription_ShouldReturnValidResult()
        {
            // Arrange
            var mockSubscription = MockGooglePlayHelper.CreateMockSubscriptionPurchase(
                orderId: "SUB_ORDER_123",
                autoRenewing: true,
                priceAmountMicros: 4990000 // $4.99
            );

            var mockRequest = MockGooglePlayHelper.CreateMockSubscriptionGetRequest(mockSubscription);
            _mockSubscriptionsResource.Get(TestPackageName, TestSubscriptionId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.VerifySubscriptionAsync(TestPackageName, TestSubscriptionId, TestPurchaseToken);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.OrderId.Should().Be("SUB_ORDER_123");
            result.AutoRenewing.Should().BeTrue();
            result.PriceAmountMicros.Should().Be(4990000);
            result.PriceCurrencyCode.Should().Be("USD");
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task VerifySubscriptionAsync_WithExpiredSubscription_ShouldReturnValidButExpiredResult()
        {
            // Arrange
            var mockSubscription = MockGooglePlayHelper.CreateExpiredSubscriptionPurchase(
                orderId: "EXPIRED_ORDER_123",
                cancelReason: 0 // User cancelled
            );

            var mockRequest = MockGooglePlayHelper.CreateMockSubscriptionGetRequest(mockSubscription);
            _mockSubscriptionsResource.Get(TestPackageName, TestSubscriptionId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.VerifySubscriptionAsync(TestPackageName, TestSubscriptionId, TestPurchaseToken);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue(); // API call succeeded
            result.OrderId.Should().Be("EXPIRED_ORDER_123");
            result.AutoRenewing.Should().BeFalse();
            result.CancelReason.Should().Be("0");
            result.ExpiryTimeMillis.Should().BeLessThan(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        [Fact]
        public async Task VerifySubscriptionAsync_WithInvalidToken_ShouldReturnInvalidResult()
        {
            // Arrange
            var exception = MockGooglePlayHelper.CreateGoogleApiException(
                HttpStatusCode.BadRequest, 
                "Invalid subscription token");

            var mockRequest = Substitute.For<PurchasesResource.SubscriptionsResource.GetRequest>(
                _mockAndroidPublisherService, TestPackageName, TestSubscriptionId, "invalid_token");
            
            mockRequest.ExecuteAsync(Arg.Any<CancellationToken>()).Throws(exception);
            
            _mockSubscriptionsResource.Get(TestPackageName, TestSubscriptionId, "invalid_token")
                .Returns(mockRequest);

            // Act
            var result = await _service.VerifySubscriptionAsync(TestPackageName, TestSubscriptionId, "invalid_token");

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Invalid subscription token");
            result.HttpStatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        }

        #endregion

        #region AcknowledgePurchaseAsync Tests

        [Fact]
        public async Task AcknowledgePurchaseAsync_WithValidRequest_ShouldReturnTrue()
        {
            // Arrange
            var mockRequest = MockGooglePlayHelper.CreateMockProductAcknowledgeRequest();
            _mockProductsResource.Acknowledge(
                Arg.Any<ProductPurchasesAcknowledgeRequest>(),
                TestPackageName,
                TestProductId,
                TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.AcknowledgePurchaseAsync(TestPackageName, TestProductId, TestPurchaseToken);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task AcknowledgePurchaseAsync_WithError_ShouldReturnFalse()
        {
            // Arrange
            var mockRequest = Substitute.For<PurchasesResource.ProductsResource.AcknowledgeRequest>(
                _mockAndroidPublisherService,
                Substitute.For<ProductPurchasesAcknowledgeRequest>(),
                TestPackageName,
                TestProductId,
                TestPurchaseToken);
            
            mockRequest.ExecuteAsync(Arg.Any<CancellationToken>())
                .Throws(new Exception("Acknowledgment failed"));
            
            _mockProductsResource.Acknowledge(
                Arg.Any<ProductPurchasesAcknowledgeRequest>(),
                TestPackageName,
                TestProductId,
                TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.AcknowledgePurchaseAsync(TestPackageName, TestProductId, TestPurchaseToken);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetSubscriptionStatusAsync Tests

        [Fact]
        public async Task GetSubscriptionStatusAsync_WithActiveSubscription_ShouldReturnActiveStatus()
        {
            // Arrange
            var futureExpiry = DateTimeOffset.UtcNow.AddDays(30);
            var mockSubscription = MockGooglePlayHelper.CreateMockSubscriptionPurchase(
                expiryTimeMillis: futureExpiry.ToUnixTimeMilliseconds(),
                autoRenewing: true
            );

            var mockRequest = MockGooglePlayHelper.CreateMockSubscriptionGetRequest(mockSubscription);
            _mockSubscriptionsResource.Get(TestPackageName, TestSubscriptionId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.GetSubscriptionStatusAsync(TestPackageName, TestSubscriptionId, TestPurchaseToken);

            // Assert
            result.Should().NotBeNull();
            result.IsActive.Should().BeTrue();
            result.IsExpired.Should().BeFalse();
            result.IsCancelled.Should().BeFalse();
            result.WillAutoRenew.Should().BeTrue();
            result.ExpiryTime.Should().BeCloseTo(futureExpiry, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GetSubscriptionStatusAsync_WithExpiredSubscription_ShouldReturnInactiveStatus()
        {
            // Arrange
            var pastExpiry = DateTimeOffset.UtcNow.AddDays(-1);
            var mockSubscription = MockGooglePlayHelper.CreateMockSubscriptionPurchase(
                expiryTimeMillis: pastExpiry.ToUnixTimeMilliseconds(),
                autoRenewing: false
            );

            var mockRequest = MockGooglePlayHelper.CreateMockSubscriptionGetRequest(mockSubscription);
            _mockSubscriptionsResource.Get(TestPackageName, TestSubscriptionId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.GetSubscriptionStatusAsync(TestPackageName, TestSubscriptionId, TestPurchaseToken);

            // Assert
            result.Should().NotBeNull();
            result.IsActive.Should().BeFalse();
            result.IsExpired.Should().BeTrue();
            result.IsCancelled.Should().BeFalse(); // Not cancelled, just expired
            result.WillAutoRenew.Should().BeFalse();
        }

        [Fact]
        public async Task GetSubscriptionStatusAsync_WithCancelledSubscription_ShouldReturnCancelledStatus()
        {
            // Arrange
            var mockSubscription = MockGooglePlayHelper.CreateCancelledSubscriptionPurchase(
                cancelReason: 0 // User cancelled
            );

            var mockRequest = MockGooglePlayHelper.CreateMockSubscriptionGetRequest(mockSubscription);
            _mockSubscriptionsResource.Get(TestPackageName, TestSubscriptionId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.GetSubscriptionStatusAsync(TestPackageName, TestSubscriptionId, TestPurchaseToken);

            // Assert
            result.Should().NotBeNull();
            result.IsActive.Should().BeFalse();
            result.IsExpired.Should().BeFalse(); // Not expired, but cancelled
            result.IsCancelled.Should().BeTrue();
            result.CancelReason.Should().Be("0");
        }

        [Fact]
        public async Task GetSubscriptionStatusAsync_WithInvalidToken_ShouldReturnInactiveStatus()
        {
            // Arrange
            var mockRequest = Substitute.For<PurchasesResource.SubscriptionsResource.GetRequest>(
                _mockAndroidPublisherService, TestPackageName, TestSubscriptionId, "invalid_token");
            
            mockRequest.ExecuteAsync(Arg.Any<CancellationToken>())
                .Throws(new Exception("Token not found"));
            
            _mockSubscriptionsResource.Get(TestPackageName, TestSubscriptionId, "invalid_token")
                .Returns(mockRequest);

            // Act
            var result = await _service.GetSubscriptionStatusAsync(TestPackageName, TestSubscriptionId, "invalid_token");

            // Assert
            result.Should().NotBeNull();
            result.IsActive.Should().BeFalse();
            result.IsExpired.Should().BeTrue();
            result.IsCancelled.Should().BeTrue();
        }

        #endregion

        #region CancelSubscriptionAsync Tests

        [Fact]
        public async Task CancelSubscriptionAsync_WithValidRequest_ShouldReturnTrue()
        {
            // Arrange
            var mockRequest = MockGooglePlayHelper.CreateMockSubscriptionCancelRequest();
            _mockSubscriptionsResource.Cancel(TestPackageName, TestSubscriptionId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.CancelSubscriptionAsync(TestPackageName, TestSubscriptionId, TestPurchaseToken);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task CancelSubscriptionAsync_WithError_ShouldReturnFalse()
        {
            // Arrange
            var mockRequest = Substitute.For<PurchasesResource.SubscriptionsResource.CancelRequest>(
                _mockAndroidPublisherService,
                TestPackageName,
                TestSubscriptionId,
                TestPurchaseToken);
            
            mockRequest.ExecuteAsync(Arg.Any<CancellationToken>())
                .Throws(new Exception("Cancellation failed"));
            
            _mockSubscriptionsResource.Cancel(TestPackageName, TestSubscriptionId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            var result = await _service.CancelSubscriptionAsync(TestPackageName, TestSubscriptionId, TestPurchaseToken);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region RefundPurchaseAsync Tests

        [Fact]
        public async Task RefundPurchaseAsync_ShouldReturnFalse()
        {
            // Arrange & Act
            // Google Play doesn't provide automatic refund API for developers
            var result = await _service.RefundPurchaseAsync(TestPackageName, TestProductId, TestPurchaseToken);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Configuration and Error Handling Tests

        [Fact]
        public void Constructor_WithMissingConfiguration_ShouldUseDefaults()
        {
            // Arrange
            var emptyConfig = Substitute.For<IConfiguration>();
            emptyConfig["GooglePlay:PackageName"].Returns((string?)null);

            // Act & Assert
            // Should not throw exception and should use default package name
            var service = new GooglePlayBillingService(_mockLogger, emptyConfig);
            service.Should().NotBeNull();
        }

        [Fact]
        public async Task VerifySubscriptionAsync_ShouldLogInformation()
        {
            // Arrange
            var mockSubscription = MockGooglePlayHelper.CreateMockSubscriptionPurchase();
            var mockRequest = MockGooglePlayHelper.CreateMockSubscriptionGetRequest(mockSubscription);
            _mockSubscriptionsResource.Get(TestPackageName, TestSubscriptionId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            await _service.VerifySubscriptionAsync(TestPackageName, TestSubscriptionId, TestPurchaseToken);

            // Assert
            _mockLogger.Received().LogInformation(
                "Verifying subscription: {SubscriptionId} for package {PackageName}",
                TestSubscriptionId,
                TestPackageName);
        }

        [Fact]
        public async Task VerifyPurchaseAsync_WithException_ShouldLogError()
        {
            // Arrange
            var mockRequest = Substitute.For<PurchasesResource.ProductsResource.GetRequest>(
                _mockAndroidPublisherService, TestPackageName, TestProductId, TestPurchaseToken);
            
            var exception = new Exception("Test error");
            mockRequest.ExecuteAsync(Arg.Any<CancellationToken>()).Throws(exception);
            
            _mockProductsResource.Get(TestPackageName, TestProductId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            await _service.VerifyPurchaseAsync(TestPackageName, TestProductId, TestPurchaseToken);

            // Assert
            _mockLogger.Received().LogError(
                exception,
                "Failed to verify purchase {ProductId}: {Error}",
                TestProductId,
                "Test error");
        }

        #endregion

        #region Cancellation Token Tests

        [Fact]
        public async Task VerifySubscriptionAsync_WithCancellationToken_ShouldPassTokenToRequest()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            
            var mockSubscription = MockGooglePlayHelper.CreateMockSubscriptionPurchase();
            var mockRequest = Substitute.For<PurchasesResource.SubscriptionsResource.GetRequest>(
                _mockAndroidPublisherService, TestPackageName, TestSubscriptionId, TestPurchaseToken);
            
            mockRequest.ExecuteAsync(cancellationToken).Returns(Task.FromResult(mockSubscription));
            
            _mockSubscriptionsResource.Get(TestPackageName, TestSubscriptionId, TestPurchaseToken)
                .Returns(mockRequest);

            // Act
            await _service.VerifySubscriptionAsync(TestPackageName, TestSubscriptionId, TestPurchaseToken, cancellationToken);

            // Assert
            await mockRequest.Received(1).ExecuteAsync(cancellationToken);
        }

        #endregion
    }
}