# TMP Text Synchronizer

A Unity utility that automatically synchronizes font sizes across multiple TextMeshPro (TMP) text components using Min, Max, or Average strategies.

## 📋 Overview

When working with dynamic UI layouts, you often need multiple text components to maintain consistent font sizes. This tool automatically calculates and applies optimal font sizes to all TMP_Text components registered under the GameObject where this component is attached, based on your chosen strategy.

> ⚠️ **Performance Note**  
> Auto-update during window resizing may cause frame drops. For production builds, disable automatic updates and call `SynchronizeFontSizes()` manually at controlled moments (scene load, text changes, settings changes).

## ✨ Key Features

- **Three Synchronization Strategies**
  - **Min**: Synchronize to the smallest calculated font size
  - **Max**: Synchronize to the largest calculated font size
  - **Average**: Synchronize to the average of all calculated sizes

- **Auto-Sizing Integration**: Works with TextMeshPro's built-in auto-sizing feature to calculate optimal sizes
- **Performance Optimization**: FPS limiting to prevent excessive calculations
- **Screen Size Responsive**: Automatically updates when screen resolution changes
- **Nested Synchronizer Support**: Handles hierarchies with multiple synchronizer components
- **Editor Tools**: Custom inspector buttons and menu items for easy testing

## 🎯 Use Cases

- **Multi-language UI**: Maintain consistent button text sizes across different languages with varying text lengths
- **Dynamic Layouts**: Keep uniform font sizes in responsive UI panels
- **Auto-sized Text Groups**: Synchronize multiple auto-sizing text components that need to match
- **Prototyping**: Test various screen sizes during UI development

## 🚀 Installation

### Via Unity Package Manager

1. Open `Window > Package Manager`
2. Click `+` → `Add package from git URL...`
3. Enter:
   ```
   https://github.com/lend-a-villin/Unity_TMP_TextSynchronizer.git
   ```

### Manual Installation

1. Download or clone this repository
2. Copy the files into your Unity project's `Assets` folder

## 📖 Usage

### Basic Setup

1. Create or select a GameObject to manage synchronization
2. Add the `FontSizeSynchronizer` component (`Add Component > UI > TMP Text Synchronizer`)
3. The component will automatically find all child TMP_Text components
4. Select your preferred **Resize Type** (Min/Max/Average)
5. Click the **"🔄 Synchronize Now"** button in the inspector, or it will synchronize automatically at runtime

### Component Settings

**Settings**
- **Font Size Calculation Method**: Choose Min, Max, or Average strategy
- **Auto Update On Screen Size Change**: Enable to automatically recalculate when screen resolution changes
- **Minimum Font Size**: Lower bound for font size (default: 1)
- **Maximum Font Size**: Upper bound for font size (default: 1024)
- **Force Override Nested**: When enabled, ignores nested FontSizeSynchronizer components and manages all descendant texts

**Performance**
- **Max Calculation FPS**: Limits how often font sizes are recalculated (1-300 FPS)
- **Apply FPS Limit To Manual Calls**: Also apply FPS limiting to manual `SynchronizeFontSizes()` calls

**Debug**
- **Show Debug Logs**: Enable detailed logging for troubleshooting

### Runtime Usage

For production builds or dynamic scenarios, you can control synchronization programmatically:

```csharp
using UnityEngine;

public class MyUIController : MonoBehaviour
{
    [SerializeField] private FontSizeSynchronizer textSynchronizer;
    
    void Start()
    {
        // Synchronize once when the scene loads
        textSynchronizer.SynchronizeFontSizes();
    }
    
    public void OnTextContentChanged()
    {
        // Re-synchronize when text content changes
        textSynchronizer.SynchronizeFontSizes();
    }
    
    public void OnResolutionChanged()
    {
        // Re-synchronize when user changes resolution in settings
        textSynchronizer.SynchronizeFontSizes();
    }
}
```

**When to Call `SynchronizeFontSizes()`:**
- Scene initialization (Start/OnEnable)
- After changing text content dynamically
- When user changes resolution in game settings
- During loading screens (frame drops are acceptable)
- After language changes in localized UIs

### Editor Tools

**Inspector Buttons**
- **🔄 Synchronize Now**: Immediately calculate and apply font sizes
- **Current Font Size**: Displays the currently synchronized font size

**Context Menu** (Right-click on component)
- **Synchronize Font Sizes**: Manual synchronization
- **Show Text Components Info**: Display detailed information about all managed text components
- **Reset to Default Settings**: Restore default settings

**Tools Menu**
- `Tools > TMP Text Synchronizer > Add to Selected`: Add component to selected GameObjects
- `Tools > TMP Text Synchronizer > Remove from Selected`: Remove component from selected GameObjects
- `Tools > TMP Text Synchronizer > Synchronize All in Scene`: Synchronize all FontSizeSynchronizer components in the scene

## 🔧 How It Works

1. **Discovery**: Finds all TMP_Text components in children (excludes nested synchronizer components, can include with Force Override Nested option)
2. **Calculation**: Temporarily enables auto-sizing on each text component to calculate optimal size
3. **Strategy Application**: Applies Min/Max/Average strategy to all calculated sizes
4. **Synchronization**: Disables auto-sizing and sets all text components to the target font size

## 🚀 Production Build Recommendations

**For Development:**
- ✅ Enable **Auto Update On Screen Size Change** for real-time testing across different resolutions
- ✅ Use **Show Debug Logs** to verify behavior
- ✅ Test with various screen sizes in the editor

**For Production Builds:**
- ⚠️ **Disable Auto Update On Screen Size Change** to prevent frame drops during window resizing
- ✅ Call `SynchronizeFontSizes()` manually at controlled moments:
  - Once during scene initialization (e.g., `Start()`)
  - When text content changes (e.g., player name, level)
  - When user changes resolution in settings menu
  - During loading screens where frame drops are acceptable
- ✅ Consider using **FPS limiting** if you need occasional runtime updates

**Alternative Approach:**
1. Use this tool during development to find optimal font sizes
2. Note the calculated font size (displayed in inspector)
3. In production, manually set that font size instead of using auto-sizing
4. Remove the component from production builds for maximum performance

## ⚙️ Requirements

- TextMeshPro package

## 📝 Tips

- For best results, ensure your TMP_Text components have proper `fontSizeMin` and `fontSizeMax` values set
- **Generally use the Min strategy** to ensure all text doesn't overflow its container
- Use **Max** or **Average** strategies only in special situations
- Enable **Show Debug Logs** when troubleshooting to see detailed calculation information
- If you experience performance issues, lower **Max Calculation FPS** or disable **Auto Update On Screen Size Change**

## 🐛 Known Limitations

- Auto-update during window resizing may cause frame drops
- Nested FontSizeSynchronizer components require either `Force Override Nested` or proper hierarchy planning
- Performance scales with the number of text components (use FPS limiting for large sets)
- Only works with TMP_Text components (legacy Unity UI Text is not supported)

## 📧 Support

For issues, feature requests, or questions, please open an issue on GitHub.

## 📄 License

MIT
