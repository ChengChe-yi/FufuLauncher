/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using FufuLauncher.Models;
using FufuLauncher.Constants;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.Storage.Streams;
using CommunityToolkit.Mvvm.Messaging;

public class BackgroundItem
{
    public string Url { get; set; }
    public string PreviewUrl { get; set; }
    public bool IsVideo { get; set; }
    public string TypeText => IsVideo ? "视频" : "图片";
}

namespace FufuLauncher.Services.Background
{
    public class BackgroundRenderResult
    {
        public ImageSource ImageSource { get; set; }
        public MediaSource VideoSource { get; set; }
        public InMemoryRandomAccessStream VideoStream { get; set; }
        public bool IsVideo { get; set; }
    }

    public interface IBackgroundRenderer
    {
        Task<BackgroundRenderResult> GetBackgroundAsync(ServerType server, bool preferVideo);
        Task<BackgroundRenderResult> GetCustomBackgroundAsync(string filePath);
        Task<BackgroundRenderResult> GetSpecificOnlineBackgroundAsync(string url, bool isVideo);
        Task PreloadImageBackgroundsAsync(IEnumerable<string> imageUrls);
        Task CacheAllBackgroundsAsync(ServerType server);
        void ClearBackground();
        void ClearCustomBackground();
    }

    public class BackgroundRenderer : IBackgroundRenderer
    {
        private static readonly HttpClient _httpClient;

        private readonly IDevBuildDetectionService _devBuildDetectionService;

        private string _cacheFolderPath => Path.Combine(Helpers.AppPaths.CacheDir, "BackgroundCache");
        private BackgroundRenderResult _cachedBackground;
        private string _currentBackgroundUrl;
        private byte[] _videoBytes;
        private string _videoBytesUrl;
        private string _videoMimeType;
        private BackgroundRenderResult _cachedCustomBackground;
        private string _customBackgroundPath;
        
        private bool AllowVideoBackground => _devBuildDetectionService.IsDevBuild;

        static BackgroundRenderer()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");
        }

        public BackgroundRenderer(IDevBuildDetectionService devBuildDetectionService)
        {
            _devBuildDetectionService = devBuildDetectionService;
        }

        private static string GetApiLanguage()
        {
            var culture = ResourceExtensions.CurrentCulture;
            return string.IsNullOrEmpty(culture) ? "zh-cn" : culture.ToLowerInvariant();
        }

        public async Task<BackgroundRenderResult> GetBackgroundAsync(ServerType server, bool preferVideo)
        {
            try
            {
                preferVideo = preferVideo && AllowVideoBackground;

                var localSettings = App.GetService<ILocalSettingsService>();

                var specificUrlObj = await localSettings.ReadSettingAsync("SelectedOnlineBackgroundUrl");
                string specificUrl = specificUrlObj?.ToString();
                if (!string.IsNullOrEmpty(specificUrl))
                {
                    var isVideoObj = await localSettings.ReadSettingAsync("SelectedOnlineBackgroundIsVideo");
                    bool isVideo = isVideoObj != null && Convert.ToBoolean(isVideoObj);

                    if (isVideo && !AllowVideoBackground)
                    {
                        Debug.WriteLine("BackgroundRenderer: 非开发版，忽略已选中的视频背景");
                    }
                    else
                    {
                        var result = await LoadFromCacheOrNull(specificUrl, isVideo);
                        if (result != null)
                        {
                            ScheduleBackgroundRefresh(server, preferVideo);
                            return result;
                        }
                    }
                }

                var cachedResult = await TryLoadFromDiskCacheAsync(server, preferVideo);
                if (cachedResult != null)
                {
                    ScheduleBackgroundRefresh(server, preferVideo);
                    return cachedResult;
                }

                var freshResult = await FetchAndCacheAsync(server, preferVideo);
                if (freshResult != null)
                    return freshResult;

                return GetFallbackBackground();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BackgroundRenderer: GetBackgroundAsync 异常 - {ex.Message}");
                return GetFallbackBackground();
            }
        }

