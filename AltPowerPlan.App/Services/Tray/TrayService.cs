using AltPowerPlan.Models;
using AltPowerPlan.Services.Power;
using AltPowerPlan.Services.Settings;
using AltPowerPlan.Services.Translation;
using AltPowerPlan.Services.Tray.Native;
using AltPowerPlan.Views.Windows;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

using System.Windows.Threading;

namespace AltPowerPlan.Services.Tray
{
    public class TrayService : ITrayService
    {
        private readonly IPowerPlanService _powerPlanService;
        private readonly ITranslationService _translationService;
        private readonly IAppSettingsProvider _settingsProvider;
        private readonly PowerFlyoutWindow _powerFlyoutWindow;

        private Window? _window;
        private IntPtr _hwnd = IntPtr.Zero;
        private IntPtr _hIcon = IntPtr.Zero;
        private HwndSource? _hwndSource;
        private string? _currentTooltip;
        private bool _isAdded;
        private bool _disposed;

        public TrayService(
            IPowerPlanService powerPlanService,
            ITranslationService translationService,
            IAppSettingsProvider settingsProvider,
            PowerFlyoutWindow powerFlyoutWindow)
        {
            _powerPlanService = powerPlanService;
            _translationService = translationService;
            _settingsProvider = settingsProvider;
            _powerFlyoutWindow = powerFlyoutWindow;

            _powerPlanService.ActivePlanChanged += OnActivePlanChanged;
            _powerPlanService.PowerStatusChanged += OnPowerStatusChanged;
            _powerFlyoutWindow.RequestOpenMainWindow += ShowWindow;
        }

        public void Initialize(Window window)
        {
            if (_isAdded)
                return;

            _window = window;
            _hwnd = new WindowInteropHelper(window).Handle;

            if (_hwnd == IntPtr.Zero)
                return;

            LoadTrayIcon();

            string tooltip = GetFormattedTooltip();
            _currentTooltip = tooltip.Length > 127 ? tooltip[..127] : tooltip;

            var nid = new TrayNativeMethods.NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<TrayNativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = TrayNativeMethods.NIF_MESSAGE | TrayNativeMethods.NIF_ICON | TrayNativeMethods.NIF_TIP,
                uCallbackMessage = (uint)TrayNativeMethods.WM_TRAYICON,
                hIcon = _hIcon,
                szTip = _currentTooltip
            };

            _isAdded = TrayNativeMethods.Shell_NotifyIcon(TrayNativeMethods.NIM_ADD, ref nid);

            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        public void UpdateTooltip(string text)
        {
            if (!_isAdded || _hwnd == IntPtr.Zero)
                return;

            string clampedText = text.Length > 127 ? text[..127] : text;
            if (string.Equals(_currentTooltip, clampedText, StringComparison.Ordinal))
                return;

            _currentTooltip = clampedText;

            var nid = new TrayNativeMethods.NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<TrayNativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = TrayNativeMethods.NIF_TIP,
                szTip = clampedText
            };

            TrayNativeMethods.Shell_NotifyIcon(TrayNativeMethods.NIM_MODIFY, ref nid);
        }

        public void Remove()
        {
            if (_isAdded && _hwnd != IntPtr.Zero)
            {
                var nid = new TrayNativeMethods.NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf<TrayNativeMethods.NOTIFYICONDATA>(),
                    hWnd = _hwnd,
                    uID = 1
                };

                TrayNativeMethods.Shell_NotifyIcon(TrayNativeMethods.NIM_DELETE, ref nid);
                _isAdded = false;
            }

            _currentTooltip = null;

            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            if (_hIcon != IntPtr.Zero)
            {
                TrayNativeMethods.DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
        }

