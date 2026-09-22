using AltPowerPlan.Models;
using System;

namespace AltPowerPlan.Services.Hotkey
{
    public interface IHotkeyService : IDisposable
    {
        event Action? HotkeyPressed;

        HotkeyModel CurrentHotkey { get; }

        bool IsRegistered { get; }

        string? LastError { get; }

        void Initialize(IntPtr hwnd);

        bool UpdateHotkey(HotkeyModel hotkey);

        void Unregister();
    }
}
