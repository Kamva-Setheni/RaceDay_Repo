using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Register, log in and log out. Everything goes through the Part 2 API.</summary>
public class AccountController : BaseController
{
    private readonly RaceDayApiClient _api;

    public AccountController(RaceDayApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (CurrentUser is not null) return RedirectToRoleHome(CurrentUser.Role);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _api.LoginAsync(model.Email, model.Password);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty,
                result.IsUnauthorized ? "Incorrect email address or password." : result.Error ?? "We could not log you in.");
            return View(model);
        }

        StartSession(result.Data!);
        TempData["Success"] = $"Welcome back, {result.Data!.User.FullName}!";

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }
        return RedirectToRoleHome(result.Data.User.Role);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (CurrentUser is not null) return RedirectToRoleHome(CurrentUser.Role);
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role == "Organiser" && string.IsNullOrWhiteSpace(model.OrganiserCode))
        {
            ModelState.AddModelError(nameof(model.OrganiserCode), "Organisers need the organiser sign-up code.");
        }
        if (!ModelState.IsValid) return View(model);

        var registered = await _api.RegisterAsync(model.FullName, model.Email, model.Password, model.Role, model.OrganiserCode);
        if (!registered.Success)
        {
            ModelState.AddModelError(string.Empty, registered.Error ?? "We could not create your account.");
            return View(model);
        }

        // Log the new user straight in
        var login = await _api.LoginAsync(model.Email, model.Password);
        if (!login.Success)
        {
            TempData["Success"] = "Your account has been created. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        StartSession(login.Data!);
        TempData["Success"] = "Welcome to RaceDay! Your account is ready.";
        return RedirectToRoleHome(login.Data!.User.Role);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _api.LogoutAsync();          // ends the API session (best effort)
        HttpContext.Session.Clear();       // ends the MVC session
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    /// <summary>Remembers the user's id and role in the session so every page knows who they are.</summary>
    private void StartSession(LoginResult login)
    {
        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, login.User.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, login.User.Role);
        HttpContext.Session.SetString(SessionKeys.FullName, login.User.FullName);
        HttpContext.Session.SetString(SessionKeys.ProfilePictureUrl, login.User.ProfilePictureUrl ?? string.Empty);
        HttpContext.Session.SetString(SessionKeys.ApiCookie, login.ApiCookie);
    }
}
