using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Repositories.Services.LayoutService;

namespace MVC.Controllers
{
    public abstract class BaseController : Controller
    {
        private readonly ILayoutService _layoutService;

        protected BaseController(ILayoutService layoutService)
        {
            _layoutService = layoutService;
        }

        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (int.TryParse(userIdClaim, out int userId))
                {
                    var layout = await _layoutService.GetLayoutAsync(userId);

                    ViewBag.Layout = layout;
                }
            }

            await next();
        }
    }
}