# Audio Rich Presence & WPF UI 🎵✨

A premium, modern Windows application to showcase your current Apple Music or YouTube activity on Discord. Features a stunning "Liquid Glass" aesthetic and optimized performance.

## 🚀 Key Features

- **Liquid Glass UI**: Modern WPF interface with semi-transparent cards, smooth animations, and a breathing background.
- **Dual Source Support**: Supports both **Apple Music** and **YouTube** (via web scraping/Windows Media Session).
- **Privacy Mode**: Privacy toggle for YouTube presence to keep your listening habits discreet.
- **System Tray Integration**: Minimizes to the system tray (notification area) for background operation.
- **Auto-Startup**: Option to launch automatically when Windows starts.
- **Clean Lifecycle**: Optimized background process management—closing the app kills all child processes immediately.

## 🛠️ Architecture

- **UI**: .NET 8 WPF (C#)
- **Backend Orchestrator**: Node.js
- **Media Helper**: .NET 8 Console Application (Helper)
- **IPC**: Stdin/Stdout pipes for efficient inter-process communication.

## 📦 How to Run

### Run from Source
1. Clone the repository.
2. Ensure you have Node.js and .NET 8 SDK installed.
3. Open `AudioRichPresenceUI.sln` in Visual Studio or Rider.
4. Build and Run.

### Standalone Build
Use the provided `dotnet publish` commands:
```powershell
# Build WPF UI
dotnet publish AudioRichPresenceUI/AudioRichPresenceUI.csproj -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist

# Build Helper
dotnet publish NowPlayingHelper/NowPlayingHelper.csproj -c Release -r win-x64 --self-contained=true -o dist/NowPlayingHelper
```

## 📄 License
MIT
