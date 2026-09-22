using AltPowerPlan.Models;
using AltPowerPlan.Services.Settings;
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace AltPowerPlan.Services.Hotkey
{
    public class HotkeyService : IHotkeyService
    {
        private const int HOTKEY_ID = 9001;
        private const int WM_HOTKEY = 0x0312;

        private const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public event Action? HotkeyPressed;

        public HotkeyModel CurrentHotkey { get; private set; } = HotkeyModel.None;

        public bool IsRegistered { get; private set; }

        public string? LastError { get; private set; }

        private readonly IAppSettingsProvider _settingsProvider;
        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource? _hwndSource;
        private bool _disposed;

        public HotkeyService(IAppSettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider;
        }

        public void Initialize(IntPtr hwnd)
        {
            _hwnd = hwnd;
            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);

            UpdateHotkey(_settingsProvider.Settings.QuickMenuHotkey);
        }

        public bool UpdateHotkey(HotkeyModel hotkey)
        {
            Unregister();
            CurrentHotkey = hotkey;

            if (_hwnd == IntPtr.Zero || hotkey.IsNone || !hotkey.IsValid)
            {
                LastError = null;
                return true;
            }

            uint modifiers = hotkey.GetWin32Modifiers();
            uint vk = hotkey.GetWin32VirtualKey();

            if (vk == 0)
            {
                LastError = "Invalid key";
                return false;
            }

            // Try with MOD_NOREPEAT first
            bool success = RegisterHotKey(_hwnd, HOTKEY_ID, modifiers | MOD_NOREPEAT, vk);

            if (!success)
            {
                // Fallback without MOD_NOREPEAT
                success = RegisterHotKey(_hwnd, HOTKEY_ID, modifiers, vk);
            }

            if (success)
            {
                IsRegistered = true;
                LastError = null;
                return true;
            }
            else
            {
                IsRegistered = false;
                int errorCode = Marshal.GetLastWin32Error();
                LastError = errorCode switch
                {
                    1409 => "HotkeyInUse",
                    _ => $"Error_{errorCode}"
                };
                return false;
            }
        }

        public void Unregister()
        {
            if (IsRegistered && _hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, HOTKEY_ID);
                IsRegistered = false;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && (int)wParam == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Unregister();
                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource = null;
                }
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
