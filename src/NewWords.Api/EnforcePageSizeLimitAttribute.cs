using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace NewWords.Api
{
    [AttributeUsage(AttributeTargets.Method)]
    public class EnforcePageSizeLimitAttribute(int maxPageSize) : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.TryGetValue("pageSize", out var pageSizeObj) && pageSizeObj is int pageSize)
            {
                if (pageSize > maxPageSize)
                {
                    context.ActionArguments["pageSize"] = maxPageSize;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}
