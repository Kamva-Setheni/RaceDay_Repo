using System.Globalization;
using Microsoft.AspNetCore.Localization;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- MVC ----------
// Every POST form is protected against cross-site request forgery
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

// ---------- Session management ----------
// The MVC session remembers who is logged in (user id and role) on every page.
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(builder.Configuration.GetValue("Session:IdleTimeoutMinutes", 60));
});

// ---------- The RaceDay API (the only place data comes from) ----------
// UseCookies = false: the client must never share cookies between users. The API session
// cookie of each user is kept in that user's MVC session and added to each request instead.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5003";
builder.Services.AddHttpClient<RaceDayApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });

var app = builder.Build();

// Visitors never see raw error pages or stack traces
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/StatusCodePage", "?code={0}");

// One culture everywhere, so decimals such as 42.2 post and display the same on every machine
var invariant = CultureInfo.InvariantCulture;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(invariant),
    SupportedCultures = new List<CultureInfo> { invariant },
    SupportedUICultures = new List<CultureInfo> { invariant }
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
