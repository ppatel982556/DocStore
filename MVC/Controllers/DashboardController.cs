using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Repositories.Services.LayoutService;

namespace MVC.Controllers
{
    [Authorize]
    public class DashboardController : BaseController
    {
        public DashboardController(
            ILayoutService layoutService)
            : base(layoutService)
        {
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}