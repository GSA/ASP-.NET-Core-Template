using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ASP_Core_MVC_Template.Models;
using GSA.FM.Utility.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;

namespace ASP_Core_MVC_Template.Controllers
{
    public class HomeController : Controller
    {
        private readonly IFMUtilityConfigService _configService;
        private readonly ILogger<HomeController> _logger;
        private readonly IStringLocalizer<SharedResource> _sharedLocalizer;

        public HomeController(IFMUtilityConfigService configService, ILogger<HomeController> logger,
            IStringLocalizer<SharedResource> sharedLocalizer)
        {
            _configService = configService;
            _logger = logger;
            _sharedLocalizer = sharedLocalizer;
        }

        public IActionResult Index()
        {
            var warning = HttpContext.Session.GetString("warning");
            if (string.IsNullOrEmpty(warning))
            {
                return RedirectToAction("Warning");
            }

            // Query app status from the config service.
            var closeMessage = _configService.GetAppClosed("CORE_TEMPLATE");
            var warningMessage = _configService.GetAppWarning("CORE_TEMPLATE");

            // Are we closed (via CAAM)?
            if (closeMessage != "OPEN")
            {
                ViewData["closeMessage"] = closeMessage;
            }

            // Do we have a warning (via CAAM)?
            if (warningMessage != "OPEN")
            {
                ViewData["warningMessage"] = warningMessage;
            }

            return View();
        }

        [HttpPost]
        public IActionResult ConfirmWarning()
        {
            HttpContext.Session.SetString("warning", "true");
            return Json(true);
        }

        public IActionResult Warning()
        {
            return View();
        }

        [Authorize(Roles = "COREADMIN,COREUSER")]
        public IActionResult LoggedIn()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
