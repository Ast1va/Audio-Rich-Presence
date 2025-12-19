<div align="center">
  <img src="logo.png" width="128" height="128" alt="Logo">
  <h1>Audio Rich Presence</h1>
  <p><i>Premium Discord Activity for Apple Music & YouTube</i></p>

  [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
  [![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
</div>

---

## 💎 Liquid Glass Experience
A state-of-the-art Windows application designed with **Liquid Glass** aesthetics. Semi-transparent interfaces, smooth gradients, and subtle breathing animations create a premium user experience that feels native to modern Windows.

## ✨ Features
*   **Dual Engine**: Seamlessly switch between **Apple Music** and **YouTube** presence.
*   **Privacy First**: Built-in privacy toggle for YouTube to hide specific metadata.
-   **Tray Operations**: Minimize to the system tray to keep your workspace clean.
*   **Smart Lifecycle**: Zero-leak process management. When the UI closes, everything closes.
*   **Startup Ready**: Optional auto-launch with Windows.

## 🛠️ Architecture
The project leverages a high-performance **Tri-Process** architecture:
1.  **WPF UI (C#)**: The beautiful frontend providing real-time controls.
2.  **Node.js Backend**: The orchestrator managing Discord RPC and logic.
3.  **Media Helper (Console)**: A dedicated .NET 8 tool for low-level Windows Media Session integration.

## 🚀 Getting Started
### Prerequisites
- .NET 8 SDK
- [Node.js (LTS)](https://nodejs.org/) (Required for backend Discord RPC)

### Installation
1. Clone the repository.
2. Run `npm install` inside the `AudioRichPresenceNode` folder.
3. Build the solution using Visual Studio, Rider, or CLI.

## 🏗️ Building for Production
To create a standalone, self-contained executable:
```powershell
# Build the UI
dotnet publish AudioRichPresenceUI/AudioRichPresenceUI.csproj -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true -o dist

# Build the Helper
dotnet publish NowPlayingHelper/NowPlayingHelper.csproj -c Release -r win-x64 --self-contained=true -o dist/NowPlayingHelper
```

---
<div align="center">
  Developed with ❤️ for the music community.
</div>
