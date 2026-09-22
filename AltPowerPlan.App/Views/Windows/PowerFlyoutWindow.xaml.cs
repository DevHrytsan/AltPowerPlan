using AltPowerPlan.Models;
using AltPowerPlan.Services.Power;
using AltPowerPlan.Services.Themes;
using AltPowerPlan.Services.Tray.Native;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace AltPowerPlan.Views.Windows
{
    public partial class PowerFlyoutWindow : Window
    {
        private readonly IPowerPlanService _powerPlanService;
        private readonly IAppThemeService _themeService;

        private DateTime _lastDeactivatedTime = DateTime.MinValue;
        private Rect _currentWorkArea;
        private ScreenSide _currentSide;

        public event Action? RequestOpenMainWindow;

        private static bool GetMonitorDpi(IntPtr hMonitor, out double scaleX, out double scaleY)
        {
            scaleX = 1.0;
            scaleY = 1.0;
            try
            {
                if (TrayNativeMethods.GetDpiForMonitor(hMonitor, 0, out uint dpiX, out uint dpiY) == 0 && dpiX > 0 && dpiY > 0)
                {
                    scaleX = dpiX / 96.0;
                    scaleY = dpiY / 96.0;
                    return true;
                }
            }
            catch
            {
                // Fallback if shcore is unavailable
            }
            return false;
        }

        private static readonly SolidColorBrush DarkFlyoutBackground = CreateFrozenBrush(0x1F, 0x1F, 0x1F);
        private static readonly SolidColorBrush DarkFlyoutBorder = CreateFrozenBrush(0x38, 0x38, 0x38);
        private static readonly SolidColorBrush DarkSeparator = CreateFrozenBrush(0x2C, 0x2C, 0x2C);
        private static readonly SolidColorBrush DarkHeaderTitle = Brushes.White;
        private static readonly SolidColorBrush DarkTextPrimary = Brushes.White;

        private static readonly SolidColorBrush LightFlyoutBackground = CreateFrozenBrush(0xF7, 0xF7, 0xF7);
        private static readonly SolidColorBrush LightFlyoutBorder = CreateFrozenBrush(0xD0, 0xD0, 0xD0);
        private static readonly SolidColorBrush LightSeparator = CreateFrozenBrush(0xE2, 0xE2, 0xE2);
        private static readonly SolidColorBrush LightHeaderTitle = CreateFrozenBrush(0x1A, 0x1A, 0x1A);
        private static readonly SolidColorBrush LightTextPrimary = CreateFrozenBrush(0x1A, 0x1A, 0x1A);

        private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public PowerFlyoutWindow(
            IPowerPlanService powerPlanService,
            IAppThemeService themeService)
        {
            _powerPlanService = powerPlanService;
            _themeService = themeService;

            InitializeComponent();

            SourceInitialized += OnSourceInitialized;
            SizeChanged += OnFlyoutSizeChanged;
            Deactivated += (s, e) =>
            {
                _lastDeactivatedTime = DateTime.UtcNow;
                Hide();
            };
            Closing += (s, e) => { e.Cancel = true; Hide(); };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Hide();
                    e.Handled = true;
                }
                else if (e.Key is Key.Up or Key.Down)
                {
                    if (Keyboard.FocusedElement is not System.Windows.Controls.Button)
                    {
                        FocusActivePlanItem();
                        e.Handled = true;
                    }
                }
            };

            _powerPlanService.ActivePlanChanged += (s, plan) =>
            {
                Dispatcher.Invoke(() => UpdateActivePlan(plan));
            };

            _powerPlanService.PowerStatusChanged += (s, status) =>
            {
                Dispatcher.Invoke(() => UpdatePowerStatus(status));
            };
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = TrayNativeMethods.GetWindowLong(hwnd, TrayNativeMethods.GWL_EXSTYLE);
            TrayNativeMethods.SetWindowLong(hwnd, TrayNativeMethods.GWL_EXSTYLE, exStyle | TrayNativeMethods.WS_EX_TOOLWINDOW);
        }

        public void ToggleAtScreenSide(ScreenSide side, bool fromHotkey = false)
        {
            if (IsVisible)
            {
                Hide();
                return;
            }

            if (!fromHotkey && (DateTime.UtcNow - _lastDeactivatedTime).TotalMilliseconds < 250)
            {
                return;
            }

            ShowAtScreenSide(side, fromHotkey);
        }

        public void ShowAtScreenSide(ScreenSide side, bool fromHotkey = false)
        {
            _currentSide = side;

            ApplyTheme();

            var plans = _powerPlanService.GetPowerPlans();
            PlansItemsControl.ItemsSource = plans;

            var active = _powerPlanService.GetActivePowerPlan();
            UpdateActivePlan(active);

            var status = _powerPlanService.GetPowerStatus();
            UpdatePowerStatus(status);

            ApplyTemplate();
            PlansItemsControl.ApplyTemplate();
            UpdateLayout();
            Measure(new Size(Width, double.PositiveInfinity));

            double flyoutWidth = ActualWidth > 0 ? ActualWidth : Width;
            double flyoutHeight = DesiredSize.Height > 0 ? DesiredSize.Height : (ActualHeight > 0 ? ActualHeight : 280);

            new WindowInteropHelper(this).EnsureHandle();

            TrayNativeMethods.GetCursorPos(out var pt);
            IntPtr hMonitor = TrayNativeMethods.MonitorFromPoint(pt, TrayNativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new TrayNativeMethods.MONITORINFO();
            mi.cbSize = Marshal.SizeOf<TrayNativeMethods.MONITORINFO>();

            Rect workArea;
            if (TrayNativeMethods.GetMonitorInfo(hMonitor, ref mi))
            {
                double dpiScaleX;
                double dpiScaleY;

                if (GetMonitorDpi(hMonitor, out double monDpiX, out double monDpiY))
                {
                    dpiScaleX = monDpiX;
                    dpiScaleY = monDpiY;
                }
                else
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                    dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                }

                workArea = new Rect(
                    mi.rcWork.Left / dpiScaleX,
                    mi.rcWork.Top / dpiScaleY,
                    (mi.rcWork.Right - mi.rcWork.Left) / dpiScaleX,
                    (mi.rcWork.Bottom - mi.rcWork.Top) / dpiScaleY
                );
            }
            else
            {
                workArea = SystemParameters.WorkArea;
            }

            _currentWorkArea = workArea;

            double targetLeft;
            double targetTop;

            switch (side)
            {
                case ScreenSide.BottomRight:
                    targetLeft = workArea.Right - flyoutWidth;
                    targetTop = workArea.Bottom - flyoutHeight;
                    break;

                case ScreenSide.BottomLeft:
                    targetLeft = workArea.Left;
                    targetTop = workArea.Bottom - flyoutHeight;
                    break;

                case ScreenSide.TopRight:
                    targetLeft = workArea.Right - flyoutWidth;
                    targetTop = workArea.Top;
                    break;

                case ScreenSide.TopLeft:
                    targetLeft = workArea.Left;
                    targetTop = workArea.Top;
                    break;

                case ScreenSide.BottomCenter:
                    targetLeft = workArea.Left + (workArea.Width - flyoutWidth) / 2.0;
                    targetTop = workArea.Bottom - flyoutHeight;
                    break;

                case ScreenSide.TopCenter:
                    targetLeft = workArea.Left + (workArea.Width - flyoutWidth) / 2.0;
                    targetTop = workArea.Top;
                    break;

                default:
                    targetLeft = workArea.Right - flyoutWidth;
                    targetTop = workArea.Bottom - flyoutHeight;
                    break;
            }

            // Screen boundary clamping: ensure window stays entirely within workArea
            if (targetLeft + flyoutWidth > workArea.Right)
                targetLeft = workArea.Right - flyoutWidth;
            if (targetLeft < workArea.Left)
                targetLeft = workArea.Left;

            if (targetTop + flyoutHeight > workArea.Bottom)
                targetTop = workArea.Bottom - flyoutHeight;
            if (targetTop < workArea.Top)
                targetTop = workArea.Top;

            Left = targetLeft;
            Top = targetTop;

            Show();
            Activate();
            Focus();
            var hwnd = new WindowInteropHelper(this).Handle;
            TrayNativeMethods.SetForegroundWindow(hwnd);

            if (fromHotkey)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
                {
                    FocusActivePlanItem();
                });
            }
        }

        private void OnFlyoutSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!IsVisible || _currentWorkArea.IsEmpty)
                return;

            if (_currentSide is ScreenSide.BottomRight or ScreenSide.BottomLeft or ScreenSide.BottomCenter)
            {
                double targetTop = _currentWorkArea.Bottom - e.NewSize.Height;
                if (targetTop < _currentWorkArea.Top)
                    targetTop = _currentWorkArea.Top;

                if (Math.Abs(Top - targetTop) > 0.5)
                {
                    Top = targetTop;
                }
            }
        }

        private void UpdateActivePlan(PowerPlanModel? active)
        {
            if (active != null)
            {
                HeaderPlanName.Text = active.Name;
            }
        }

        private void UpdatePowerStatus(SystemPowerStatusModel status)
        {
            if (status.HasBattery)
            {
                BatteryBadge.Visibility = Visibility.Visible;
                BatteryBadgeText.Text = $"{status.BatteryPercentage}%";
                BatteryBadgeIcon.Symbol = status.IsCharging
                    ? Wpf.Ui.Controls.SymbolRegular.BatteryCharge24
                    : (status.BatteryPercentage <= 20
                        ? Wpf.Ui.Controls.SymbolRegular.BatteryWarning24
                        : Wpf.Ui.Controls.SymbolRegular.Battery1024);
            }
            else
            {
                BatteryBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyTheme()
        {
            var themeChoice = _themeService.GetTheme();
            bool isDark = themeChoice == ThemeChoice.Dark;
            if (themeChoice == ThemeChoice.System)
            {
                isDark = ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark;
            }

            if (isDark)
            {
                FlyoutBorder.Background = DarkFlyoutBackground;
                FlyoutBorder.BorderBrush = DarkFlyoutBorder;
                Separator1.Fill = DarkSeparator;
                Separator2.Fill = DarkSeparator;
                HeaderTitle.Foreground = DarkHeaderTitle;
                Foreground = DarkTextPrimary;
                BatteryBadgeText.Foreground = DarkTextPrimary;
            }
            else
            {
                FlyoutBorder.Background = LightFlyoutBackground;
                FlyoutBorder.BorderBrush = LightFlyoutBorder;
                Separator1.Fill = LightSeparator;
                Separator2.Fill = LightSeparator;
                HeaderTitle.Foreground = LightHeaderTitle;
                Foreground = LightTextPrimary;
                BatteryBadgeText.Foreground = LightTextPrimary;
            }
        }

        private void OnPlanItemClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PowerPlanModel plan)
            {
                _powerPlanService.SetActivePowerPlan(plan.Id);
                Hide();
            }
        }

        private void OnOpenAppClicked(object sender, RoutedEventArgs e)
        {
            Hide();
            RequestOpenMainWindow?.Invoke();
        }

        private void FocusActivePlanItem()
        {
            if (PlansItemsControl.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                void OnGeneratorStatusChanged(object? sender, EventArgs e)
                {
                    if (PlansItemsControl.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                    {
                        PlansItemsControl.ItemContainerGenerator.StatusChanged -= OnGeneratorStatusChanged;
                        ApplyActivePlanFocus();
                    }
                }
                PlansItemsControl.ItemContainerGenerator.StatusChanged += OnGeneratorStatusChanged;
                return;
            }

            ApplyActivePlanFocus();
        }

        private void ApplyActivePlanFocus()
        {
            if (PlansItemsControl.ItemsSource is not IEnumerable<PowerPlanModel> plans)
                return;

            int targetIndex = 0;
            int i = 0;
            foreach (var plan in plans)
            {
                if (plan.IsActive)
                {
                    targetIndex = i;
                    break;
                }
                i++;
            }

            if (PlansItemsControl.ItemContainerGenerator.ContainerFromIndex(targetIndex) is FrameworkElement container)
            {
                var button = container as System.Windows.Controls.Button ?? FindVisualChild<System.Windows.Controls.Button>(container);
                if (button != null)
                {
                    button.Focus();
                    Keyboard.Focus(button);
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
