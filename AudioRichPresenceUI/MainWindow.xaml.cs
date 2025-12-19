using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Drawing; // Icon için

namespace AudioRichPresenceUI
{
    public partial class MainWindow : Window
    {
        // Şimdilik geliştirme: node index.js
        // Sonra: AudioRichPresenceNode.exe yapacağız.
        private const string BackgroundProcessFileName = "node";
        private const string BackgroundProcessArguments = "index.js";

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "AudioRichPresenceUI";

        private Process? _nodeProcess;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();
            LoadStartupState();
            UpdateStatus(isRunning: false);
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            
            // Logo integration
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "logo.png");
                if (File.Exists(iconPath))
                {
                    using (var stream = File.OpenRead(iconPath))
                    {
                        var bitmap = (Bitmap)System.Drawing.Image.FromStream(stream);
                        _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                    }
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Information;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Information;
            }

            _notifyIcon.Text = "Audio Rich Presence";
            _notifyIcon.Visible = true;

            // Context Menu
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Göster", null, (s, e) => ShowWindow());
            menu.Items.Add("Çıkış", null, (s, e) => Close());
            _notifyIcon.ContextMenuStrip = menu;

            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(3000, "Rich Presence", "Uygulama arka planda çalışmaya devam ediyor.", System.Windows.Forms.ToolTipIcon.None);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close(); // Trigger OnClosed
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            StopNode();
            base.OnClosed(e);
        }

        // Pencerenin herhangi bir yerinden sürükleme
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            UpdateNodeState();
        }

        private void UpdateNodeState()
        {
            bool apple = ToggleApple.IsChecked == true;
            bool youtube = ToggleYoutube.IsChecked == true;
            bool privacy = PrivacyCheckBox.IsChecked == true;

            // Eğer her ikisi de kapalıysa node'u kapat
            if (!apple && !youtube)
            {
                StopNode();
                return;
            }

            // En az biri açıksa node çalışmalı
            StartNode();

            // Config gönder
            SendConfig(apple, youtube, privacy);
        }

        private void SendConfig(bool apple, bool youtube, bool privacy)
        {
            if (_nodeProcess == null || _nodeProcess.HasExited) return;

            try
            {
                var json = $"{{\"apple\":{apple.ToString().ToLower()},\"youtube\":{youtube.ToString().ToLower()},\"youtubePrivacy\":{privacy.ToString().ToLower()}}}";
                _nodeProcess.StandardInput.WriteLine(json);
            }
            catch
            {
                // sessiz
            }
        }

        private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            SetRunOnStartup(StartupCheckBox.IsChecked == true);
        }

        private void StartNode()
        {
            try
            {
                if (_nodeProcess != null && !_nodeProcess.HasExited)
                {
                    UpdateStatus(isRunning: true);
                    return;
                }

                // Debug'da: ...\AudioRichPresenceUI\bin\Debug\net8.0-windows\
                // Publish'te: ...\dist\ (tek klasör)
                string baseDir = AppContext.BaseDirectory;

                // 1) Publish senaryosu: Node klasörü exe ile aynı klasörde olabilir
                // dist/
                //   AudioRichPresenceUI.exe
                //   AudioRichPresenceNode/  (veya ileride Node exe)
                string nodeDir = Path.Combine(baseDir, "AudioRichPresenceNode");

                // 2) Debug senaryosu: baseDir bin/Debug/... içindedir.
                // Oradan yukarı çıkıp proje köküne gidip AudioRichPresenceNode'ı bulacağız.
                if (!Directory.Exists(nodeDir))
                {
                    // baseDir: ...\AudioRichPresenceUI\bin\Debug\net8.0-windows\
                    // 4 seviye yukarı -> ...\Audio Rich Presence and WPF\
                    string root = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
                    nodeDir = Path.Combine(root, "AudioRichPresenceNode");
                }

                if (!Directory.Exists(nodeDir))
                {
                    UpdateStatus(isRunning: false, error: $"Node klasörü bulunamadı: {nodeDir}");
                    ToggleApple.IsChecked = false;
                    ToggleYoutube.IsChecked = false;
                    return;
                }

                string indexPath = Path.Combine(nodeDir, BackgroundProcessArguments);
                if (!File.Exists(indexPath))
                {
                    UpdateStatus(isRunning: false, error: $"index.js bulunamadı: {indexPath}");
                    ToggleApple.IsChecked = false;
                    ToggleYoutube.IsChecked = false;
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = BackgroundProcessFileName,          // node
                    Arguments = BackgroundProcessArguments,        // index.js
                    WorkingDirectory = nodeDir,                    // ✅ kritik
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true                   // ✅ IPC için gerekli
                };

                _nodeProcess = Process.Start(psi);

                if (_nodeProcess == null)
                {
                    UpdateStatus(isRunning: false, error: "Node process başlatılamadı (null döndü)");
                    ToggleApple.IsChecked = false;
                    ToggleYoutube.IsChecked = false;
                    return;
                }

                UpdateStatus(isRunning: true);
            }
            catch (Exception ex)
            {
                UpdateStatus(isRunning: false, error: ex.Message);
                ToggleApple.IsChecked = false;
                ToggleYoutube.IsChecked = false;
            }
        }

        private void StopNode()
        {
            try
            {
                if (_nodeProcess != null && !_nodeProcess.HasExited)
                {
                    _nodeProcess.Kill(entireProcessTree: true); // Kill node and NowPlayingHelper
                    _nodeProcess.Dispose();
                }
            }
            catch
            {
                // sessiz
            }
            finally
            {
                _nodeProcess = null;
                UpdateStatus(isRunning: false);
            }
        }

        private void UpdateStatus(bool isRunning, string? error = null)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                StatusText.Text = "HATA";
                StatusCircle.Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FFFF4444");
                StatusBadge.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#33FF4444");
                return;
            }

            if (isRunning)
            {
                StatusText.Text = "AKTİF";
                StatusCircle.Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF00FF80");
                StatusBadge.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#3300FF80");
            }
            else
            {
                StatusText.Text = "KAPALI";
                StatusCircle.Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF808080");
                StatusBadge.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1AFFFFFF");
            }
        }

        private void LoadStartupState()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                if (key == null) return;

                var value = key.GetValue(RunValueName) as string;
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;

                StartupCheckBox.IsChecked =
                    !string.IsNullOrEmpty(value) &&
                    exePath != null &&
                    string.Equals(value, exePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // sessiz
            }
        }

        private void SetRunOnStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                               ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath == null) return;

                if (enable)
                    key.SetValue(RunValueName, exePath);
                else
                    key.DeleteValue(RunValueName, false);
            }
            catch
            {
                // sessiz
            }
        }
    }
}