        private void LoadTrayIcon()
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
            }
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
            }

            if (File.Exists(iconPath))
            {
                int cxSmIcon = TrayNativeMethods.GetSystemMetrics(TrayNativeMethods.SM_CXSMICON);
                int cySmIcon = TrayNativeMethods.GetSystemMetrics(TrayNativeMethods.SM_CYSMICON);
                if (cxSmIcon <= 0) cxSmIcon = 16;
                if (cySmIcon <= 0) cySmIcon = 16;

                _hIcon = TrayNativeMethods.LoadImage(
                    IntPtr.Zero,
                    iconPath,
                    TrayNativeMethods.IMAGE_ICON,
                    cxSmIcon,
                    cySmIcon,
                    TrayNativeMethods.LR_LOADFROMFILE);
            }

            if (_hIcon == IntPtr.Zero)
            {
                string? processPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                {
                    TrayNativeMethods.ExtractIconEx(processPath, 0, out IntPtr hLarge, out IntPtr hSmall, 1);
                    _hIcon = hSmall != IntPtr.Zero ? hSmall : hLarge;
                    if (hLarge != IntPtr.Zero && hLarge != _hIcon)
                    {
                        TrayNativeMethods.DestroyIcon(hLarge);
                    }
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == TrayNativeMethods.WM_TRAYICON)
            {
                int mouseMsg = (int)lParam;
                if (mouseMsg == TrayNativeMethods.WM_LBUTTONUP)
                {
                    ShowQuickMenu();
                    handled = true;
                }
                else if (mouseMsg == TrayNativeMethods.WM_LBUTTONDBLCLK)
                {
                    ShowWindow();
                    handled = true;
                }
                else if (mouseMsg == TrayNativeMethods.WM_RBUTTONUP || mouseMsg == TrayNativeMethods.WM_CONTEXTMENU)
                {
                    ShowContextMenu();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private void ShowQuickMenu()
        {
            _powerFlyoutWindow.ToggleAtScreenSide(_settingsProvider.Settings.QuickMenuSide);
        }

        private void ShowContextMenu()
        {
            if (_window == null || _hwnd == IntPtr.Zero)
                return;

            var menu = BuildContextMenu();
            TrayNativeMethods.SetForegroundWindow(_hwnd);
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private ContextMenu BuildContextMenu()
        {
            var menu = new ContextMenu();
            var activePlan = _powerPlanService.GetActivePowerPlan();

            // Header: Title
            var titleItem = new MenuItem
            {
                Header = "AltPowerPlan",
                FontWeight = FontWeights.Bold,
                IsEnabled = false
            };
            menu.Items.Add(titleItem);

            menu.Items.Add(new Separator());

            // Available power plans
            var plans = _powerPlanService.GetPowerPlans();
            var planMenuItems = new List<(MenuItem Item, PowerPlanModel Plan)>();

            foreach (var plan in plans)
            {
                bool isPlanActive = activePlan != null ? plan.Id == activePlan.Id : plan.IsActive;
                var planItem = new MenuItem
                {
                    Header = plan.Name,
                    IsCheckable = true,
                    IsChecked = isPlanActive
                };

                var targetPlan = plan;
                planItem.Click += (s, e) =>
                {
                    if (targetPlan.Id == _powerPlanService.GetActivePowerPlan()?.Id)
                    {
                        planItem.IsChecked = true;
                        return;
                    }

                    if (_powerPlanService.SetActivePowerPlan(targetPlan.Id))
                    {
                        foreach (var (item, p) in planMenuItems)
                        {
                            item.IsChecked = p.Id == targetPlan.Id;
                        }
                    }
                    else
                    {
                        var currentActive = _powerPlanService.GetActivePowerPlan();
                        planItem.IsChecked = currentActive != null && targetPlan.Id == currentActive.Id;
                    }
                };

                planMenuItems.Add((planItem, plan));
                menu.Items.Add(planItem);
            }

            menu.Items.Add(new Separator());

            // Open Window
            var openItem = new MenuItem
            {
                Header = _translationService.GetString("TrayOpenWindow")
            };
            openItem.Click += (s, e) => ShowWindow();
            menu.Items.Add(openItem);

            // Exit
            var exitItem = new MenuItem
            {
                Header = _translationService.GetString("TrayExit")
            };
            exitItem.Click += (s, e) => ExitApplication();
            menu.Items.Add(exitItem);

            return menu;
        }

        private void ShowWindow()
        {
            if (_window != null)
            {
                if (_window.WindowState == WindowState.Minimized)
                {
                    _window.WindowState = WindowState.Normal;
                }

                _window.Show();
                _window.Activate();
            }
        }

        private void ExitApplication()
        {
            Remove();
            Application.Current?.Shutdown();
        }

        private string GetFormattedTooltip(PowerPlanModel? plan = null, SystemPowerStatusModel? status = null)
        {
            var p = plan ?? _powerPlanService.GetActivePowerPlan();
            var s = status ?? _powerPlanService.GetPowerStatus();

            string planName = p?.Name ?? "Balanced";
            string batteryInfo = s.HasBattery
                ? $" ({s.BatteryPercentage}%{(s.IsCharging ? " ⚡" : (s.IsOnAcPower ? " AC" : ""))})"
                : "";

            return $"AltPowerPlan - {planName}{batteryInfo}";
        }

        private void OnActivePlanChanged(object? sender, PowerPlanModel? newActivePlan)
        {
            Application.Current?.Dispatcher?.BeginInvoke(
                DispatcherPriority.Background,
                () => UpdateTooltip(GetFormattedTooltip(plan: newActivePlan)));
        }

        private void OnPowerStatusChanged(object? sender, SystemPowerStatusModel status)
        {
            Application.Current?.Dispatcher?.BeginInvoke(
                DispatcherPriority.Background,
                () => UpdateTooltip(GetFormattedTooltip(status: status)));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _powerPlanService.ActivePlanChanged -= OnActivePlanChanged;
                _powerPlanService.PowerStatusChanged -= OnPowerStatusChanged;
                _powerFlyoutWindow.RequestOpenMainWindow -= ShowWindow;
                Remove();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
