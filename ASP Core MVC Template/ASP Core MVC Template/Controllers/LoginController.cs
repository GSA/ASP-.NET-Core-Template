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

        public LoginController(IWebHostEnvironment env, IConfiguration configuration,
            ILogger<LoginController> logger, IStringLocalizer<SharedResource> sharedLocalizer,
            IFMUtilityDataAPIService dataAPIService)
        {
            _env = env;
            _configuration = configuration;
            _logger = logger;
            _sharedLocalizer = sharedLocalizer;
            _dataAPIService = dataAPIService;
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
                    var samlEndpoint = Startup.Configuration.GetValue<string>("SecureAuth:RedirectURL");
                    return Redirect(samlEndpoint);
                }
            }
            // Already logged in.
            else return RedirectToAction("Index", "Home");
        }

        [Route("Login/LogoutAsync")]
        public async Task<IActionResult> LogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> ExternalLoginCallback()
        {
            // This is the SecureAuth callback, which is reached by a 302. The token that
            // SecureAuth sends is a cookie.
            var viewDataErrorKey = "secureAuthError";
            var tokenName = Startup.Configuration.GetValue<string>("SecureAuth:TokenName");
            string token = Request.Cookies[tokenName];

            if (token != null)
            {
                // Decrypt the token with our SecureAuth keys.
                var validationKey = _configuration["SecureAuth:ValidationKey"];
                var decryptionKey = _configuration["SecureAuth:DecryptionKey"];

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
                        var legacyFormsAuthenticationTicketEncryptor = new LegacyFormsAuthenticationTicketEncryptor(decryptionKeyBytes, validationKeyBytes, ShaVersion.Sha1);
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
            // Use our utility Data API service to check for roles.
            try
            {
                var caamRoles = _dataAPIService.GetCAAMRoles("Core Template", User.Identity.Name,
                    Request.Cookies[_configuration["SharedCookieName"]]);

                if (caamRoles.Count > 0)
                {
                    var roleName = caamRoles[0];
                    _logger.LogInformation("User " + User.Identity.Name + " has the " + roleName + " role.");

                    // Create a new user principal with the role.
                    var principal = CreateIdentity(User.Identity.Name, User.FindFirst(ClaimTypes.Email).Value, roleName);
                    // Re-Authenticate using the identity.
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                    return RedirectToAction("LoggedIn", "Home");
                }
                else
                {
                    // No roles found. Log out.
                    _logger.LogInformation("User " + User.Identity.Name + " has no valid roles.");
                    return RedirectToAction("LogoutAsync", "Login");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return RedirectToAction("LogoutAsync", "Login");
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
