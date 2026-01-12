# OSM Map Quick Reference

## ?? Quick Start

### Run the Application
```bash
cd C:\Pavaman\Final-repo
dotnet run --project PavamanDroneConfigurator.UI\PavamanDroneConfigurator.UI.csproj
```

### View GPS Track on Map
1. Click **"?? Load Log"** button
2. Select a .bin flight log with GPS data
3. Click **"Map"** tab
4. GPS track automatically displays!

---

## ?? Map Controls

### **Mouse Controls**
- **Pan:** Click and drag
- **Zoom In:** Mouse wheel up
- **Zoom Out:** Mouse wheel down

### **Button Controls**
- **?? Zoom to Track** - Fit entire flight path in view
- **?? Center on Start** - Pan to launch point
- **?? Center on End** - Pan to landing point

---

## ?? Visual Legend

| Symbol | Meaning |
|--------|---------|
| Blue Line | GPS flight track |
| Green Circle | Start position (launch) |
| Red Circle | End position (landing) |

---

## ?? Info Overlay

**Location:** Bottom-left corner

**Shows:**
- **Track Points:** Number of GPS coordinates
- **Center:** Current map center (lat, lon)
- **Duration:** Total flight time

---

## ?? Troubleshooting

### Map Not Showing
**Problem:** "No GPS data available" message

**Solutions:**
1. ? Verify log file contains GPS messages
2. ? Check GPS had satellite lock during flight
3. ? Ensure GPS data is not all (0,0) coordinates

### Track Not Rendering
**Problem:** Map loads but track doesn't appear

**Solutions:**
1. ? Click "?? Zoom to Track" button
2. ? Check console for errors
3. ? Reload log file

### Map Tiles Not Loading
**Problem:** Gray/blank map

**Solutions:**
1. ? Check internet connection
2. ? Wait 2-3 seconds for tiles to download
3. ? Zoom in/out to trigger tile refresh

---

## ?? Sample Flight Logs

### Where to Find
- Download sample logs from ArduPilot forums
- Use your own drone flight logs (.bin files)
- Mission Planner logs work perfectly

### Requirements
- Must be DataFlash .bin format
- Must contain GPS messages
- GPS must have had valid fix (?6 satellites)

---

## ?? Tips & Tricks

### **Best Practices**
1. ? Always click "Zoom to Track" first for best view
2. ? Use "Center on Start/End" to inspect launch/landing
3. ? Zoom in for detailed track inspection
4. ? Check info overlay for track statistics

### **Performance**
- First tile load may take 2-3 seconds
- Subsequent views use cached tiles (faster)
- Tracks with 1000+ points render smoothly

---

## ?? Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Mouse Wheel | Zoom |
| Drag | Pan |
| Double Click | Zoom in on point |

---

## ?? Advanced

### Export GPS Track
1. Go to Map tab
2. Click "??? Export KML" (top toolbar)
3. Open in Google Earth

### Compare Multiple Flights
*Coming soon - multiple track overlay*

---

## ?? Support

**Issues?**
- Check OSM_MAP_INTEGRATION_COMPLETE.md for detailed docs
- Verify build: `dotnet build PavamanDroneConfigurator.sln`
- Check logs in console output

---

**Last Updated:** January 2026  
**Version:** 1.0  
**Status:** Production Ready ?
