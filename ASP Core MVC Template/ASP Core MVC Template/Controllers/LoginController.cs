using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using AspNetCore.LegacyAuthCookieCompat;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using GSA.FM.Utility.Core.Interfaces;

namespace ASP_Core_MVC_Template.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoginController> _logger;
        private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
        private readonly IFMUtilityDataAPIService _dataAPIService;
        private readonly IFMUtilityAuditService _auditService;

        public LoginController(IWebHostEnvironment env, IConfiguration configuration,
            ILogger<LoginController> logger, IStringLocalizer<SharedResource> sharedLocalizer,
            IFMUtilityDataAPIService dataAPIService, IFMUtilityAuditService auditService)
        {
            _env = env;
            _configuration = configuration;
            _logger = logger;
            _sharedLocalizer = sharedLocalizer;
            _dataAPIService = dataAPIService;
            _auditService = auditService;
        }

        [Route("Login/LoginAsync")]
        public async Task<IActionResult> LoginAsync()
        {
            if (!HttpContext.User.Identity.IsAuthenticated)
            {
                // Detect if we're running locally within Visual Studio.
                if (_env.IsDevelopment())
                {
                    // Create principal and authorize.
                    var principal = CreateIdentity("williammdinkel", "william.dinkel@gsa.gov");
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                    // Now redirect to determine this user's roles.
                    return RedirectToAction("CheckCAAM", "Login");
                }
                else
                {
                    // Redirect to SecureAuth based on parameters in the config file.
                    var samlEndpoint = _configuration["SecureAuth:RedirectURL"];
                    return Redirect(samlEndpoint);
                }
            }
            // Already logged in.
            else return RedirectToAction("Index", "Home");
        }

        [Route("Login/LoginFailed")]
        public IActionResult LoginFailed()
        {
            return View();
        }

        [Route("Login/LogoutAsync")]
        public async Task<IActionResult> LogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // Audit logout.
            if (User.Identity.IsAuthenticated)
            {
                await _auditService.WriteUserEvent("DEMO Template", User.Identity.Name, UserEvent.Logoff);
            }
            
            return RedirectToAction("Index", "Home");
        }

        [Route("Login/SessionTimeoutAsync")]
        public async Task<IActionResult> SessionTimeoutAsync()
        {
            // Audit logout.
            if (User.Identity.IsAuthenticated)
            {
                await _auditService.WriteUserEvent("DEMO Template", User.Identity.Name, UserEvent.SessionTimeout);
            }

            return RedirectToAction("LogoutAsync", "Login");
        }

        public async Task<IActionResult> ExternalLoginCallback()
        {
            // This is the SecureAuth callback, which is reached by a 302. The token that
            // SecureAuth sends is a cookie.
            var viewDataErrorKey = "secureAuthError";
            var tokenName = _configuration["SecureAuth:TokenName"];
            string token = Request.Cookies[tokenName];

            if (token != null)
            {
                // Decrypt the token with our SecureAuth keys.
                var validationKey = _configuration["SecureAuth:ValidationKey"];
                var decryptionKey = _configuration["SecureAuth:DecryptionKey"];

                // Default to Framework45 for compatibility mode.
                CompatibilityMode compatibilityMode = CompatibilityMode.Framework45;
                if (_configuration.GetValue<bool>("SecureAuth:UseCompatibilityMode20SP2"))
                {
                    compatibilityMode = CompatibilityMode.Framework20SP2;
                }

                if (validationKey == null || decryptionKey == null)
                {
                    ViewData[viewDataErrorKey] = "SecureAuth keys missing from configuration file.";
                }
                else
                {
                    byte[] decryptionKeyBytes = HexUtils.HexToBinary(decryptionKey);
                    byte[] validationKeyBytes = HexUtils.HexToBinary(validationKey);

                    try
                    {
                        var legacyFormsAuthenticationTicketEncryptor = new LegacyFormsAuthenticationTicketEncryptor(decryptionKeyBytes, validationKeyBytes, ShaVersion.Sha1, compatibilityMode);
                        FormsAuthenticationTicket decryptedTicket = legacyFormsAuthenticationTicketEncryptor.DecryptCookie(token);

                        // If already authenticated and usernames don't match, log out.
                        if (User.Identity.IsAuthenticated)
                        {
                            if (decryptedTicket.Name != User.Identity.Name) return await LogoutAsync();
                        }
                        else
                        {
                            // Let's authenticate!
                            // Create a user principal object for this user.
                            var principal = CreateIdentity(decryptedTicket.Name, decryptedTicket.UserData);
                            // Authenticate using the identity.
                            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                            // Now redirect to determine this user's roles.
                            return RedirectToAction("CheckCAAM", "Login");
                        }
                    }
                    catch (Exception ex)
                    {
                        ViewData[viewDataErrorKey] = ex.Message;
                    }
                }
            }
            else
            {
                // No token.
                ViewData[viewDataErrorKey] = "SecureAuth post-authentication token is missing.";
            }

            // Append our common error message.
            ViewData[viewDataErrorKey] = String.Format("{0} {1}", ViewData[viewDataErrorKey],
                _sharedLocalizer["CaptureAndEmailUsMessage"]);
            return View();
        }

        [Authorize]
        public async Task<IActionResult> CheckCAAM()
        {
            var viewDataErrorKey = "loginFailedError";

            // Use our utility Data API service to check for roles.
            try
            {
                var caamRoles = _dataAPIService.GetCAAMRolesWithKey("Core Template", User.Identity.Name,
                    _configuration["FMDataAPIKey"]);

                if (caamRoles.Count > 0)
                {
                    var roleName = caamRoles[0];
                    _logger.LogInformation("User " + User.Identity.Name + " has the " + roleName + " role.");

                    // Create a new user principal with the role.
                    var principal = CreateIdentity(User.Identity.Name, User.FindFirst(ClaimTypes.Email).Value, roleName);
                    // Re-Authenticate using the identity.
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                    // Audit successful logon.
                    await _auditService.WriteUserEvent("DEMO Template", User.Identity.Name, UserEvent.LogonSuccessful);

                    return RedirectToAction("LoggedIn", "Home");
                }
                else
                {
                    // No roles found. Log the failure.
                    var message = "User " + User.Identity.Name + " has no valid roles.";
                    _logger.LogInformation(message);
                    TempData[viewDataErrorKey] = message;

                    // Log out.
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                    // Audit failed logon.
                    await _auditService.WriteUserEvent("DEMO Template", User.Identity.Name, UserEvent.LogonFailed);

                    return RedirectToAction("LoginFailed", "Login");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                TempData[viewDataErrorKey] = ex.Message;

                // Log out.
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Audit failed logon.
                await _auditService.WriteUserEvent("DEMO Template", User.Identity.Name, UserEvent.LogonFailed);

                return RedirectToAction("LoginFailed", "Login");
            }
        }

        private ClaimsPrincipal CreateIdentity(string username, string email, string roleName = "")
        {
            // Create the identity object including role.
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, username));
            identity.AddClaim(new Claim(ClaimTypes.Name, username));
            identity.AddClaim(new Claim(ClaimTypes.Email, email));
            if (roleName != "") identity.AddClaim(new Claim(ClaimTypes.Role, roleName));

            // Create a principal object.
            return new ClaimsPrincipal(identity);
        }
    }
}
