using DRS.Scaffold.Syslog.WebUI.Services;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7295";

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Session � stores JWT between requests
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout        = TimeSpan.FromHours(8);
    o.Cookie.HttpOnly    = true;
    o.Cookie.IsEssential = true;
    o.Cookie.Name        = ".SentinelLog.Session";
});

// Typed HttpClient for the backend REST API
builder.Services.AddHttpClient<ApiClient>(c =>
{
    c.BaseAddress = new Uri(apiBase);
    c.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

// Named HttpClient for the browser-side API proxy (/api/... pass-through)
builder.Services.AddHttpClient("ApiProxy", c =>
{
    c.BaseAddress = new Uri(apiBase);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();           // must come after UseRouting
app.UseAuthorization();

// /api/... proxy — forwards browser JS fetch calls to the backend API with JWT
app.Map("/api/{**rest}", async (HttpContext ctx, IHttpClientFactory factory, string? rest) =>
{
    var client  = factory.CreateClient("ApiProxy");
    var path    = "/" + (rest ?? string.Empty);
    var qs      = ctx.Request.QueryString.Value ?? string.Empty;
    var target  = new Uri(new Uri(apiBase), "api" + path + qs);

    using var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);

    var token = ctx.Session.GetString("Token");
    if (!string.IsNullOrEmpty(token))
        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Content-Type"))
    {
        req.Content = new StreamContent(ctx.Request.Body);
        if (ctx.Request.ContentType is { } ct)
            req.Content.Headers.TryAddWithoutValidation("Content-Type", ct);
    }

    using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
    if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        ctx.Session.Clear();
    ctx.Response.StatusCode = (int)resp.StatusCode;
    foreach (var h in resp.Headers.Concat(resp.Content.Headers))
    {
        if (!string.Equals(h.Key, "transfer-encoding", StringComparison.OrdinalIgnoreCase))
            ctx.Response.Headers[h.Key] = h.Value.ToArray();
    }
    await resp.Content.CopyToAsync(ctx.Response.Body);
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
