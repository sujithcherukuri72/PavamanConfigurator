using Microsoft.Extensions.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Parses STATUSTEXT messages from Flight Controller during accelerometer calibration.
/// 
/// CRITICAL: This parser detects position requests, completion, and failure messages.
/// FC controls the calibration workflow entirely via STATUSTEXT.
/// </summary>
public class AccelStatusTextParser
{
    private readonly ILogger<AccelStatusTextParser> _logger;
    
    // Keywords for position detection (case-insensitive)
    private const string PLACE = "place";
    private const string LEVEL = "level";
    private const string LEFT = "left";
    private const string RIGHT = "right";
    private const string NOSE_DOWN = "nose down";
    private const string NOSE_UP = "nose up";
    private const string BACK = "back";
    private const string UPSIDE = "upside";
    
    // Keywords for completion detection
    private static readonly string[] CompletionKeywords =
    {
        "calibration successful",
        "calibration complete",
        "calibration done",
        "accel calibration successful",
        "accelerometer calibration successful",
        "accel cal complete",
        "accel offsets"
    };
    
    // Keywords for failure detection
    private static readonly string[] FailureKeywords =
    {
        "calibration failed",
        "calibration cancelled",
        "calibration timeout",
        "accel cal failed",
        "failed",
        "error"
    };
    
    // Keywords for sampling detection
    private static readonly string[] SamplingKeywords =
    {
        "sampling",
        "reading",
        "detected",
        "hold still"
    };
    
    public AccelStatusTextParser(ILogger<AccelStatusTextParser> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Parse STATUSTEXT message to detect position requests, completion, or failure.
    /// </summary>
    public StatusTextParseResult Parse(string statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
            return new StatusTextParseResult();
        
        var lowerText = statusText.ToLowerInvariant();
        
        // Check for completion FIRST (highest priority)
        if (IsCompletionMessage(lowerText))
        {
            _logger.LogInformation("Detected completion message: {Text}", statusText);
            return new StatusTextParseResult
            {
                IsSuccess = true,
                OriginalText = statusText
            };
        }
        
        // Check for failure
        if (IsFailureMessage(lowerText))
        {
            _logger.LogWarning("Detected failure message: {Text}", statusText);
            return new StatusTextParseResult
            {
                IsFailure = true,
                OriginalText = statusText
            };
        }
        
        // Check for position request
        var requestedPosition = DetectPositionRequest(lowerText);
        if (requestedPosition.HasValue)
        {
            _logger.LogInformation("Detected position request: position {Position} from text: {Text}", 
                requestedPosition.Value, statusText);
            
            return new StatusTextParseResult
            {
                IsPositionRequest = true,
                RequestedPosition = requestedPosition.Value,
                OriginalText = statusText
            };
        }
        
        // Check for sampling message
        if (IsSamplingMessage(lowerText))
        {
            _logger.LogDebug("Detected sampling message: {Text}", statusText);
            return new StatusTextParseResult
            {
                IsSampling = true,
                OriginalText = statusText
            };
        }
        
        // Unknown/informational message
        return new StatusTextParseResult
        {
            OriginalText = statusText
        };
    }
    
    private bool IsCompletionMessage(string lowerText)
    {
        return CompletionKeywords.Any(keyword => lowerText.Contains(keyword));
    }
    
    private bool IsFailureMessage(string lowerText)
    {
        // Check if contains failure keyword but NOT "not failed" or "didn't fail"
        if (FailureKeywords.Any(keyword => lowerText.Contains(keyword)))
        {
            return !lowerText.Contains("not failed") && !lowerText.Contains("didn't fail");
        }
        return false;
    }
    
    private bool IsSamplingMessage(string lowerText)
    {
        return SamplingKeywords.Any(keyword => lowerText.Contains(keyword));
    }
    
    /// <summary>
    /// Detect which position FC is requesting (1-6).
    /// Returns null if not a position request.
    /// </summary>
    private int? DetectPositionRequest(string lowerText)
    {
        // Must contain "place" to be a position request
        if (!lowerText.Contains(PLACE))
            return null;
        
        // Check positions in order of specificity (most specific first)
        
        // Position 4: NOSE DOWN (check before general "nose")
        if (lowerText.Contains(NOSE_DOWN) || 
            (lowerText.Contains("nose") && lowerText.Contains("down")))
        {
            return 4;
        }
        
        // Position 5: NOSE UP
        if (lowerText.Contains(NOSE_UP) || 
            (lowerText.Contains("nose") && lowerText.Contains("up")))
        {
            return 5;
        }
        
        // Position 2: LEFT (check it's not "left side" vs "right side")
        if (lowerText.Contains(LEFT) && !lowerText.Contains(RIGHT))
        {
            return 2;
        }
        
        // Position 3: RIGHT
        if (lowerText.Contains(RIGHT) && !lowerText.Contains(LEFT))
        {
            return 3;
        }
        
        // Position 6: BACK / UPSIDE DOWN
        if (lowerText.Contains(BACK) || lowerText.Contains(UPSIDE))
        {
            return 6;
        }
        
        // Position 1: LEVEL (check last, as it's most common word)
        if (lowerText.Contains(LEVEL))
        {
            return 1;
        }
        
        // Contains "place" but no recognized position keyword
        _logger.LogWarning("STATUSTEXT contains 'place' but no recognized position: {Text}", lowerText);
        return null;
    }
}

/// <summary>
/// Result of parsing a STATUSTEXT message.
/// </summary>
public class StatusTextParseResult
{
    /// <summary>FC is requesting a position</summary>
    public bool IsPositionRequest { get; set; }
    
    /// <summary>Position requested (1-6), if IsPositionRequest is true</summary>
    public int? RequestedPosition { get; set; }
    
    /// <summary>FC reported calibration success</summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>FC reported calibration failure</summary>
    public bool IsFailure { get; set; }
    
    /// <summary>FC is sampling position</summary>
    public bool IsSampling { get; set; }
    
    /// <summary>Original STATUSTEXT message</summary>
    public string OriginalText { get; set; } = "";
}
