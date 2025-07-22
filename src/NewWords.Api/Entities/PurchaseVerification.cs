using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents Google Play purchase verification requests and results.
    /// Used for caching verification responses and debugging verification issues.
    /// </summary>
    [SugarTable("PurchaseVerifications")]
    public class PurchaseVerification
    {
        /// <summary>
        /// Unique identifier for the verification record (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the Users table.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int UserId { get; set; }

        /// <summary>
        /// Foreign key to the UserSubscriptions table (nullable for failed verifications).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? UserSubscriptionId { get; set; }

        /// <summary>
        /// Google Play purchase token being verified.
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 1000)]
        public string PurchaseToken { get; set; } = string.Empty;

        /// <summary>
        /// Google Play product ID being verified.
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 100)]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>
        /// Google Play order ID from verification response.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 100)]
        public string? OrderId { get; set; }

        /// <summary>
        /// Verification status: Pending, Success, Failed, Expired, Cancelled
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 20)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Purchase state from Google Play: Purchased, Pending, Cancelled
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 20)]
        public string? PurchaseState { get; set; }

        /// <summary>
        /// Acknowledgement state from Google Play: Acknowledged, Unacknowledged
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 20)]
        public string? AcknowledgementState { get; set; }

        /// <summary>
        /// Purchase timestamp from Google Play (Unix timestamp in milliseconds).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? PurchaseTimeMillis { get; set; }

        /// <summary>
        /// Start time for subscription from Google Play (Unix timestamp in milliseconds).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? StartTimeMillis { get; set; }

        /// <summary>
        /// Expiry time for subscription from Google Play (Unix timestamp in milliseconds).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? ExpiryTimeMillis { get; set; }

        /// <summary>
        /// Auto-renewing flag from Google Play.
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public bool? AutoRenewing { get; set; }

        /// <summary>
        /// Price amount in micro-units from Google Play.
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? PriceAmountMicros { get; set; }

        /// <summary>
        /// Price currency code from Google Play.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 3)]
        public string? PriceCurrencyCode { get; set; }

        /// <summary>
        /// Country code from Google Play verification.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 2)]
        public string? CountryCode { get; set; }

        /// <summary>
        /// Developer payload from the original purchase.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 500)]
        public string? DeveloperPayload { get; set; }

        /// <summary>
        /// Full Google Play API response in JSON format.
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
        public string? GooglePlayResponse { get; set; }

        /// <summary>
        /// Error message if verification failed.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// HTTP status code from Google Play API.
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// How long the verification took (in milliseconds).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? VerificationDurationMs { get; set; }

        /// <summary>
        /// Type of verification: Initial, Periodic, Manual, Renewal
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 20)]
        public string VerificationType { get; set; } = "Initial";

        /// <summary>
        /// When this verification was started (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long StartedAt { get; set; }

        /// <summary>
        /// When this verification was completed (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? CompletedAt { get; set; }

        /// <summary>
        /// When this verification record was created (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; }

        /// <summary>
        /// Navigation property to User entity.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public User? User { get; set; }

        /// <summary>
        /// Navigation property to UserSubscription entity.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public UserSubscription? UserSubscription { get; set; }

        /// <summary>
        /// Checks if the verification was successful.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsSuccessful => Status == "Success" && string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Checks if the verification is still pending.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsPending => Status == "Pending" && CompletedAt == null;

        /// <summary>
        /// Checks if the purchase is active based on Google Play response.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsPurchaseActive => PurchaseState == "Purchased" && 
                                       (ExpiryTimeMillis == null || ExpiryTimeMillis > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        /// <summary>
        /// Gets the verification age in hours.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public double VerificationAgeHours => CompletedAt.HasValue 
            ? (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - CompletedAt.Value) / 3600.0 
            : 0;

        /// <summary>
        /// Checks if this verification result is stale (older than 24 hours).
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsStale => VerificationAgeHours > 24;

        /// <summary>
        /// Gets the price in regular currency units (not micro-units).
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public decimal? PriceAmount => PriceAmountMicros.HasValue ? PriceAmountMicros.Value / 1_000_000m : null;

        /// <summary>
        /// Gets the purchase time as DateTimeOffset.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public DateTimeOffset? PurchaseTime => PurchaseTimeMillis.HasValue 
            ? SafeFromUnixTimeMilliseconds(PurchaseTimeMillis.Value) 
            : null;

        /// <summary>
        /// Gets the expiry time as DateTimeOffset.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public DateTimeOffset? ExpiryTime => ExpiryTimeMillis.HasValue 
            ? SafeFromUnixTimeMilliseconds(ExpiryTimeMillis.Value) 
            : null;

        /// <summary>
        /// Safely converts Unix milliseconds to DateTimeOffset, handling extreme values.
        /// </summary>
        private static DateTimeOffset? SafeFromUnixTimeMilliseconds(long milliseconds)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Handle extreme values gracefully - return null for out-of-range values
                return null;
            }
        }
    }
}