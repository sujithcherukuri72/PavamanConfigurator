# ?? How to Add Your Custom Logo

## Option 1: Using an Image File (Recommended)

### Step 1: Prepare Your Logo
- Create a logo image (PNG, JPG, or SVG)
- Recommended size: 42x42 pixels or larger (square)
- Transparent background recommended for PNG
- Name it: `logo.png` (or any name you prefer)

### Step 2: Add to Assets Folder
1. Place your logo file in: `src/PavamanDroneConfigurator/Assets/`
2. Right-click the file in Visual Studio
3. Select Properties
4. Set **Build Action** to `AvaloniaResource`

### Step 3: Update MainWindow.axaml

Replace this code (around line 100):
```xml
<!-- Current SVG Logo -->
<Border Width="42" Height="42" 
        Background="White" 
        CornerRadius="21"
        Padding="6"
        BoxShadow="0 2 8 0 #40000000">
    <Viewbox>
        <Canvas Width="24" Height="24">
            <Path Fill="#4CAF50" Data="M12,2L4.5,20.29L5.21,21L12,18L18.79,21L19.5,20.29L12,2Z"/>
        </Canvas>
    </Viewbox>
</Border>
```

With this:
```xml
<!-- Your Custom Logo -->
<Border Width="42" Height="42" 
        Background="White" 
        CornerRadius="21"
        Padding="4"
        BoxShadow="0 2 8 0 #40000000">
    <Image Source="/Assets/logo.png" 
           Stretch="Uniform"/>
</Border>
```

### Step 4: Update SplashScreen.axaml

Replace the drone icon (around line 35):
```xml
<!-- Current Icon -->
<Border Width="120" Height="120" 
        Background="#4CAF50" 
        CornerRadius="60"
        BoxShadow="0 5 15 2 #40000000">
    <Viewbox Width="70" Height="70">
        <Canvas Width="24" Height="24">
            <Path Fill="White" Data="M12,2L4.5,20.29L5.21,21L12,18L18.79,21L19.5,20.29L12,2Z"/>
        </Canvas>
    </Viewbox>
</Border>
```

With:
```xml
<!-- Your Custom Logo -->
<Border Width="120" Height="120" 
        Background="#4CAF50" 
        CornerRadius="60"
        BoxShadow="0 5 15 2 #40000000"
        Padding="20">
    <Image Source="/Assets/logo.png" 
           Stretch="Uniform"/>
</Border>
```

### Step 5: Update ConnectionDialog.axaml

Replace the header icon (around line 30):
```xml
<!-- Current Icon -->
<Border Width="50" Height="50" 
        Background="White" 
        CornerRadius="25"
        Padding="8">
    <Viewbox>
        <Canvas Width="24" Height="24">
            <Path Fill="#4CAF50" Data="M12,2L4.5,20.29L5.21,21L12,18L18.79,21L19.5,20.29L12,2Z"/>
        </Canvas>
    </Viewbox>
</Border>
```

With:
```xml
<!-- Your Custom Logo -->
<Border Width="50" Height="50" 
        Background="White" 
        CornerRadius="25"
        Padding="6">
    <Image Source="/Assets/logo.png" 
           Stretch="Uniform"/>
</Border>
```

## Option 2: Using a Different SVG Icon

If you have an SVG path string for your logo:

### Example: Replace the drone SVG path

Current path:
```
M12,2L4.5,20.29L5.21,21L12,18L18.79,21L19.5,20.29L12,2Z
```

Replace with your SVG path in all three files:
- `MainWindow.axaml`
- `SplashScreen.axaml`
- `ConnectionDialog.axaml`

### Getting SVG Path from Icon Files:
1. Open your SVG file in a text editor
2. Look for the `<path d="...">` tag
3. Copy the `d` attribute value
4. Paste it in the `Data` property

Example:
```xml
<Path Fill="#4CAF50" Data="YOUR_SVG_PATH_HERE"/>
```

## Option 3: Using Material Design Icons

