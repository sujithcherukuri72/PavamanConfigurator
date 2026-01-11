using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Downloads ArduPilot parameter metadata XML files from official GitHub repository.
/// Provides caching and fallback mechanisms for offline operation.
/// Supports Copter and Plane vehicle types.
/// </summary>
public class ArduPilotMetadataDownloader
{
    private readonly ILogger<ArduPilotMetadataDownloader> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;

    private const string GITHUB_RAW_BASE_URL = "https://raw.githubusercontent.com/ArduPilot/ardupilot/master/Tools/autotest/param_metadata/";

    private static readonly Dictionary<VehicleType, string> XmlFiles = new()
    {
        [VehicleType.Copter] = "apm.pdef.xml",
        [VehicleType.Plane] = "ArduPlane.pdef.xml",
        [VehicleType.Rover] = "APMrover2.pdef.xml",
        [VehicleType.Sub] = "ArduSub.pdef.xml",
        [VehicleType.Tracker] = "AntennaTracker.pdef.xml"
    };

    public ArduPilotMetadataDownloader(ILogger<ArduPilotMetadataDownloader> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Set up cache directory in AppData
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(appData, "PavamanDroneConfigurator", "ParamCache");
        
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            _logger.LogInformation("Parameter cache directory: {CacheDir}", _cacheDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create cache directory");
        }
    }

    /// <summary>
    /// Downloads parameter XML for specified vehicle type from GitHub.
    /// Uses cached version if download fails.
    /// </summary>
    public async Task<string?> DownloadXmlAsync(VehicleType vehicleType)
    {
        if (!XmlFiles.TryGetValue(vehicleType, out var filename))
        {
            _logger.LogWarning("Unknown vehicle type: {VehicleType}, falling back to Copter", vehicleType);
            filename = XmlFiles[VehicleType.Copter];
            vehicleType = VehicleType.Copter;
        }

        var cacheFile = GetCacheFilePath(vehicleType);

        try
        {
            // Try to download latest version
            var url = GITHUB_RAW_BASE_URL + filename;
            _logger.LogInformation("Downloading parameter metadata from {Url}", url);

            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead);
            response.EnsureSuccessStatusCode();

            var xmlContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(xmlContent))
            {
                _logger.LogWarning("Downloaded empty XML content");
                return await LoadFromCacheAsync(vehicleType);
            }

            // Save to cache
            try
            {
                await File.WriteAllTextAsync(cacheFile, xmlContent);
                _logger.LogInformation("Downloaded and cached {Filename} ({Size} bytes)", filename, xmlContent.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache downloaded XML");
            }

            return xmlContent;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error downloading parameter metadata");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout downloading parameter metadata");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download parameter metadata from GitHub");
        }

        // Try to load from cache
        return await LoadFromCacheAsync(vehicleType);
    }

    /// <summary>
    /// Loads parameter XML from cache (if available).
    /// Returns null if not cached.
    /// </summary>
    public async Task<string?> LoadFromCacheAsync(VehicleType vehicleType)
    {
        var cacheFile = GetCacheFilePath(vehicleType);
        
        try
        {
            if (File.Exists(cacheFile))
            {
                _logger.LogInformation("Loading from cache: {CacheFile}", cacheFile);
                return await File.ReadAllTextAsync(cacheFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read from cache");
        }

        _logger.LogDebug("No cache found for {VehicleType}", vehicleType);
        return null;
    }

    /// <summary>
    /// Checks if cached version exists and returns its age.
    /// </summary>
    public TimeSpan? GetCacheAge(VehicleType vehicleType)
    {
        var cacheFile = GetCacheFilePath(vehicleType);
        
        try
        {
            if (File.Exists(cacheFile))
            {
                var fileInfo = new FileInfo(cacheFile);
                return DateTime.UtcNow - fileInfo.LastWriteTimeUtc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cache age");
        }

        return null;
    }

    /// <summary>
    /// Clears all cached parameter files.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                foreach (var file in Directory.GetFiles(_cacheDirectory, "*.xml"))
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted cache file: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
        }
    }

    private string GetCacheFilePath(VehicleType vehicleType)
    {
        if (XmlFiles.TryGetValue(vehicleType, out var filename))
        {
            return Path.Combine(_cacheDirectory, filename);
        }

        return Path.Combine(_cacheDirectory, $"{vehicleType}.pdef.xml");
    }
}
