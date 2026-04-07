using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DRS.Scaffold.Syslog.WebUI.Models;
using Microsoft.Extensions.Logging;

namespace DRS.Scaffold.Syslog.WebUI.Services;

/// <summary>
/// Thin wrapper around HttpClient that targets the SentinelLog REST API.
/// Automatically attaches the Bearer token stored in the caller's session.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiClient> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ApiClient(HttpClient http, ILogger<ApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    // ?? Auth helpers ??????????????????????????????????????????????????????????

    public void SetBearerToken(string token) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    // ?? Auth endpoints ????????????????????????????????????????????????????????

    public async Task<LoginResponse?> LoginAsync(LoginRequest req)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(req, _json), Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("api/auth/login", content);

        var body = await resp.Content.ReadAsStringAsync();

        // Both 200 (success) and 401 (bad credentials) return a LoginResponse JSON body.
        // Any other status (e.g. 500) returns a raw error — surface it so the user sees
        // something meaningful instead of a generic "Invalid credentials" message.
        if (resp.StatusCode == System.Net.HttpStatusCode.OK ||
            resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            try { return JsonSerializer.Deserialize<LoginResponse>(body, _json); }
            catch { /* fall through */ }
        }

        _logger.LogError("Login API returned {Status}: {Body}", (int)resp.StatusCode,
            body.Length > 400 ? body[..400] : body);

        return new LoginResponse
        {
            Success = false,
            Message = $"API error ({(int)resp.StatusCode}) — check server logs. " +
                      (body.Length > 200 ? body[..200] : body)
        };
    }

    public Task<LoginResponse?> RefreshAsync(string refreshToken) =>
        PostAsync<object, LoginResponse>(
            "api/auth/refresh", new { RefreshToken = refreshToken });

    public Task LogoutAsync(string refreshToken) =>
        PostVoidAsync("api/auth/logout", new { RefreshToken = refreshToken });

    // ?? User endpoints ????????????????????????????????????????????????????????

    public Task<List<UserDto>?> GetUsersAsync() =>
        GetAsync<List<UserDto>>("api/users");

    public Task<UserDto?> GetUserAsync(long id) =>
        GetAsync<UserDto>($"api/users/{id}");

    public Task<UserDto?> CreateUserAsync(object req) =>
        PostAsync<object, UserDto>("api/users", req);

    public Task<UserDto?> UpdateUserAsync(long id, object req) =>
        PutAsync<object, UserDto>($"api/users/{id}", req);

    public Task<bool> DeleteUserAsync(long id) =>
        DeleteAsync($"api/users/{id}");

    // ?? System logs (search) ??????????????????????????????????????????????????

    public Task<PagedResult<LogEventDto>?> GetLogsAsync(
        string? host, string? program, string? sourceIp,
        string? from, string? to, int page, int size,
        string? keyword = null, string? severity = null,
        string? facility = null, string? deviceType = null,
        string? sortOrder = "desc")
    {
        var qs = BuildQuery(
            ("hostname",   host),
            ("program",    program),
            ("sourceIp",   sourceIp),
            ("timeFrom",   from),
            ("timeTo",     to),
            ("keyword",    keyword),
            ("severity",   severity),
            ("facility",   facility),
            ("deviceType", deviceType),
            ("descending", sortOrder == "asc" ? "false" : "true"),
            ("page",       page.ToString()),
            ("pageSize",   size.ToString()));

        return GetAsync<PagedResult<LogEventDto>>($"api/logs/search{qs}");
    }

    // ?? Settings endpoints ????????????????????????????????????????????????????

    public Task<PlatformSettingsDto?> GetSettingsAsync() =>
        GetAsync<PlatformSettingsDto>("api/settings");

    public Task<PlatformSettingsDto?> UpdateSettingsAsync(object req) =>
        PutAsync<object, PlatformSettingsDto>("api/settings", req);

    public Task<StorageOverviewDto?> GetStorageOverviewAsync() =>
        GetAsync<StorageOverviewDto>("api/settings/storage");

    public Task<List<StoragePolicyDto>?> GetStoragePoliciesAsync() =>
        GetAsync<List<StoragePolicyDto>>("api/settings/storage/policies");

    // ?? Dashboard / analytics endpoints ??????????????????????????????????????

    public Task<DashboardSummaryDto?> GetDashboardSummaryAsync() =>
        GetAsync<DashboardSummaryDto>("api/logs/dashboard/summary");

    public Task<LogSourceSummaryDto?> GetSourcesSummaryAsync() =>
        GetAsync<LogSourceSummaryDto>("api/sources/summary");

    public Task<PagedResult<AlertEventDto>?> GetAlertEventsAsync(
        int page = 1, int pageSize = 10) =>
        GetAsync<PagedResult<AlertEventDto>>(
            $"api/alerts/events?page={page}&pageSize={pageSize}&acknowledged=false");

    public Task<List<TopTalkerDto>?> GetTopTalkersAsync(int topN = 5) =>
        GetAsync<List<TopTalkerDto>>($"api/logs/analytics/top-talkers?topN={topN}");

    public Task<List<EventVolumeDto>?> GetEventVolumeAsync(int hours = 24, int bucketMinutes = 60) =>
        GetAsync<List<EventVolumeDto>>($"api/logs/analytics/event-volume?hours={hours}&bucketMinutes={bucketMinutes}");

    // ?? Generic HTTP helpers ??????????????????????????????????????????????????

    private async Task<TOut?> GetAsync<TOut>(string url)
    {
        var resp = await _http.GetAsync(url);
        return await DeserializeAsync<TOut>(resp, "GET", url);
    }

    private async Task<TOut?> PostAsync<TIn, TOut>(string url, TIn body)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(url, content);
        return await DeserializeAsync<TOut>(resp, "POST", url);
    }

    private async Task PostVoidAsync<TIn>(string url, TIn body)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(url, content);
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("POST {Url} returned {Status}", url, (int)resp.StatusCode);
    }

    private async Task<TOut?> PutAsync<TIn, TOut>(string url, TIn body)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var resp = await _http.PutAsync(url, content);
        return await DeserializeAsync<TOut>(resp, "PUT", url);
    }

    private async Task<bool> DeleteAsync(string url)
    {
        var resp = await _http.DeleteAsync(url);
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("DELETE {Url} returned {Status}", url, (int)resp.StatusCode);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>
    /// Reads and deserializes only when the response is a success with JSON content.
    /// Returns default(TOut) and logs a warning for any non-success or non-JSON response.
    /// </summary>
    private async Task<TOut?> DeserializeAsync<TOut>(
        HttpResponseMessage resp, string method, string url)
    {
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "{Method} {Url} ? {Status}: {Body}",
                method, url, (int)resp.StatusCode,
                body.Length > 200 ? body[..200] + "�" : body);
            return default;
        }

        var mediaType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "{Method} {Url} ? {Status} but Content-Type is '{MediaType}': {Body}",
                method, url, (int)resp.StatusCode, mediaType,
                body.Length > 200 ? body[..200] + "�" : body);
            return default;
        }

        var json = await resp.Content.ReadAsStringAsync();
        try
        {
            return JsonSerializer.Deserialize<TOut>(json, _json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "{Method} {Url} ? JSON parse failed. Body: {Body}",
                method, url,
                json.Length > 300 ? json[..300] + "�" : json);
            return default;
        }
    }

    private static string BuildQuery(params (string key, string? val)[] pairs)
    {
        var parts = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.val))
            .Select(p => $"{Uri.EscapeDataString(p.key)}={Uri.EscapeDataString(p.val!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? "?" + qs : string.Empty;
    }
}
