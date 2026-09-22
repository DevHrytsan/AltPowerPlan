using System;
using System.Windows;

namespace AltPowerPlan.Services.Tray
{
    public interface ITrayService : IDisposable
    {
        /// <summary>
        /// Initializes the system tray icon associated with the given window.
        /// </summary>
        void Initialize(Window window);

        /// <summary>
        /// Updates the tray icon tooltip.
        /// </summary>
        void UpdateTooltip(string text);

        /// <summary>
        /// Removes the tray icon from the Windows notification area.
        /// </summary>
        void Remove();
    }
}
