using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace NewWords.Api
{
    [AttributeUsage(AttributeTargets.Method)]
    public class EnforcePageSizeLimitAttribute : ActionFilterAttribute
    {
        private readonly int _maxPageSize;

        public EnforcePageSizeLimitAttribute(int maxPageSize)
        {
            _maxPageSize = maxPageSize;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.TryGetValue("pageSize", out var pageSizeObj) && pageSizeObj is int pageSize)
            {
                if (pageSize > _maxPageSize)
                {
                    context.ActionArguments["pageSize"] = _maxPageSize;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}