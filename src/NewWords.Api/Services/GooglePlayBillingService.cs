using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewWords.Api.Services.interfaces;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;

namespace NewWords.Api.Services
{
    /// <summary>
    /// Service for interacting with Google Play Billing API.
    /// Implements purchase verification and subscription management.
    /// </summary>
    public class GooglePlayBillingService : IGooglePlayBillingService
    {
        private readonly ILogger<GooglePlayBillingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly AndroidPublisherService? _androidPublisherService;
        private readonly string _packageName;

        public GooglePlayBillingService(
            ILogger<GooglePlayBillingService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _packageName = _configuration["GooglePlay:PackageName"] ?? "com.shukebeta.newwords";

            // Initialize Google Play service with error handling for test environments
            try
            {
                var credential = GetGoogleCredential();
                _androidPublisherService = new AndroidPublisherService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "NewWords API"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Google Play service. This is expected in test environments.");
                // In test environments or when credentials are not available, we'll use a null service
                // Individual methods will handle this gracefully
                _androidPublisherService = null;
            }
        }

        public async Task<GooglePlayVerificationResult> VerifyPurchaseAsync(
            string packageName, 
            string productId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default)
        {
            if (_androidPublisherService == null)
            {
                _logger.LogWarning("Google Play service not initialized. Returning failed verification.");
                return new GooglePlayVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = "Google Play service not available",
                    HttpStatusCode = 503
                };
            }

            try
            {
                _logger.LogInformation("Verifying purchase: {ProductId} for package {PackageName}", productId, packageName);

                var request = _androidPublisherService.Purchases.Products.Get(packageName, productId, purchaseToken);
                var response = await request.ExecuteAsync(cancellationToken);

                return new GooglePlayVerificationResult
                {
                    IsValid = true,
                    OrderId = response.OrderId,
                    PurchaseState = response.PurchaseState?.ToString(),
                    AcknowledgementState = response.AcknowledgementState?.ToString(),
                    PurchaseTimeMillis = response.PurchaseTimeMillis,
                    DeveloperPayload = response.DeveloperPayload,
                    RawResponse = JsonSerializer.Serialize(response)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify purchase {ProductId}: {Error}", productId, ex.Message);
                return new GooglePlayVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message,
                    HttpStatusCode = ex is Google.GoogleApiException gae ? (int)gae.HttpStatusCode : null
                };
            }
        }

        public async Task<GooglePlaySubscriptionResult> VerifySubscriptionAsync(
            string packageName, 
            string subscriptionId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default)
        {
            if (_androidPublisherService == null)
            {
                _logger.LogWarning("Google Play service not initialized. Returning failed verification.");
                return new GooglePlaySubscriptionResult
                {
                    IsValid = false,
                    ErrorMessage = "Google Play service not available",
                    HttpStatusCode = 503
                };
            }

            try
            {
                _logger.LogInformation("Verifying subscription: {SubscriptionId} for package {PackageName}", subscriptionId, packageName);

                var request = _androidPublisherService.Purchases.Subscriptions.Get(packageName, subscriptionId, purchaseToken);
                var response = await request.ExecuteAsync(cancellationToken);

                return new GooglePlaySubscriptionResult
                {
                    IsValid = true,
                    OrderId = response.OrderId,
                    PurchaseState = "Purchased", // Subscriptions are always purchased if API returns success
                    AcknowledgementState = response.AcknowledgementState?.ToString(),
                    StartTimeMillis = response.StartTimeMillis,
                    ExpiryTimeMillis = response.ExpiryTimeMillis,
                    AutoRenewing = response.AutoRenewing,
                    PriceAmountMicros = response.PriceAmountMicros,
                    PriceCurrencyCode = response.PriceCurrencyCode,
                    PaymentState = response.PaymentState?.ToString(),
                    CancelReason = response.CancelReason?.ToString(),
                    UserCancellationTimeMillis = response.UserCancellationTimeMillis?.ToString(),
                    CountryCode = response.CountryCode,
                    RawResponse = JsonSerializer.Serialize(response)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify subscription {SubscriptionId}: {Error}", subscriptionId, ex.Message);
                return new GooglePlaySubscriptionResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message,
                    HttpStatusCode = ex is Google.GoogleApiException gae ? (int)gae.HttpStatusCode : null
                };
            }
        }

