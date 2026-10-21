using GameHub.Data;
using GameHub.Helpers;
using GameHub.Hubs;
using GameHub.Services.Implementations;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddAuthentication(AuthConstants.Scheme)
    .AddCookie(AuthConstants.Scheme, options =>
    {
        options.Cookie.Name = "GameHub.Admin.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Error/NotFound";
    })
    .AddCookie(AuthConstants.CustomerScheme, options =>
    {
        options.Cookie.Name = "GameHub.Customer.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
        options.SlidingExpiration = true;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Error/NotFound";
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddScoped<IWebsiteInquiryNotifier, WebsiteInquiryNotifier>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".GameHub.Customer.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(15);
});
builder.Services.AddControllersWithViews(options =>
{
    // Run root-model validation (IValidatableObject cross-field rules) even when a
    // child property already failed. With the MVC default (false), cross-field errors
    // such as "Maximum salary must be >= minimum" or "Deadline must be in the future"
    // are silently skipped whenever the form contains any other validation error.
    options.ValidateComplexTypesIfChildValidationFails = true;
});
builder.Services.AddScoped<ICustomerCodeService, CustomerCodeService>();
builder.Services.AddScoped<IMembershipNumberService, MembershipNumberService>();
builder.Services.AddScoped<IBookingNumberService, BookingNumberService>();
builder.Services.AddScoped<IPaymentGateway, DemoPaymentGateway>();
builder.Services.AddScoped<IBookingValidationService, BookingValidationService>();
builder.Services.AddScoped<ICustomerMembershipService, CustomerMembershipService>();
builder.Services.AddScoped<IMembershipOperationService, MembershipOperationService>();
builder.Services.AddScoped<IBookingLifecycleService, BookingLifecycleService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IJobPostingNumberService, JobPostingNumberService>();
builder.Services.AddScoped<IJobApplicationNumberService, JobApplicationNumberService>();
builder.Services.AddScoped<IJobApplicationFileService, JobApplicationFileService>();
builder.Services.AddScoped<IJobApplicationDuplicateService, JobApplicationDuplicateService>();
builder.Services.AddScoped<IEmployeeNumberService, EmployeeNumberService>();
builder.Services.AddScoped<IOfferNumberService, OfferNumberService>();
builder.Services.AddScoped<ISimplePdfGenerator, SimplePdfGenerator>();
builder.Services.AddScoped<IPerformanceNumberService, PerformanceNumberService>();
builder.Services.AddScoped<ITerminationNumberService, TerminationNumberService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/NotFound");
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<CrmNotificationsHub>(CrmNotificationsHub.Route);

if (app.Environment.IsDevelopment())
{
    await DatabaseSeeder.SeedAsync(app.Services);
}

app.Run();