If you want to use Material Design icons:

### Step 1: Install Material.Icons.Avalonia
```bash
dotnet add package Material.Icons.Avalonia
```

### Step 2: Add namespace to your XAML
```xml
xmlns:icons="clr-namespace:Material.Icons.Avalonia;assembly=Material.Icons.Avalonia"
```

### Step 3: Use Material Icon
```xml
<Border Width="42" Height="42" 
        Background="White" 
        CornerRadius="21"
        Padding="6"
        BoxShadow="0 2 8 0 #40000000">
    <icons:MaterialIcon Kind="Helicopter" 
                        Foreground="#4CAF50"
                        Width="30" 
                        Height="30"/>
</Border>
```

Available drone-related icons:
- `Helicopter`
- `Quadcopter`
- `Airplane`
- `RadioTower`
- `Wifi`

## Option 4: Adding a Window Icon (Taskbar Icon)

### For Windows .ico file:

1. Create or convert your logo to .ico format
2. Name it: `app-icon.ico`
3. Place in: `src/PavamanDroneConfigurator/Assets/`
4. Set Build Action to `AvaloniaResource`

5. Add to MainWindow.axaml:
```xml
<Window ...
        Icon="/Assets/app-icon.ico">
```

6. Add to ConnectionDialog.axaml:
```xml
<Window ...
        Icon="/Assets/app-icon.ico">
```

## File Structure

After adding your logo, your Assets folder should look like:
```
src/PavamanDroneConfigurator/
??? Assets/
    ??? logo.png           (Your main logo)
    ??? app-icon.ico       (Optional: Window icon)
```

## Tips for Best Results

### Image Quality
- Use high-resolution images (at least 2x the display size)
- PNG with transparency works best
- Square images work better than rectangular

### Colors
- For logo on green background: Use white or light colors
- For logo on white background: Use green (#4CAF50) or dark colors
- Ensure good contrast

### Sizing
- **Header Logo**: 42x42px display size
- **Splash Screen**: 120x120px display size  
- **Connection Dialog**: 50x50px display size
- **Window Icon**: 256x256px source (will be scaled)

## Testing Your Logo

After making changes:

1. **Clean and rebuild**:
   ```bash
   dotnet clean
   dotnet build
   ```

2. **Run the app**:
   ```bash
   dotnet run --project src/PavamanDroneConfigurator/PavamanDroneConfigurator.csproj
   ```

3. **Check all locations**:
   - [ ] Splash screen logo
   - [ ] Main window header logo
   - [ ] Connection dialog header icon
   - [ ] Window taskbar icon (if added)

## Troubleshooting

**Logo not showing?**
- Check file path is correct: `/Assets/logo.png`
- Verify Build Action is set to `AvaloniaResource`
- Clean and rebuild the project
- Check image file is in the correct folder

**Logo too big/small?**
- Adjust the `Width` and `Height` properties
- Adjust the `Padding` on the Border
- Use `Stretch="Uniform"` to maintain aspect ratio

**Logo colors wrong?**
- If using SVG: Change the `Fill` color
- If using PNG: Edit the source image
- Consider using different logos for light/dark backgrounds

## Example: Complete Logo Setup

```xml
<!-- Main Window Header Logo -->
<Border Width="42" Height="42" 
        Background="White" 
        CornerRadius="21"
        Padding="4"
        BoxShadow="0 2 8 0 #40000000"
        ClipToBounds="True">
    <Image Source="/Assets/logo.png" 
           Stretch="Uniform"
           RenderOptions.BitmapInterpolationMode="HighQuality"/>
</Border>
```

This ensures:
- ? Circular container
- ? White background
- ? Shadow effect
- ? High-quality rendering
- ? Maintains aspect ratio
- ? Clips to border

## Need Help?

If you encounter issues:
1. Check the build output for errors
2. Verify file paths
3. Ensure Build Action is set correctly
4. Try rebuilding the project
5. Check image format is supported (PNG, JPG, BMP, SVG)