        public async Task<bool> AcknowledgePurchaseAsync(
            string packageName, 
            string productId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Acknowledging purchase: {ProductId}", productId);

                var request = _androidPublisherService.Purchases.Products.Acknowledge(
                    new Google.Apis.AndroidPublisher.v3.Data.ProductPurchasesAcknowledgeRequest(),
                    packageName,
                    productId,
                    purchaseToken);

                await request.ExecuteAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acknowledge purchase {ProductId}: {Error}", productId, ex.Message);
                return false;
            }
        }

        public async Task<GooglePlaySubscriptionStatus> GetSubscriptionStatusAsync(
            string packageName, 
            string subscriptionId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var verificationResult = await VerifySubscriptionAsync(packageName, subscriptionId, purchaseToken, cancellationToken);
                
                if (!verificationResult.IsValid)
                {
                    return new GooglePlaySubscriptionStatus
                    {
                        IsActive = false,
                        IsExpired = true,
                        IsCancelled = true
                    };
                }

                var now = DateTimeOffset.UtcNow;
                var expiryTime = verificationResult.ExpiryTimeMillis.HasValue 
                    ? DateTimeOffset.FromUnixTimeMilliseconds(verificationResult.ExpiryTimeMillis.Value)
                    : (DateTimeOffset?)null;

                var isExpired = expiryTime.HasValue && expiryTime.Value <= now;
                var isCancelled = !string.IsNullOrEmpty(verificationResult.CancelReason);

                return new GooglePlaySubscriptionStatus
                {
                    IsActive = !isExpired && !isCancelled,
                    IsExpired = isExpired,
                    IsCancelled = isCancelled,
                    WillAutoRenew = verificationResult.AutoRenewing == true,
                    ExpiryTime = expiryTime,
                    CancelReason = verificationResult.CancelReason,
                    PaymentState = verificationResult.PaymentState
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get subscription status {SubscriptionId}: {Error}", subscriptionId, ex.Message);
                return new GooglePlaySubscriptionStatus
                {
                    IsActive = false,
                    IsExpired = true,
                    IsCancelled = true
                };
            }
        }

        public async Task<bool> RefundPurchaseAsync(
            string packageName, 
            string productId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Refunding purchase: {ProductId}", productId);

                // Note: Google Play doesn't provide a direct refund API for developers
                // Refunds typically need to be processed through Google Play Console manually
                // This method is included for completeness but will log a warning
                _logger.LogWarning("Refund requested for {ProductId}, but Google Play doesn't provide automatic refund API. Manual processing required.", productId);
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refund purchase {ProductId}: {Error}", productId, ex.Message);
                return false;
            }
        }

        public async Task<bool> CancelSubscriptionAsync(
            string packageName, 
            string subscriptionId, 
            string purchaseToken, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Cancelling subscription: {SubscriptionId}", subscriptionId);

                var request = _androidPublisherService.Purchases.Subscriptions.Cancel(packageName, subscriptionId, purchaseToken);
                await request.ExecuteAsync(cancellationToken);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel subscription {SubscriptionId}: {Error}", subscriptionId, ex.Message);
                return false;
            }
        }

        private GoogleCredential GetGoogleCredential()
        {
            var credentialPath = _configuration["GooglePlay:ServiceAccountKeyPath"];
            var credentialJson = _configuration["GooglePlay:ServiceAccountKeyJson"];

            if (!string.IsNullOrEmpty(credentialPath) && File.Exists(credentialPath))
            {
                _logger.LogInformation("Loading Google credential from file: {Path}", credentialPath);
                return GoogleCredential.FromFile(credentialPath)
                    .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
            }
            
            if (!string.IsNullOrEmpty(credentialJson))
            {
                _logger.LogInformation("Loading Google credential from JSON configuration");
                return GoogleCredential.FromJson(credentialJson)
                    .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
            }

            _logger.LogWarning("No Google credential configuration found. Using default credentials.");
            return GoogleCredential.GetApplicationDefault()
                .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
        }

        public void Dispose()
        {
            _androidPublisherService?.Dispose();
        }
    }
}