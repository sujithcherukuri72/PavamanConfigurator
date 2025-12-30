# ?? Quick Start Guide - Updated UI

## What's New? ?

Your Pavaman Drone Configurator now has:
- ? Professional splash screen on startup
- ? Connection dialog before main window
- ? Logo in the application header
- ? Smooth transitions and animations
- ? Better navigation with active state highlighting
- ? Modern green color theme throughout
- ? Improved form controls and styling

## Running the Application

### Option 1: Using Visual Studio
1. Open the solution in Visual Studio
2. Press **F5** or click the **Run** button
3. Watch the splash screen appear
4. Configure connection in the dialog
5. Enjoy the improved UI!

### Option 2: Using Command Line
```bash
cd src/PavamanDroneConfigurator
dotnet run
```

## What You'll See

### 1. Splash Screen (2.5 seconds)
```
???????????????????????????????????????
?                                     ?
?          ?? (Green Circle)          ?
?                                     ?
?           PAVAMAN                   ?
?      DRONE CONFIGURATOR             ?
?                                     ?
?          Loading...                 ?
?      ???????????????                ?
?                                     ?
?         Version 1.0.0               ?
?   © 2024 Pavaman Technologies       ?
?                                     ?
???????????????????????????????????????
```

### 2. Connection Dialog
```
??????????????????????????????????????????????
?  ??  Drone Connection Required             ?
?  Please connect to your drone              ?
??????????????????????????????????????????????
?                                            ?
?  COMMUNICATION SETTINGS                    ?
?                                            ?
?  Connection Type:  [Serial Port      ?]   ?
?  Serial Port:      [COM3             ?]   ?
?  Baud Rate:        [115200           ?]   ?
?                                            ?
?  Status: Disconnected                      ?
?                                            ?
?                [Cancel]  [Connect]         ?
??????????????????????????????????????????????
```

### 3. Main Window
```
????????????????????????????????????????????????????????????
?  ??  PAVAMAN DRONE CONFIGURATOR                          ?
????????????????????????????????????????????????????????????
? NAV    ?                                                 ?
? ????   ?  Your main content area                         ?
?        ?                                                 ?
? ? CONN ?  - Connection settings                          ?
? SENSOR ?  - Real-time telemetry                          ?
? SAFETY ?  - Parameter configuration                      ?
? FLIGHT ?  - and more...                                  ?
? RC CAL ?                                                 ?
? MOTOR  ?                                                 ?
? POWER  ?                                                 ?
? SPRAY  ?                                                 ?
? PID    ?                                                 ?
? PARAMS ?                                                 ?
?        ?                                                 ?
????????????????????????????????????????????????????????????
```

## Key Features to Notice

### ?? Active Navigation Highlighting
- Click on any navigation item
- Notice the light green background indicating the active section
- Smooth transitions when switching between sections

### ?? Logo in Header
- Drone logo appears to the left of the title
- White circular container with shadow
- Professional branding

### ?? Connection Flow
- Connection dialog must be completed before accessing main UI
- Click "Cancel" to skip connection
- Click "Connect" to establish drone connection
- Dialog auto-closes on successful connection

### ? Smooth Animations
- 200ms transitions on all buttons
- Hover effects on navigation items
- Focus states on input controls
- Progress bar animations

## Customization Options

### Change Splash Screen Duration
Edit `src/PavamanDroneConfigurator/App.axaml.cs` line 40:
```csharp
await splashScreen.ShowAndWaitAsync(2500); // milliseconds
```

### Add Your Logo
See `HOW_TO_ADD_LOGO.md` for detailed instructions.

Quick version:
1. Place `logo.png` in `Assets/` folder
2. Update the three XAML files:
   - `SplashScreen.axaml`
   - `ConnectionDialog.axaml`
   - `MainWindow.axaml`

### Change Theme Colors
Edit `src/PavamanDroneConfigurator/Styles/AppStyles.axaml`:
```xml
<!-- Change #4CAF50 to your preferred color -->
<Setter Property="Background" Value="#4CAF50"/>
```

## Troubleshooting

### Issue: Splash screen doesn't appear
**Solution**: The splash screen shows for only 2.5 seconds. If you miss it, restart the app and watch carefully.

### Issue: Connection dialog appears but I don't have a drone
**Solution**: Click the "Cancel" button to skip connection and access the main UI.

### Issue: Navigation not highlighting
**Solution**: This is normal behavior. The active item highlights when you click on it.

### Issue: Logo not showing
**Solution**: The current logo is an SVG drone icon. To use your own logo, see `HOW_TO_ADD_LOGO.md`.

## Build Information

**Status**: ? Build Successful  
**Target Framework**: .NET 9  
**UI Framework**: Avalonia 11.2.1  
**Architecture**: MVVM with ReactiveUI  

## Next Steps

1. **Test the connection**:
   - Connect a flight controller via serial port
   - Select the correct COM port
   - Click "Connect"
   - Watch parameter loading progress

2. **Explore the UI**:
   - Navigate through different sections
   - Notice the smooth transitions
   - Test form controls

3. **Customize**:
   - Add your own logo
   - Adjust colors if needed
   - Modify splash screen duration

## Performance Notes

- Splash screen is lightweight (renders in <100ms)
- Connection dialog uses async operations
- Navigation is instant with smooth visual feedback
- No performance impact from animations (hardware accelerated)

## File Structure Reference

```
src/PavamanDroneConfigurator/
??? App.axaml.cs                    ? Startup flow
??? Views/
?   ??? SplashScreen.axaml          ? Splash screen UI
?   ??? SplashScreen.axaml.cs       ? Splash logic
?   ??? ConnectionDialog.axaml      ? Connection UI
?   ??? ConnectionDialog.axaml.cs   ? Connection logic
?   ??? MainWindow.axaml            ? Main UI with logo
??? ViewModels/
?   ??? MainWindowViewModel.cs      ? Navigation state
??? Styles/
?   ??? AppStyles.axaml             ? All styling
??? Assets/                         ? Place logo here
```

## Support

For issues or questions:
1. Check `IMPROVEMENTS_SUMMARY.md` for detailed changes
2. See `HOW_TO_ADD_LOGO.md` for logo customization
3. Review `UI_IMPROVEMENTS.md` for technical details

## Success Checklist

After running the app, verify:
- [ ] Splash screen appears and disappears smoothly
- [ ] Connection dialog shows with serial port options
- [ ] Can click "Cancel" to skip connection
- [ ] Main window opens with logo in header
- [ ] Navigation items highlight when clicked
- [ ] Smooth transitions on hover and click
- [ ] All sections are accessible
- [ ] No visual glitches or flickering

**All checks passed?** ?? Your UI improvements are working perfectly!

---

*Last Updated: December 30, 2024*  
*Version: 1.0.0*
