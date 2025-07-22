using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents a user's subscription status and details.
    /// Implements the mixed strategy: local persistence + smart verification.
    /// </summary>
    [SugarTable("UserSubscriptions")]
    public class UserSubscription
    {
        /// <summary>
        /// Unique identifier for the subscription record (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the Users table.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int UserId { get; set; }

        /// <summary>
        /// Subscription tier: Free, Monthly, Yearly, Lifetime
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 20)]
        public string SubscriptionTier { get; set; } = "Free";

        /// <summary>
        /// Whether the subscription is currently active.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// When the subscription starts (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long StartedAt { get; set; }

        /// <summary>
        /// When the subscription expires (Unix timestamp). Null for lifetime subscriptions.
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? ExpiresAt { get; set; }

        /// <summary>
        /// Google Play purchase token for verification.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 1000)]
        public string? PurchaseToken { get; set; }

        /// <summary>
        /// Google Play product ID that was purchased.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 100)]
        public string? ProductId { get; set; }

        /// <summary>
        /// Google Play order ID for tracking.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 100)]
        public string? OrderId { get; set; }

        /// <summary>
        /// When this subscription was last verified with Google Play (Unix timestamp).
        /// Used for smart verification scheduling.
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? LastVerifiedAt { get; set; }

        /// <summary>
        /// When this subscription record was created (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; }

        /// <summary>
        /// When this subscription record was last updated (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? UpdatedAt { get; set; }

        /// <summary>
        /// Soft delete timestamp (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? DeletedAt { get; set; }

        /// <summary>
        /// Current word count for the user (to track free tier limits).
        /// Updated when words are added/removed.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int CurrentWordCount { get; set; } = 0;

        /// <summary>
        /// Version for optimistic concurrency control.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int Version { get; set; } = 1;

        /// <summary>
        /// Navigation property to User entity.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public User? User { get; set; }

        /// <summary>
        /// Checks if the subscription is expired based on current time.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// Checks if the subscription will expire soon (within 3 days).
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool WillExpireSoon => ExpiresAt.HasValue && 
            ExpiresAt.Value <= DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds();

        /// <summary>
        /// Checks if the subscription needs verification (not verified in last 24 hours).
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool NeedsVerification => LastVerifiedAt == null || 
            LastVerifiedAt <= DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();

        /// <summary>
        /// Gets the word limit for this subscription tier.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public int WordLimit => SubscriptionTier switch
        {
            "Free" => 500,
            "Monthly" or "Yearly" or "Lifetime" => -1, // Unlimited
            _ => 500 // Default to free
        };

        /// <summary>
        /// Checks if the user can add more words.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool CanAddWords => SubscriptionTier != "Free" || CurrentWordCount < WordLimit;

        /// <summary>
        /// Gets remaining words for free tier users.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public int RemainingWords => SubscriptionTier == "Free" 
            ? Math.Max(0, WordLimit - CurrentWordCount) 
            : -1; // Unlimited
    }
}