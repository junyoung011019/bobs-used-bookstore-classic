using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Bookstore.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        private readonly IConfiguration _configuration;

        public AuthenticationController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Login(string redirectUri = null)
        {
            if (string.IsNullOrWhiteSpace(redirectUri)) return RedirectToAction("Index", "Home");
            return Redirect(redirectUri);
        }

        public IActionResult LogOut()
        {
            return _configuration["Services:Authentication"] == "aws" ? CognitoSignOut() : LocalSignOut();
        }

        private IActionResult LocalSignOut()
        {
            Response.Cookies.Delete("LocalAuthentication");
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult();
            return RedirectToAction("Index", "Home");
        }

        private IActionResult CognitoSignOut()
        {
            Response.Cookies.Delete(".AspNetCore.Cookies");
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult();

            var domain = _configuration["Authentication:Cognito:CognitoDomain"];
            var clientId = _configuration["Authentication:Cognito:LocalClientId"];
            var logoutUri = $"{Request.Scheme}://{Request.Host}/";

            return Redirect($"{domain}/logout?client_id={clientId}&logout_uri={logoutUri}");
        }
    }
}
