using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents historical records of subscription changes and purchase events.
    /// Used for audit trails, debugging, and analytics.
    /// </summary>
    [SugarTable("SubscriptionHistory")]
    public class SubscriptionHistory
    {
        /// <summary>
        /// Unique identifier for the history record (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the Users table.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int UserId { get; set; }

        /// <summary>
        /// Foreign key to the UserSubscriptions table (nullable for events before subscription creation).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? UserSubscriptionId { get; set; }

        /// <summary>
        /// Type of event: Purchase, Upgrade, Downgrade, Renewal, Cancellation, Expiration, Verification
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 50)]
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Subscription tier before the change.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 20)]
        public string? PreviousTier { get; set; }

        /// <summary>
        /// Subscription tier after the change.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 20)]
        public string? NewTier { get; set; }

        /// <summary>
        /// Google Play purchase token associated with this event.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 1000)]
        public string? PurchaseToken { get; set; }

        /// <summary>
        /// Google Play product ID for this event.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 100)]
        public string? ProductId { get; set; }

        /// <summary>
        /// Google Play order ID for this event.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 100)]
        public string? OrderId { get; set; }

        /// <summary>
        /// Amount paid for this purchase (in cents, if applicable).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? AmountPaid { get; set; }

        /// <summary>
        /// Currency code for the payment (e.g., USD, EUR).
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 3)]
        public string? Currency { get; set; }

        /// <summary>
        /// Previous expiration date (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? PreviousExpiresAt { get; set; }

        /// <summary>
        /// New expiration date (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? NewExpiresAt { get; set; }

        /// <summary>
        /// Additional event metadata in JSON format.
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
        public string? Metadata { get; set; }

        /// <summary>
        /// Error message if the event failed.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Source of the event: App, GooglePlay, Admin, System
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 20)]
        public string EventSource { get; set; } = "App";

        /// <summary>
        /// IP address of the user when event occurred (for security auditing).
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 45)]
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent of the client when event occurred.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 500)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// When this history record was created (Unix timestamp).
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
        /// Checks if this was a successful event (no error message).
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsSuccessful => string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Checks if this event involved a payment.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsPaymentEvent => EventType is "Purchase" or "Renewal" or "Upgrade" && AmountPaid.HasValue && AmountPaid.Value > 0;

        /// <summary>
        /// Gets the tier change direction.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public string ChangeDirection
        {
            get
            {
                if (PreviousTier == null && NewTier != null) return "Initial";
                if (PreviousTier != null && NewTier == null) return "Cancellation";
                if (PreviousTier == NewTier)
                {
                    // Both are equal, but if they are unknown tiers, treat as "Change"
                    if (PreviousTier == "Unknown") return "Change";
                    return "No Change";
                }
                
                return (PreviousTier, NewTier) switch
                {
                    ("Free", "Monthly" or "Yearly" or "Lifetime") => "Upgrade",
                    ("Monthly", "Yearly" or "Lifetime") => "Upgrade",
                    ("Yearly", "Lifetime") => "Upgrade",
                    ("Monthly" or "Yearly" or "Lifetime", "Free") => "Downgrade",
                    ("Yearly" or "Lifetime", "Monthly") => "Downgrade",
                    ("Lifetime", "Yearly") => "Downgrade",
                    _ => "Change"
                };
            }
        }
    }
}