using Api.Framework.Models;
using Api.Framework.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Entities;
using NewWords.Api.Services.interfaces;
using NewWords.Api.Models.DTOs.Subscription;

namespace NewWords.Api.Controllers
{
    /// <summary>
    /// Controller for managing user subscriptions and premium features.
    /// </summary>
    [Authorize]
    public class SubscriptionController(
        ISubscriptionService subscriptionService,
        ICurrentUser currentUser) 
        : BaseController
    {
        /// <summary>
        /// Gets the current user's subscription status.
        /// </summary>
        /// <param name="forceRefresh">Force verification with Google Play</param>
        /// <returns>Current subscription details</returns>
        [HttpGet]
        public async Task<ApiResult<UserSubscription>> GetStatus(bool forceRefresh = false)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                return new FailedResult<UserSubscription>(null, "User not authenticated");
            }

            var subscription = await subscriptionService.GetUserSubscriptionAsync(userId, forceRefresh);
            return new SuccessfulResult<UserSubscription>(subscription);
        }

        /// <summary>
        /// Processes a new subscription purchase from mobile app.
        /// </summary>
        /// <param name="request">Purchase verification request</param>
        /// <returns>Updated subscription status</returns>
        [HttpPost]
        public async Task<ApiResult<UserSubscription>> ProcessPurchase(ProcessPurchaseRequest request)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                return new FailedResult<UserSubscription>(null, "User not authenticated");
            }

            if (string.IsNullOrEmpty(request.PurchaseToken) || string.IsNullOrEmpty(request.ProductId))
            {
                return new FailedResult<UserSubscription>(null, "Purchase token and product ID are required");
            }

            return await subscriptionService.ProcessPurchaseAsync(userId, request.PurchaseToken, request.ProductId);
        }

        /// <summary>
        /// Restores user's purchases from Google Play.
        /// Used when user reinstalls app or logs in on new device.
        /// </summary>
        /// <param name="request">Purchase tokens to restore</param>
        /// <returns>Restored subscription status</returns>
        [HttpPost]
        public async Task<ApiResult<UserSubscription>> RestorePurchases(RestorePurchasesRequest request)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                return new FailedResult<UserSubscription>(null, "User not authenticated");
            }

            if (request.PurchaseTokens == null || !request.PurchaseTokens.Any())
            {
                return new FailedResult<UserSubscription>(null, "At least one purchase token is required");
            }

            return await subscriptionService.RestorePurchasesAsync(userId, request.PurchaseTokens);
        }

        /// <summary>
        /// Cancels the user's current subscription.
        /// </summary>
        /// <param name="request">Cancellation request</param>
        /// <returns>Cancellation result</returns>
        [HttpPost]
        public async Task<ApiResult<bool>> Cancel(CancelSubscriptionRequest request)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                return new FailedResult<bool>(false, "User not authenticated");
            }

            var reason = string.IsNullOrEmpty(request.Reason) ? "User requested" : request.Reason;
            return await subscriptionService.CancelSubscriptionAsync(userId, reason);
        }

        /// <summary>
        /// Checks if user can add more words based on their subscription.
        /// </summary>
        /// <returns>True if user can add words</returns>
        [HttpGet]
        public async Task<ApiResult<bool>> CanAddWords()
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                return new FailedResult<bool>(false, "User not authenticated");
            }

            var canAdd = await subscriptionService.CanUserAddWordsAsync(userId);
            return new SuccessfulResult<bool>(canAdd);
        }

        /// <summary>
        /// Gets the remaining word count for the user.
        /// </summary>
        /// <returns>Remaining words (-1 for unlimited)</returns>
        [HttpGet]
        public async Task<ApiResult<int>> GetRemainingWords()
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                return new FailedResult<int>(0, "User not authenticated");
            }

            var remaining = await subscriptionService.GetRemainingWordsAsync(userId);
            return new SuccessfulResult<int>(remaining);
        }

        /// <summary>
        /// Gets subscription history for the current user.
        /// </summary>
        /// <param name="pageSize">Page size for pagination</param>
        /// <param name="pageNumber">Page number for pagination</param>
        /// <returns>Paginated subscription history</returns>
        [HttpGet]
        public async Task<ApiResult<PageData<SubscriptionHistory>>> GetHistory(int pageSize = 20, int pageNumber = 1)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                return new FailedResult<PageData<SubscriptionHistory>>(null, "User not authenticated");
            }

            var history = await subscriptionService.GetSubscriptionHistoryAsync(userId, pageSize, pageNumber);
            return new SuccessfulResult<PageData<SubscriptionHistory>>(history);
        }

        /// <summary>
        /// Validates and refreshes the user's subscription with Google Play.
        /// </summary>
        /// <returns>Updated subscription status</returns>
        [HttpPost]
        public async Task<ApiResult<UserSubscription>> ValidateSubscription()
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                return new FailedResult<UserSubscription>(null, "User not authenticated");
            }

            return await subscriptionService.ValidateSubscriptionAsync(userId);
        }

        /// <summary>
        /// Gets subscription analytics data for admin users.
        /// Requires elevated permissions.
        /// </summary>
        /// <param name="startDate">Start date for analytics (ISO 8601)</param>
        /// <param name="endDate">End date for analytics (ISO 8601)</param>
        /// <returns>Analytics data</returns>
        [HttpGet]
        [Authorize(Roles = "Admin")] // Assuming admin role exists
        public async Task<ApiResult<SubscriptionAnalytics>> GetAnalytics(string startDate, string endDate)
        {
            if (!DateTimeOffset.TryParse(startDate, out var start) || 
                !DateTimeOffset.TryParse(endDate, out var end))
            {
                return new FailedResult<SubscriptionAnalytics>(null, "Invalid date format. Use ISO 8601 format.");
            }

            if (start >= end)
            {
                return new FailedResult<SubscriptionAnalytics>(null, "Start date must be before end date");
            }

            var analytics = await subscriptionService.GetSubscriptionAnalyticsAsync(start, end);
            return new SuccessfulResult<SubscriptionAnalytics>(analytics);
        }
    }
}