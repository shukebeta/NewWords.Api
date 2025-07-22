using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using NSubstitute;

namespace NewWords.Api.Tests.Helpers
{
    /// <summary>
    /// Helper class for creating mock Google Play API responses and services.
    /// </summary>
    public static class MockGooglePlayHelper
    {
        public static AndroidPublisherService CreateMockAndroidPublisherService()
        {
            return Substitute.For<AndroidPublisherService>();
        }

        public static ProductPurchase CreateMockProductPurchase(
            string orderId = "TEST_ORDER_123",
            int? purchaseState = 0, // 0 = Purchased
            int? acknowledgementState = 1, // 1 = Acknowledged
            long? purchaseTimeMillis = null,
            string? developerPayload = null)
        {
            var purchase = new ProductPurchase
            {
                OrderId = orderId,
                PurchaseState = purchaseState,
                AcknowledgementState = acknowledgementState,
                PurchaseTimeMillis = purchaseTimeMillis ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DeveloperPayload = developerPayload
            };

            return purchase;
        }

        public static SubscriptionPurchase CreateMockSubscriptionPurchase(
            string orderId = "TEST_ORDER_123",
            int? acknowledgementState = 1, // 1 = Acknowledged
            long? startTimeMillis = null,
            long? expiryTimeMillis = null,
            bool? autoRenewing = true,
            long? priceAmountMicros = 4990000, // $4.99
            string priceCurrencyCode = "USD",
            int? paymentState = 1, // 1 = Received
            string countryCode = "US")
        {
            var now = DateTimeOffset.UtcNow;
            var subscription = new SubscriptionPurchase
            {
                OrderId = orderId,
                AcknowledgementState = acknowledgementState,
                StartTimeMillis = startTimeMillis ?? now.ToUnixTimeMilliseconds(),
                ExpiryTimeMillis = expiryTimeMillis ?? now.AddMonths(1).ToUnixTimeMilliseconds(),
                AutoRenewing = autoRenewing,
                PriceAmountMicros = priceAmountMicros,
                PriceCurrencyCode = priceCurrencyCode,
                PaymentState = paymentState,
                CountryCode = countryCode
            };

            return subscription;
        }

        public static SubscriptionPurchase CreateExpiredSubscriptionPurchase(
            string orderId = "EXPIRED_ORDER_123",
            int? cancelReason = 0) // 0 = User cancelled
        {
            var now = DateTimeOffset.UtcNow;
            return new SubscriptionPurchase
            {
                OrderId = orderId,
                StartTimeMillis = now.AddMonths(-2).ToUnixTimeMilliseconds(),
                ExpiryTimeMillis = now.AddDays(-1).ToUnixTimeMilliseconds(),
                AutoRenewing = false,
                PriceAmountMicros = 4990000,
                PriceCurrencyCode = "USD",
                PaymentState = 1,
                CancelReason = cancelReason,
                UserCancellationTimeMillis = now.AddDays(-1).ToUnixTimeMilliseconds()
            };
        }

        public static SubscriptionPurchase CreateCancelledSubscriptionPurchase(
            string orderId = "CANCELLED_ORDER_123",
            int? cancelReason = 0) // 0 = User cancelled
        {
            var now = DateTimeOffset.UtcNow;
            return new SubscriptionPurchase
            {
                OrderId = orderId,
                StartTimeMillis = now.AddMonths(-1).ToUnixTimeMilliseconds(),
                ExpiryTimeMillis = now.AddDays(29).ToUnixTimeMilliseconds(), // Still valid but cancelled
                AutoRenewing = false,
                PriceAmountMicros = 4990000,
                PriceCurrencyCode = "USD",
                PaymentState = 1,
                CancelReason = cancelReason,
                UserCancellationTimeMillis = now.AddDays(-5).ToUnixTimeMilliseconds()
            };
        }

        public static Google.GoogleApiException CreateGoogleApiException(
            System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.BadRequest,
            string message = "Invalid request")
        {
            // Note: Google.GoogleApiException is difficult to mock directly
            // We'll create a basic exception for testing
            return new Google.GoogleApiException("GoogleApi", message)
            {
                HttpStatusCode = statusCode
            };
        }

        public static ProductPurchasesAcknowledgeRequest CreateAcknowledgeRequest(
            string? developerPayload = null)
        {
            return new ProductPurchasesAcknowledgeRequest
            {
                DeveloperPayload = developerPayload
            };
        }

        public static PurchasesResource CreateMockPurchasesResource()
        {
            return Substitute.For<PurchasesResource>(Substitute.For<AndroidPublisherService>());
        }

        public static PurchasesResource.ProductsResource CreateMockProductsResource()
        {
            return Substitute.For<PurchasesResource.ProductsResource>(Substitute.For<AndroidPublisherService>());
        }

        public static PurchasesResource.SubscriptionsResource CreateMockSubscriptionsResource()
        {
            return Substitute.For<PurchasesResource.SubscriptionsResource>(Substitute.For<AndroidPublisherService>());
        }

        public static PurchasesResource.ProductsResource.GetRequest CreateMockProductGetRequest(
            ProductPurchase purchase)
        {
            var request = Substitute.For<PurchasesResource.ProductsResource.GetRequest>(
                Substitute.For<AndroidPublisherService>(),
                "packageName",
                "productId", 
                "purchaseToken");
            
            request.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(purchase));
            return request;
        }

        public static PurchasesResource.SubscriptionsResource.GetRequest CreateMockSubscriptionGetRequest(
            SubscriptionPurchase subscription)
        {
            var request = Substitute.For<PurchasesResource.SubscriptionsResource.GetRequest>(
                Substitute.For<AndroidPublisherService>(),
                "packageName",
                "subscriptionId", 
                "purchaseToken");
            
            request.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(subscription));
            return request;
        }

        public static PurchasesResource.ProductsResource.AcknowledgeRequest CreateMockProductAcknowledgeRequest()
        {
            var request = Substitute.For<PurchasesResource.ProductsResource.AcknowledgeRequest>(
                Substitute.For<AndroidPublisherService>(),
                Substitute.For<ProductPurchasesAcknowledgeRequest>(),
                "packageName",
                "productId",
                "purchaseToken");
            
            request.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            return request;
        }

        public static PurchasesResource.SubscriptionsResource.CancelRequest CreateMockSubscriptionCancelRequest()
        {
            var request = Substitute.For<PurchasesResource.SubscriptionsResource.CancelRequest>(
                Substitute.For<AndroidPublisherService>(),
                "packageName",
                "subscriptionId",
                "purchaseToken");
            
            request.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            return request;
        }
    }
}