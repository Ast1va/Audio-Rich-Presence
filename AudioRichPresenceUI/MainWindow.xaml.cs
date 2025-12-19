using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Drawing; // Icon için

using System.Text.Json; // Ayarlar için

namespace AudioRichPresenceUI
{
    public class AppSettings
    {
        public bool AppleMusicEnabled { get; set; } = true;
        public bool YoutubeEnabled { get; set; } = true;
        public bool YoutubePrivacyEnabled { get; set; } = false;
        public bool RunAtStartup { get; set; } = false;
    }
    public partial class MainWindow : Window
    {
        // Şimdilik geliştirme: node index.js
        // Sonra: AudioRichPresenceNode.exe yapacağız.
        private const string BackgroundProcessFileName = "node";
        private const string BackgroundProcessArguments = "index.js";

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "AudioRichPresenceUI";
        private string SettingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");

        private Process? _nodeProcess;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private AppSettings _settings = new();

        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();
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
            SaveSettings(); // Kapatırken de garantiye al
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            UpdateNodeState();
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
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
            SaveSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { _settings = new AppSettings(); }

            // UI'ı güncelle
            ToggleApple.IsChecked = _settings.AppleMusicEnabled;
            ToggleYoutube.IsChecked = _settings.YoutubeEnabled;
            PrivacyCheckBox.IsChecked = _settings.YoutubePrivacyEnabled;
            StartupCheckBox.IsChecked = _settings.RunAtStartup;
        }

        private void SaveSettings()
        {
            try
            {
                _settings.AppleMusicEnabled = ToggleApple.IsChecked == true;
                _settings.YoutubeEnabled = ToggleYoutube.IsChecked == true;
                _settings.YoutubePrivacyEnabled = PrivacyCheckBox.IsChecked == true;
                _settings.RunAtStartup = StartupCheckBox.IsChecked == true;

                string json = JsonSerializer.Serialize(_settings);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
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
                string msg = ex.Message;
                // 'Sistem belirtilen dosyayı bulamıyor' - Node.js yüklü değilse genelde bu gelir
                if (ex is System.ComponentModel.Win32Exception winEx && winEx.NativeErrorCode == 2)
                {
                    msg = "Node.js sistemde bulunamadı. Lütfen Node.js'i (LTS) yükleyin veya PATH'e eklendiğinden emin olun.";
                }

                UpdateStatus(isRunning: false, error: $"HATA AYRINTISI: {msg}");
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
                StatusToolTip.Content = error;
                return;
            }

            if (isRunning)
            {
                StatusText.Text = "AKTİF";
                StatusCircle.Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF00FF80");
                StatusBadge.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#3300FF80");
                StatusToolTip.Content = "Her şey yolunda, arka plan süreci çalışıyor.";
            }
            else
            {
                StatusText.Text = "KAPALI";
                StatusCircle.Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#FF808080");
                StatusBadge.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1AFFFFFF");
                StatusToolTip.Content = "Zengin içerik paylaşımı devre dışı.";
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