        private async Task<BackgroundRenderResult> TryLoadFromDiskCacheAsync(ServerType server, bool preferVideo)
        {
            var apiCachePath = GetApiCachePath(server);
            if (!File.Exists(apiCachePath))
                return null;

            try
            {
                var cachedJson = await File.ReadAllTextAsync(apiCachePath);
                var bgInfo = ParseTargetBackground(cachedJson, preferVideo);
                if (bgInfo == null)
                    return null;

                return await LoadFromCacheOrNull(bgInfo.Url, bgInfo.IsVideo);
            }
            catch
            {
                return null;
            }
        }

        private async Task<BackgroundRenderResult> LoadFromCacheOrNull(string url, bool isVideo)
        {
            if (!isVideo && url == _currentBackgroundUrl && _cachedBackground != null)
                return _cachedBackground;

            if (isVideo)
            {
                var cached = await LoadVideoFromMemoryOrNull(url);
                if (cached != null)
                    return cached;
            }

            var cachedFilePath = FindCachedFilePath(url, isVideo ? GetVideoExtension(url) : ".img");
            if (cachedFilePath == null || new FileInfo(cachedFilePath).Length <= 1024)
                return null;

            try
            {
                BackgroundRenderResult result;
                if (isVideo)
                {
                    result = await LoadVideoIntoMemoryAsync(url, cachedFilePath);
                }
                else
                {
                    var bitmap = new BitmapImage(new Uri(cachedFilePath));
                    result = new BackgroundRenderResult { ImageSource = bitmap, IsVideo = false };
                }

                _cachedBackground = result;
                _currentBackgroundUrl = url;
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BackgroundRenderer: 缓存文件加载失败({cachedFilePath}): {ex.Message}");
                try { File.Delete(cachedFilePath); } catch { }
                return null;
            }
        }

        private async Task<BackgroundRenderResult> LoadVideoFromMemoryOrNull(string url)
        {
            if (_videoBytes == null || !string.Equals(_videoBytesUrl, url, StringComparison.Ordinal))
                return null;

            try
            {
                var mediaSource = await CreateMediaSourceFromBytesAsync(_videoBytes, _videoMimeType);
                if (mediaSource == null)
                    return null;

                return new BackgroundRenderResult
                {
                    VideoSource = mediaSource,
                    VideoStream = null,
                    IsVideo = true
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BackgroundRenderer: 内存视频缓存加载失败({url}): {ex.Message}");
                ReleaseVideoCache();
                return null;
            }
        }

        private async Task<BackgroundRenderResult> LoadVideoIntoMemoryAsync(string url, string filePath)
        {
            try
            {
                var mimeType = GetMimeType(filePath);
                var bytes = await File.ReadAllBytesAsync(filePath);
                var mediaSource = await CreateMediaSourceFromBytesAsync(bytes, mimeType);
                if (mediaSource == null)
                    return null;

                _videoBytes = bytes;
                _videoBytesUrl = url;
                _videoMimeType = mimeType;

                return new BackgroundRenderResult { VideoSource = mediaSource, VideoStream = null, IsVideo = true };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BackgroundRenderer: 视频载入内存失败({filePath}): {ex.Message}");
                ReleaseVideoCache();
                return null;
            }
        }

        private static async Task<MediaSource> CreateMediaSourceFromBytesAsync(byte[] bytes, string contentType)
        {
            try
            {
                var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);
                return MediaSource.CreateFromStream(stream, contentType);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BackgroundRenderer: 从内存创建视频源失败: {ex.Message}");
                return null;
            }
        }

