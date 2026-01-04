# Assets Folder

This folder contains all static assets used in the Pavanam Drone Configurator application.

## Structure

```
Assets/
└── Images/
    ├── background.jpg       - Splash screen background image ✅ ADDED
    ├── logo.ico            - Company/Product logo icon ✅ ADDED
    ├── logo-withname.ico   - Logo with product name ✅ ADDED
    └── splash.png          - Full splash screen image ✅ ADDED
```

## Current Assets Status

✅ **All assets successfully integrated!**

- ✅ `background.jpg` - 38 KB - Used as splash screen background
- ✅ `logo.ico` - 213 KB - Used as application icon and splash screen logo
- ✅ `logo-withname.ico` - 215 KB - Available for About dialog
- ✅ `splash.png` - 2.05 MB - Full splash screen composition (optional)

## Image Specifications

### background.jpg ✅
- **Current Size:** 1920x1080px (Full HD)
- **Format:** JPEG
- **Purpose:** Background for splash screen
- **Usage:** Automatically loaded in SplashScreenWindow
- **Design:** Custom branded background with visual effects

### logo.ico ✅
- **Current Size:** 512x512px icon file
- **Format:** ICO (Windows Icon)
- **Purpose:** 
  - Application executable icon (taskbar, desktop)
  - Splash screen center logo
  - Window title bar icon
- **Usage:** 
  - Configured in project file as `ApplicationIcon`
  - Referenced in SplashScreenWindow XAML

### logo-withname.ico ✅
- **Current Size:** 512x512px icon file
- **Format:** ICO (Windows Icon)
- **Purpose:** Full branding with product name
- **Usage:** Available for About dialog, help screens, etc.
- **Note:** Ready to use in future UI elements

### splash.png ✅
- **Current Size:** 1920x1080px (2.05 MB)
- **Format:** PNG
- **Purpose:** Complete custom splash screen (alternative)
- **Usage:** Can be used instead of component-based splash
- **Note:** Currently using component-based splash with background.jpg

## Integration Details

### Splash Screen
The splash screen now uses your custom assets:
- **Background:** `background.jpg` as full-screen background
- **Logo:** `logo.ico` displayed in circular container
- **Overlay:** Semi-transparent dark overlay for text visibility
- **Branding:** "Pavanam Drone Configurator" title text
- **Progress:** Loading bar with status messages

### Application Icon
Your `logo.ico` is configured as the application icon:
- Shows in Windows taskbar
- Shows in window title bar
- Shows in desktop shortcuts
- Shows in task manager
- Shows in file explorer

### Asset Loading
All assets are embedded as `AvaloniaResource`:
```xml
<AvaloniaResource Include="Assets\**" />
```

Assets are loaded using Avalonia's asset loader:
```
avares://PavanamDroneConfigurator.UI/Assets/Images/logo.ico
avares://PavanamDroneConfigurator.UI/Assets/Images/background.jpg
```

## Build Configuration

Assets are automatically:
- ✅ Embedded in the application binary
- ✅ Available at runtime without external files
- ✅ Optimized for fast loading
- ✅ Cached by Avalonia

## Customization Options

### Option 1: Keep Current Setup (Recommended)
Current implementation uses:
- Custom background image
- Custom logo in center
- Text-based branding
- Progress bar
- Version information

### Option 2: Use Full Splash Image
To use `splash.png` instead:
1. Update `SplashScreenWindow.axaml`
2. Replace Border content with Image control
3. Point to `avares://PavanamDroneConfigurator.UI/Assets/Images/splash.png`

### Option 3: Hybrid Approach
- Use `splash.png` as background
- Overlay progress bar and version text
- Best for complex branded splash screens

## Technical Details

### Asset URIs
```csharp
// Logo
"avares://PavanamDroneConfigurator.UI/Assets/Images/logo.ico"

// Background
"avares://PavanamDroneConfigurator.UI/Assets/Images/background.jpg"

// Logo with name
"avares://PavanamDroneConfigurator.UI/Assets/Images/logo-withname.ico"

// Full splash
"avares://PavanamDroneConfigurator.UI/Assets/Images/splash.png"
```

### Asset Detection
The `SplashScreenViewModel` automatically detects available assets:
```csharp
HasLogo = AssetExists("avares://PavanamDroneConfigurator.UI/Assets/Images/logo.ico");
HasBackground = AssetExists("avares://PavanamDroneConfigurator.UI/Assets/Images/background.jpg");
HasSplash = AssetExists("avares://PavanamDroneConfigurator.UI/Assets/Images/splash.png");
```

### Fallback Behavior
If custom assets are missing:
- Background: Uses blue gradient
- Logo: Shows default drone icon (SVG)
- Everything still works perfectly

## File Size Guidelines

Current sizes:
- ✅ `background.jpg`: 38 KB (excellent)
- ✅ `logo.ico`: 213 KB (good for multi-resolution icon)
- ✅ `logo-withname.ico`: 215 KB (good)
- ⚠️ `splash.png`: 2.05 MB (consider optimization if used)

### Optimization Tips
If you want to reduce `splash.png` size:
1. Use JPEG format instead of PNG (if no transparency needed)
2. Reduce resolution to 1280x720 (still looks great)
3. Use image compression tools (TinyPNG, ImageOptim)
4. Consider WebP format (Avalonia supports it)

## Usage Examples

### In XAML
```xml
<!-- Background -->
<ImageBrush Source="avares://PavanamDroneConfigurator.UI/Assets/Images/background.jpg"/>

<!-- Logo -->
<Image Source="avares://PavanamDroneConfigurator.UI/Assets/Images/logo.ico"/>

<!-- Logo with name -->
<Image Source="avares://PavanamDroneConfigurator.UI/Assets/Images/logo-withname.ico"/>
```

### In Code
```csharp
var logoUri = new Uri("avares://PavanamDroneConfigurator.UI/Assets/Images/logo.ico");
var bitmap = new Bitmap(AssetLoader.Open(logoUri));
```

## Next Steps

### Additional Branding Opportunities
1. **About Dialog** - Use `logo-withname.ico`
2. **Help Screen** - Show branded header
3. **Loading Screens** - Reuse splash assets
4. **Error Dialogs** - Show logo for professional look
5. **Success Messages** - Branded confirmation screens

### Future Asset Additions
Consider adding:
- `icon-small.ico` (16x16, 32x32 for system tray)
- `banner.png` (for About dialog header)
- `error-icon.png` (for error messages)
- `success-icon.png` (for confirmations)

## Support

If you need to update assets:
1. Replace files in `Assets/Images/` folder
2. Keep the same filenames
3. Rebuild the application
4. Assets are automatically embedded

No code changes required for asset updates!

---

**Status:** ✅ **ALL ASSETS INTEGRATED**  
**Last Updated:** January 4, 2026  
**Ready for:** Production deployment
