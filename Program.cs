using System.Globalization;

using ICP;

using ICP.Data;

using ICP.Filters;

using ICP.Models.Auth;

using ICP.Services;

using Microsoft.AspNetCore.Authentication.Negotiate;

using Microsoft.AspNetCore.Localization;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Options;



var builder = WebApplication.CreateBuilder(args);



builder.Services.Configure<AppAuthOptions>(

    builder.Configuration.GetSection(AppAuthOptions.SectionName));



builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");



var supportedCultures = new[] { "zh-TW", "en", "ja" };

builder.Services.Configure<RequestLocalizationOptions>(options =>

{

    options.SetDefaultCulture("zh-TW");

    options.AddSupportedCultures(supportedCultures);

    options.AddSupportedUICultures(supportedCultures);

    options.RequestCultureProviders =

    [

        new CookieRequestCultureProvider(),

        new AcceptLanguageHeaderRequestCultureProvider()

    ];

});



builder.Services.AddHttpContextAccessor();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>

{

    options.Cookie.HttpOnly = true;

    options.Cookie.IsEssential = true;

    options.IdleTimeout = TimeSpan.FromHours(8);

});



builder.Services

    .AddAuthentication(NegotiateDefaults.AuthenticationScheme)

    .AddNegotiate();



builder.Services.AddAuthorization();



builder.Services.AddScoped<PermissionScannerService>();

builder.Services.AddScoped<PermissionResourceSyncService>();

builder.Services.AddScoped<UserInfoResolver>();

builder.Services.AddScoped<LoginSessionService>();

builder.Services.AddScoped<RequireLoginFilter>();



builder.Services

    .AddControllersWithViews(options =>

    {

        options.Filters.Add<RequireLoginFilter>();

    })

    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)

    .AddDataAnnotationsLocalization(options =>

        options.DataAnnotationLocalizerProvider = (_, factory) =>

            factory.Create(typeof(SharedResource)));



builder.Services.AddDbContext<ApplicationDbContext>(options =>

{

    var connectionString = builder.Configuration.GetConnectionString("ICP_Connection");

    if (!string.IsNullOrWhiteSpace(connectionString))

    {

        options.UseSqlServer(connectionString);

    }

});



builder.Services.AddDbContext<IlcDbContext>(options =>

{

    var connectionString = builder.Configuration.GetConnectionString("ILC_Connection");

    if (!string.IsNullOrWhiteSpace(connectionString))

    {

        options.UseSqlServer(connectionString);

    }

});



var app = builder.Build();



if (!app.Environment.IsDevelopment())

{

    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();

}



app.UseHttpsRedirection();

app.UseRouting();

var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();



app.MapStaticAssets();



app.MapControllerRoute(

    name: "default",

    pattern: "{controller=Home}/{action=Index}/{id?}")

    .WithStaticAssets();



app.Run();