        private static string GetMimeType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".webm" => "video/webm",
                ".mp4" => "video/mp4",
                ".mkv" => "video/x-matroska",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                _ => "video/mp4"
            };
        }

        private void ReleaseVideoCache()
        {
            _videoBytes = null;
            _videoBytesUrl = null;
            _videoMimeType = null;
        }

        private string? FindCachedFilePath(string url, string defaultExtension)
        {
            var predictedPath = Path.Combine(_cacheFolderPath, GetCacheFileName(url, defaultExtension));
            if (File.Exists(predictedPath))
                return predictedPath;
            
            try
            {
                if (Directory.Exists(_cacheFolderPath))
                {
                    var hash = ComputeUrlHash(url);
                    foreach (var file in Directory.GetFiles(_cacheFolderPath, hash + ".*"))
                    {
                        if (!file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                            return file;
                    }
                }
            }
            catch { }

            return null;
        }

        private async Task<string> ResolveBackgroundApiUrlAsync(ServerType server)
        {
            var localSettings = App.GetService<ILocalSettingsService>();
            var customApiObj = await localSettings.ReadSettingAsync("CustomBackgroundApiUrl");
            var customApi = customApiObj?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(customApi) && Uri.TryCreate(customApi, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return customApi;
            }

            return server switch
            {
                ServerType.CN => ApiEndpoints.BackgroundCnApi,
                ServerType.OS => ApiEndpoints.BackgroundOsApi.Replace("language=zh-cn", $"language={GetApiLanguage()}"),
                _ => ApiEndpoints.BackgroundCnApi
            };
        }

        private async Task<BackgroundRenderResult> FetchAndCacheAsync(ServerType server, bool preferVideo)
        {
            try
            {
                var apiUrl = await ResolveBackgroundApiUrlAsync(server);

                var response = await _httpClient.GetStringAsync(apiUrl);
                await SaveApiCacheAsync(server, response);

                var localSettings = App.GetService<ILocalSettingsService>();
                var currentHash = ComputeMD5(response);
                var savedHashObj = await localSettings.ReadSettingAsync("BackgroundJsonHash");
                string savedHash = savedHashObj?.ToString();

                if (!string.IsNullOrEmpty(savedHash) && savedHash != currentHash)
                {
                    await localSettings.SaveSettingAsync("SelectedOnlineBackgroundUrl", "");
                    await localSettings.SaveSettingAsync("SelectedOnlineBackgroundIsVideo", false);
                }
                await localSettings.SaveSettingAsync("BackgroundJsonHash", currentHash);

                var specificUrlObj = await localSettings.ReadSettingAsync("SelectedOnlineBackgroundUrl");
                string specificUrl = specificUrlObj?.ToString();
                if (!string.IsNullOrEmpty(specificUrl))
                {
                    var isVideoObj = await localSettings.ReadSettingAsync("SelectedOnlineBackgroundIsVideo");
                    bool isVideo = isVideoObj != null && Convert.ToBoolean(isVideoObj);

                    if (isVideo && !AllowVideoBackground)
                    {
                        Debug.WriteLine("BackgroundRenderer: 忽略已选中的视频背景");
                    }
                    else
                    {
                        await DownloadToCache(specificUrl, isVideo ? GetVideoExtension(specificUrl) : ".img");
                        return await LoadFromCacheOrNull(specificUrl, isVideo);
                    }
                }

                var bgInfo = ParseTargetBackground(response, preferVideo);
                if (bgInfo == null)
                    return null;

                await DownloadToCache(bgInfo.Url, bgInfo.IsVideo ? GetVideoExtension(bgInfo.Url) : ".img");
                var result = await LoadFromCacheOrNull(bgInfo.Url, bgInfo.IsVideo);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await PreloadAllFromResponse(response);
                        CleanupStaleCacheFiles(response);
                    }
                    catch { }
                });

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BackgroundRenderer: FetchAndCacheAsync 异常 - {ex.Message}");
                return null;
            }
        }

        private void ScheduleBackgroundRefresh(ServerType server, bool preferVideo)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var apiUrl = await ResolveBackgroundApiUrlAsync(server);

                    var response = await _httpClient.GetStringAsync(apiUrl);
                    var newHash = ComputeMD5(response);

                    var apiCachePath = GetApiCachePath(server);
                    string oldHash = null;
                    if (File.Exists(apiCachePath))
                    {
                        try
                        {
                            var oldJson = await File.ReadAllTextAsync(apiCachePath);
                            oldHash = ComputeMD5(oldJson);
                        }
                        catch { }
                    }

                    await SaveApiCacheAsync(server, response);

                    var localSettings = App.GetService<ILocalSettingsService>();
                    var savedHashObj = await localSettings.ReadSettingAsync("BackgroundJsonHash");
                    string savedHash = savedHashObj?.ToString();

                    if (!string.IsNullOrEmpty(savedHash) && savedHash != newHash)
                    {
                        await localSettings.SaveSettingAsync("SelectedOnlineBackgroundUrl", "");
                        await localSettings.SaveSettingAsync("SelectedOnlineBackgroundIsVideo", false);
                    }
                    await localSettings.SaveSettingAsync("BackgroundJsonHash", newHash);

                    bool dataChanged = oldHash != null && oldHash != newHash;

                    var bgInfo = ParseTargetBackground(response, preferVideo);
                    if (bgInfo != null)
                    {
                        await DownloadToCache(bgInfo.Url, bgInfo.IsVideo ? GetVideoExtension(bgInfo.Url) : ".img");
                    }

                    await PreloadAllFromResponse(response);
                    CleanupStaleCacheFiles(response);

                    if (dataChanged)
                    {
                        _cachedBackground = null;
                        _currentBackgroundUrl = null;
                        WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BackgroundRenderer: 后台刷新异常 - {ex.Message}");
                }
            });
        }

        private BackgroundUrlInfo ParseTargetBackground(string apiResponse, bool preferVideo)
        {
            try
            {
                preferVideo = preferVideo && AllowVideoBackground;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                var result = JsonSerializer.Deserialize<HoyoverseBackgroundResponse>(apiResponse, options);
                if (result?.Retcode != 0 || result.Data?.GameInfoList == null || result.Data.GameInfoList.Length == 0)
                    return null;

                var backgrounds = result.Data.GameInfoList[0].Backgrounds;
                if (backgrounds == null || backgrounds.Length == 0)
                    return null;

                if (preferVideo)
                {
                    var videoBg = backgrounds.FirstOrDefault(b =>
                        b.Type == "BACKGROUND_TYPE_VIDEO" && !string.IsNullOrEmpty(b.Video?.Url));
                    if (videoBg != null)
                        return new BackgroundUrlInfo { Url = videoBg.Video.Url, IsVideo = true };
                }

                var staticBg = backgrounds.FirstOrDefault(b =>
                    b.Type != "BACKGROUND_TYPE_VIDEO" && !string.IsNullOrEmpty(b.Background?.Url));
                if (staticBg != null)
                    return new BackgroundUrlInfo { Url = staticBg.Background.Url, IsVideo = false };

                return null;
            }
            catch
            {
                return null;
            }
        }

        private List<string> ParseAllUrls(string apiResponse, bool includeVideos = true)
        {
            var urls = new List<string>();
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                var result = JsonSerializer.Deserialize<HoyoverseBackgroundResponse>(apiResponse, options);
                if (result?.Retcode != 0 || result.Data?.GameInfoList == null || result.Data.GameInfoList.Length == 0)
                    return urls;

                var backgrounds = result.Data.GameInfoList[0].Backgrounds;
                if (backgrounds == null)
                    return urls;

                foreach (var b in backgrounds)
                {
                    if (includeVideos && b.Type == "BACKGROUND_TYPE_VIDEO" && !string.IsNullOrEmpty(b.Video?.Url))
                        urls.Add(b.Video.Url);
                    if (!string.IsNullOrEmpty(b.Background?.Url))
                        urls.Add(b.Background.Url);
                }
            }
            catch { }
            return urls;
        }

        private async Task PreloadAllFromResponse(string apiResponse)
        {
            var allUrls = ParseAllUrls(apiResponse, includeVideos: AllowVideoBackground);
            foreach (var url in allUrls)
            {
                try
                {
                    var urlExt = GetUrlPathExtension(url);
                    var ext = urlExt != null && VideoExtensions.Contains(urlExt) ? urlExt : ".img";
                    await DownloadToCache(url, ext);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BackgroundRenderer: 预缓存失败({url}): {ex.Message}");
                }
            }
        }

        private void CleanupStaleCacheFiles(string apiResponse)
        {
            try
            {
                if (!Directory.Exists(_cacheFolderPath))
                    return;
                
                var validHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allUrls = ParseAllUrls(apiResponse, includeVideos: AllowVideoBackground);
                foreach (var url in allUrls)
                {
                    validHashes.Add(ComputeUrlHash(url));
                }

                foreach (var file in Directory.GetFiles(_cacheFolderPath))
                {
                    var name = Path.GetFileName(file);
                    if (name is "api_cn.json" or "api_os.json")
                        continue;
                    if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(file); } catch { }
                        continue;
                    }
                    if (!validHashes.Contains(Path.GetFileNameWithoutExtension(name)))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        public async Task<BackgroundRenderResult> GetSpecificOnlineBackgroundAsync(string url, bool isVideo)
        {
            if (isVideo && !AllowVideoBackground)
            {
                Debug.WriteLine("BackgroundRenderer: 非开发版，拒绝加载指定视频背景");
                return null;
            }

            try
            {
                var ext = isVideo ? GetVideoExtension(url) : ".img";
                await DownloadToCache(url, ext);
                var result = await LoadFromCacheOrNull(url, isVideo);
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BackgroundRenderer: 指定背景加载失败 - {ex.Message}");
                return null;
            }
        }

        public async Task<BackgroundRenderResult> GetCustomBackgroundAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            if (_cachedCustomBackground != null && filePath == _customBackgroundPath)
                return _cachedCustomBackground;

            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var isVideo = extension is ".mp4" or ".webm" or ".mkv" or ".avi" or ".mov";

                if (isVideo && !AllowVideoBackground)
                {
                    Debug.WriteLine($"BackgroundRenderer: 非开发版，禁止使用视频自定义背景: {filePath}");
                    return null;
                }

                BackgroundRenderResult result;
                if (isVideo)
                {
                    var videoSource = MediaSource.CreateFromUri(new Uri(filePath));
                    result = new BackgroundRenderResult { VideoSource = videoSource, IsVideo = true };
                }
                else
                {
                    var bitmap = new BitmapImage(new Uri(filePath));
                    result = new BackgroundRenderResult { ImageSource = bitmap, IsVideo = false };
                }

                _cachedCustomBackground = result;
                _customBackgroundPath = filePath;
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BackgroundRenderer: 自定义背景加载失败 - {ex.Message}");
                return null;
            }
        }

        public async Task PreloadImageBackgroundsAsync(IEnumerable<string> imageUrls)
        {
            foreach (var url in imageUrls)
            {
                try
                {
                    await DownloadToCache(url, ".img");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BackgroundRenderer: 预加载失败({url}): {ex.Message}");
                }
            }
        }

        public async Task CacheAllBackgroundsAsync(ServerType server)
        {
            try
            {
                var apiCachePath = GetApiCachePath(server);
                if (!File.Exists(apiCachePath))
                    return;

                var json = await File.ReadAllTextAsync(apiCachePath);
                await PreloadAllFromResponse(json);
                CleanupStaleCacheFiles(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BackgroundRenderer: CacheAllBackgroundsAsync 异常 - {ex.Message}");
            }
        }

        public void ClearBackground()
        {
            if (Directory.Exists(_cacheFolderPath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_cacheFolderPath))
                        File.Delete(file);
                }
                catch { }
            }

            _cachedBackground = null;
            _currentBackgroundUrl = null;
            ReleaseVideoCache();
        }

        public void ClearCustomBackground()
        {
            _customBackgroundPath = null;
            _cachedCustomBackground = null;
            ReleaseVideoCache();
        }

        private async Task DownloadToCache(string url, string defaultExtension)
        {
            var urlExt = GetUrlPathExtension(url);
            var isVideoDownload = (urlExt != null && VideoExtensions.Contains(urlExt)) ||
                                  (urlExt == null && VideoExtensions.Contains(defaultExtension));
            if (isVideoDownload && !AllowVideoBackground)
            {
                Debug.WriteLine($"BackgroundRenderer: 非开发版，跳过视频背景下载: {url}");
                return;
            }

            var fileName = GetCacheFileName(url, defaultExtension);
            var cachedFilePath = Path.Combine(_cacheFolderPath, fileName);

            if (File.Exists(cachedFilePath) && new FileInfo(cachedFilePath).Length > 1024)
                return;

            Directory.CreateDirectory(_cacheFolderPath);

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            if (GetUrlPathExtension(url) == null)
            {
                var detected = GetExtensionFromContentType(response.Content.Headers.ContentType?.MediaType, defaultExtension);
                if (!string.Equals(detected, defaultExtension, StringComparison.OrdinalIgnoreCase))
                {
                    fileName = GetCacheFileName(url, detected);
                    cachedFilePath = Path.Combine(_cacheFolderPath, fileName);
                    if (File.Exists(cachedFilePath) && new FileInfo(cachedFilePath).Length > 1024)
                        return;
                }
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            var tempFile = Path.Combine(_cacheFolderPath, $"{fileName}.tmp");
            await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                await contentStream.CopyToAsync(fileStream);
            }
            File.Move(tempFile, cachedFilePath, true);
        }

        private static string GetExtensionFromContentType(string? mediaType, string defaultExtension)
        {
            if (string.IsNullOrEmpty(mediaType))
                return defaultExtension;

            var mime = mediaType.ToLowerInvariant();
            if (mime.Contains("webm")) return ".webm";
            if (mime.Contains("mp4")) return ".mp4";
            if (mime.Contains("matroska")) return ".mkv";
            if (mime.Contains("quicktime")) return ".mov";
            if (mime.StartsWith("image/")) return ".img";
            return defaultExtension;
        }

        private string GetApiCachePath(ServerType server)
        {
            var name = server == ServerType.OS ? "api_os.json" : "api_cn.json";
            return Path.Combine(_cacheFolderPath, name);
        }

        private async Task SaveApiCacheAsync(ServerType server, string json)
        {
            Directory.CreateDirectory(_cacheFolderPath);
            await File.WriteAllTextAsync(GetApiCachePath(server), json);
        }

        private static readonly string[] VideoExtensions = { ".webm", ".mp4", ".mkv", ".avi", ".mov" };

        private static string ComputeUrlHash(string url)
        {
            var bytes = Encoding.UTF8.GetBytes(url);
            var hash = MD5.HashData(bytes);
            return Convert.ToHexString(hash).ToLower();
        }

        private static string? GetUrlPathExtension(string url)
        {
            try
            {
                var ext = Path.GetExtension(new Uri(url).AbsolutePath)?.ToLowerInvariant();
                return string.IsNullOrEmpty(ext) ? null : ext;
            }
            catch
            {
                return null;
            }
        }
        
        private static string GetVideoExtension(string url)
        {
            var ext = GetUrlPathExtension(url);
            return ext != null && VideoExtensions.Contains(ext) ? ext : ".mp4";
        }

        private string GetCacheFileName(string url, string defaultExtension = ".mp4")
        {
            var extension = GetUrlPathExtension(url) ?? defaultExtension;
            return ComputeUrlHash(url) + extension;
        }

        private static string ComputeMD5(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = MD5.HashData(bytes);
            return Convert.ToHexString(hash).ToLower();
        }

        private BackgroundRenderResult GetFallbackBackground()
        {
            try
            {
                var bgPath = Path.Combine(AppContext.BaseDirectory, "Assets", "bg.png");
                if (!File.Exists(bgPath))
                    return null;

                var bitmap = new BitmapImage(new Uri(bgPath));
                return new BackgroundRenderResult { ImageSource = bitmap, IsVideo = false };
            }
            catch
            {
                return null;
            }
        }
    }
}

