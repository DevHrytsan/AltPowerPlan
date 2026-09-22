using AltPowerPlan.Models;
using System;
using System.Collections.Generic;

namespace AltPowerPlan.Services.Power
{
    public interface IPowerPlanService
    {
        /// <summary>
        /// Triggered when the active Windows power plan changes (either internally or externally).
        /// </summary>
        event EventHandler<PowerPlanModel?>? ActivePlanChanged;

        /// <summary>
        /// Triggered when the system power source (AC vs Battery) or battery level changes.
        /// </summary>
        event EventHandler<SystemPowerStatusModel>? PowerStatusChanged;

        /// <summary>
        /// Retrieves all power plans installed on the system.
        /// </summary>
        IReadOnlyList<PowerPlanModel> GetPowerPlans(bool forceRefresh = false);

        /// <summary>
        /// Retrieves the currently active power plan.
        /// </summary>
        PowerPlanModel? GetActivePowerPlan();

        /// <summary>
        /// Retrieves current battery and AC line power status.
        /// </summary>
        SystemPowerStatusModel GetPowerStatus();

        /// <summary>
        /// Sets the specified power plan as active in Windows.
        /// </summary>
        bool SetActivePowerPlan(Guid planGuid);

        /// <summary>
        /// Creates a new power plan by duplicating an existing scheme and setting its name and description.
        /// </summary>
        Guid? CreatePowerPlan(Guid sourcePlanGuid, string name, string? description = null);

        /// <summary>
        /// Duplicates an existing power plan with an optional new name.
        /// </summary>
        Guid? DuplicatePowerPlan(Guid sourcePlanGuid, string? newName = null);

        /// <summary>
        /// Exports a power plan to a .pow file asynchronously.
        /// </summary>
        Task<bool> ExportPowerPlanAsync(Guid planGuid, string targetFilePath);

        /// <summary>
        /// Imports a power plan from a .pow file asynchronously.
        /// </summary>
        Task<Guid?> ImportPowerPlanAsync(string sourceFilePath);

        /// <summary>
        /// Restores default Windows power schemes asynchronously.
        /// </summary>
        Task<bool> RestoreDefaultSchemesAsync();

        /// <summary>
        /// Deletes an existing power plan from the system.
        /// </summary>
        bool DeletePowerPlan(Guid planGuid);

        /// <summary>
        /// Updates the friendly name and description of an existing power plan.
        /// </summary>
        bool UpdatePowerPlan(Guid planGuid, string name, string? description = null);

        /// <summary>
        /// Opens the Windows Edit Plan Settings window in Control Panel (for the active plan, or activates target first).
        /// </summary>
        void OpenWindowsEditPlanSettings(Guid? planGuid = null);

        /// <summary>
        /// Opens the main Windows Power Options applet in Control Panel.
        /// </summary>
        void OpenWindowsPowerOptions();

        /// <summary>
        /// Registers a window handle to receive WM_POWERBROADCAST notifications from the OS.
        /// </summary>
        void RegisterNotification(IntPtr hwnd);

        /// <summary>
        /// Unregisters notification listeners.
        /// </summary>
        void UnregisterNotification();
    }
}
