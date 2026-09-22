using AltPowerPlan.Models;
using AltPowerPlan.Services.Power.Native;
using AltPowerPlan.Services.Settings;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace AltPowerPlan.Services.Power
{
    public class PowerPlanService : IPowerPlanService, IDisposable
    {
        public static readonly Guid BalancedGuid = new("381b4222-f694-41f0-9685-ff5bb260df2e");
        public static readonly Guid HighPerformanceGuid = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        public static readonly Guid PowerSaverGuid = new("a1841308-3541-4fab-bc81-f71556f20b4a");
        public static readonly Guid UltimatePerformanceGuid = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

        public event EventHandler<PowerPlanModel?>? ActivePlanChanged;
        public event EventHandler<SystemPowerStatusModel>? PowerStatusChanged;

        private readonly IAppSettingsProvider _settingsProvider;
        private IntPtr _powerSchemeNotificationHandle = IntPtr.Zero;
        private IntPtr _acDcNotificationHandle = IntPtr.Zero;
        private HwndSource? _hwndSource;
        private bool _disposed;

        private readonly object _plansLock = new();
        private List<PowerPlanModel>? _cachedPlans;

        public PowerPlanService(IAppSettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider;
        }

        public SystemPowerStatusModel GetPowerStatus()
        {
            if (PowerNativeMethods.GetSystemPowerStatus(out var status))
            {
                bool hasBattery = (status.BatteryFlag & 128) == 0 && status.BatteryFlag != 255;
                bool isAc = status.ACLineStatus == 1;
                bool isCharging = (status.BatteryFlag & 8) != 0;
                byte percent = status.BatteryLifePercent;

                return new SystemPowerStatusModel
                {
                    HasBattery = hasBattery,
                    IsOnAcPower = isAc,
                    IsCharging = isCharging,
                    BatteryPercentage = percent <= 100 ? percent : (byte)0
                };
            }
            return new SystemPowerStatusModel { HasBattery = false, IsOnAcPower = true };
        }

        public IReadOnlyList<PowerPlanModel> GetPowerPlans(bool forceRefresh = false)
        {
            lock (_plansLock)
            {
                if (!forceRefresh && _cachedPlans != null)
                {
                    return _cachedPlans;
                }

                var plans = EnumeratePlansInternal();
                _cachedPlans = plans;
                return plans;
            }
        }

        private unsafe List<PowerPlanModel> EnumeratePlansInternal()
        {
            var plans = new List<PowerPlanModel>(8);
            var activeId = GetActiveSchemeGuid();
            var seenGuids = new HashSet<Guid>();

            // 1. Enumerate via standard PowerEnumerate API using stackalloc buffer
            uint index = 0;
            uint bufferSize = 16;
            byte* buffer = stackalloc byte[16];

            while (PowerNativeMethods.PowerEnumerate(
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                PowerNativeMethods.ACCESS_SCHEME,
                index,
                buffer,
                ref bufferSize) == PowerNativeMethods.ERROR_SUCCESS)
            {
                Guid schemeGuid = *(Guid*)buffer;
                if (seenGuids.Add(schemeGuid))
                {
                    AddPlanIfValid(plans, schemeGuid, activeId);
                }

                index++;
                bufferSize = 16;
            }

            // 2. Check HKLM PowerSchemes for custom / user-created power plans that Windows 11 Modern Standby
            // hides from PowerEnumerate/powercfg when not active.
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        if (!Guid.TryParse(subKeyName, out var schemeGuid) || seenGuids.Contains(schemeGuid))
                        {
                            continue;
                        }

                        // Uninstalled system templates (Ultimate, High Performance, Power Saver) are dormant
                        // unless enumerated by PowerEnumerate/powercfg or currently active.
                        if (IsUninstalledTemplate(schemeGuid, activeId))
                        {
                            continue;
                        }

                        if (seenGuids.Add(schemeGuid))
                        {
                            AddPlanIfValid(plans, schemeGuid, activeId);
                        }
                    }
                }
            }
            catch { }

            // 3. Fallback: Only spawn powercfg.exe if native Win32 and Registry enumeration found no plans
            if (plans.Count == 0)
            {
                var powercfgGuids = EnumerateViaPowercfg();
                foreach (var schemeGuid in powercfgGuids)
                {
                    if (seenGuids.Add(schemeGuid))
                    {
                        AddPlanIfValid(plans, schemeGuid, activeId);
                    }
                }
            }

            // 4. Always ensure the active plan is present
            if (activeId.HasValue && seenGuids.Add(activeId.Value))
            {
                AddPlanIfValid(plans, activeId.Value, activeId);
            }

            return plans;
        }

        private static bool IsUninstalledTemplate(Guid guid, Guid? activeId)
        {
            if (activeId.HasValue && activeId.Value == guid)
            {
                return false;
            }

            return guid == UltimatePerformanceGuid ||
                   guid == HighPerformanceGuid ||
                   guid == PowerSaverGuid;
        }

        private static List<Guid> EnumerateViaPowercfg()
        {
            var result = new List<Guid>();
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/list",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                if (p != null)
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    var matches = System.Text.RegularExpressions.Regex.Matches(
                        output,
                        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        if (Guid.TryParse(m.Value, out var g) && !result.Contains(g))
                        {
                            result.Add(g);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        private void AddPlanIfValid(List<PowerPlanModel> plans, Guid schemeGuid, Guid? activeId)
        {
            string name = ReadSchemeFriendlyName(schemeGuid);
            string description = ReadSchemeDescription(schemeGuid);

            if (string.IsNullOrWhiteSpace(name))
            {
                name = GetFallbackName(schemeGuid);
            }

            // Filter out Windows internal overlays (e.g. "Better Battery-life Overlay", "Max Performance Overlay")
            if (name.Contains("Overlay", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("Overlay", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var type = DetermineType(schemeGuid, name);
            bool isActive = activeId.HasValue && activeId.Value == schemeGuid;

            plans.Add(new PowerPlanModel
            {
                Id = schemeGuid,
                Name = name,
                Description = description,
                Type = type,
                IsActive = isActive
            });
        }

        public PowerPlanModel? GetActivePowerPlan()
        {
            var activeGuid = GetActiveSchemeGuid();
            if (!activeGuid.HasValue)
                return null;

            string name = ReadSchemeFriendlyName(activeGuid.Value);
            string description = ReadSchemeDescription(activeGuid.Value);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = GetFallbackName(activeGuid.Value);
            }

            return new PowerPlanModel
            {
                Id = activeGuid.Value,
                Name = name,
                Description = description,
                Type = DetermineType(activeGuid.Value, name),
                IsActive = true
            };
        }

        public bool SetActivePowerPlan(Guid planGuid)
        {
            uint result = PowerNativeMethods.PowerSetActiveScheme(IntPtr.Zero, ref planGuid);
            if (result != PowerNativeMethods.ERROR_SUCCESS)
            {
                try
                {
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = $"/setactive {planGuid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    p?.WaitForExit(3000);
                }
                catch { }
            }

            var active = GetActivePowerPlan();
            bool isCurrent = active != null && active.Id == planGuid;
            if (isCurrent || result == PowerNativeMethods.ERROR_SUCCESS)
            {
                lock (_plansLock)
                {
                    if (_cachedPlans != null)
                    {
                        foreach (var p in _cachedPlans)
                        {
                            p.IsActive = (p.Id == planGuid);
                        }
                    }
                }

                ActivePlanChanged?.Invoke(this, active);
                return true;
            }
            return false;
        }

        public Guid? CreatePowerPlan(Guid sourcePlanGuid, string name, string? description = null)
        {
            Guid? newGuid = null;

            // 1. Try native Win32 PowerDuplicateScheme first if sourcePlanGuid is installed on this system
            uint result = PowerNativeMethods.PowerDuplicateScheme(IntPtr.Zero, ref sourcePlanGuid, out IntPtr newGuidPtr);
            if (result == PowerNativeMethods.ERROR_SUCCESS && newGuidPtr != IntPtr.Zero)
            {
                try
                {
                    unsafe
                    {
                        newGuid = *(Guid*)newGuidPtr;
                    }
                }
                finally
                {
                    PowerNativeMethods.LocalFree(newGuidPtr);
                }
            }

            // 2. If sourcePlanGuid is a preset, try built-in powercfg aliases
            if (!newGuid.HasValue)
            {
                if (sourcePlanGuid == PowerSaverGuid)
                {
                    newGuid = DuplicateViaPowercfg("SCHEME_MAX");
                }
                else if (sourcePlanGuid == HighPerformanceGuid)
                {
                    newGuid = DuplicateViaPowercfg("SCHEME_MIN");
                }
                else if (sourcePlanGuid == BalancedGuid)
                {
                    newGuid = DuplicateViaPowercfg("SCHEME_BALANCED");
                }
            }

            // 3. Try duplicating via powercfg with raw GUID string
            if (!newGuid.HasValue)
            {
                newGuid = DuplicateViaPowercfg(sourcePlanGuid.ToString());
            }

            // 4. Fallback: if template cannot be duplicated (e.g. Modern Standby S0 blocks Power Saver / High Performance templates),
            // duplicate the current active plan or any available installed plan
            if (!newGuid.HasValue)
            {
                var candidates = new List<Guid>();
                var activeId = GetActiveSchemeGuid();
                if (activeId.HasValue && activeId.Value != sourcePlanGuid)
                {
                    candidates.Add(activeId.Value);
                }

                var existingPlans = GetPowerPlans();
                foreach (var p in existingPlans)
                {
                    if (p.Id != sourcePlanGuid && !candidates.Contains(p.Id))
                    {
                        candidates.Add(p.Id);
                    }
                }

                if (sourcePlanGuid != BalancedGuid && !candidates.Contains(BalancedGuid))
                {
                    candidates.Add(BalancedGuid);
                }

                foreach (var candidate in candidates)
                {
                    var cand = candidate;
                    result = PowerNativeMethods.PowerDuplicateScheme(IntPtr.Zero, ref cand, out newGuidPtr);
                    if (result == PowerNativeMethods.ERROR_SUCCESS && newGuidPtr != IntPtr.Zero)
                    {
                        try
                        {
                            unsafe
                            {
                                newGuid = *(Guid*)newGuidPtr;
                            }
                            break;
                        }
                        finally
                        {
                            PowerNativeMethods.LocalFree(newGuidPtr);
                        }
                    }

                    newGuid = DuplicateViaPowercfg(candidate.ToString());
                    if (newGuid.HasValue)
                    {
                        break;
                    }
                }
            }

            // 5. Elevated fallback: in case standard user permissions are restricted on this device
            if (!newGuid.HasValue)
            {
                if (sourcePlanGuid == PowerSaverGuid)
                {
                    newGuid = DuplicateViaElevatedPowercfg("SCHEME_MAX") ?? DuplicateViaElevatedPowercfg(PowerSaverGuid.ToString());
                }
                else if (sourcePlanGuid == HighPerformanceGuid)
                {
                    newGuid = DuplicateViaElevatedPowercfg("SCHEME_MIN") ?? DuplicateViaElevatedPowercfg(HighPerformanceGuid.ToString());
                }
                else if (sourcePlanGuid == BalancedGuid)
                {
                    newGuid = DuplicateViaElevatedPowercfg("SCHEME_BALANCED");
                }

                if (!newGuid.HasValue)
                {
                    var activeId = GetActiveSchemeGuid() ?? BalancedGuid;
                    newGuid = DuplicateViaElevatedPowercfg(activeId.ToString());
                }
            }

            if (newGuid.HasValue)
            {
                Guid guid = newGuid.Value;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    byte[] nameBytes = Encoding.Unicode.GetBytes(name + "\0");
                    PowerNativeMethods.PowerWriteFriendlyName(
                        IntPtr.Zero,
                        ref guid,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        nameBytes,
                        (uint)nameBytes.Length);

                    try
                    {
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                            $@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{guid}", writable: true);
                        key?.SetValue("FriendlyName", name);
                    }
                    catch { }

                    try
                    {
                        string descArg = string.IsNullOrWhiteSpace(description) ? "" : $" \"{description}\"";
                        using var p = Process.Start(new ProcessStartInfo
                        {
                            FileName = "powercfg.exe",
                            Arguments = $"-changename {guid} \"{name}\"{descArg}",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        p?.WaitForExit(3000);
                    }
                    catch { }
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    byte[] descBytes = Encoding.Unicode.GetBytes(description + "\0");
                    PowerNativeMethods.PowerWriteDescription(
                        IntPtr.Zero,
                        ref guid,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        descBytes,
                        (uint)descBytes.Length);

                    try
                    {
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                            $@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{guid}", writable: true);
                        key?.SetValue("Description", description);
                    }
                    catch { }
                }

                // If user created a Power Saver plan (especially when duplicated from Balanced on Modern Standby),
                // configure energy-saving CPU throttling and timeouts
                if (sourcePlanGuid == PowerSaverGuid || DetermineType(guid, name) == PowerPlanType.PowerSaver)
                {
                    RunPowercfgNoWindow($"/setdcvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMAX 50");
                    RunPowercfgNoWindow($"/setacvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMAX 80");
                    RunPowercfgNoWindow($"/setdcvalueindex {guid} SUB_VIDEO VIDEOIDLE 180");
                    RunPowercfgNoWindow($"/setdcvalueindex {guid} SUB_SLEEP STANDBYIDLE 300");
                }

                lock (_plansLock)
                {
                    _cachedPlans = null;
                }

                return guid;
            }

            return null;
        }

        private static void RunPowercfgNoWindow(string arguments)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit(2000);
            }
            catch { }
        }

        public Guid? DuplicatePowerPlan(Guid sourcePlanGuid, string? newName = null)
        {
            string name = newName ?? $"{ReadSchemeFriendlyName(sourcePlanGuid)} - Copy";
            string desc = ReadSchemeDescription(sourcePlanGuid);
            return CreatePowerPlan(sourcePlanGuid, name, desc);
        }

        public async Task<bool> ExportPowerPlanAsync(Guid planGuid, string targetFilePath)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = $"-export \"{targetFilePath}\" {planGuid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync().ConfigureAwait(false);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Guid?> ImportPowerPlanAsync(string sourceFilePath)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = $"-import \"{sourceFilePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await process.WaitForExitAsync().ConfigureAwait(false);

                var match = System.Text.RegularExpressions.Regex.Match(
                    output,
                    @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

                if (match.Success && Guid.TryParse(match.Value, out var guid))
                {
                    lock (_plansLock)
                    {
                        _cachedPlans = null;
                    }
                    return guid;
                }
            }
            catch
            {
            }
            return null;
        }

        public async Task<bool> RestoreDefaultSchemesAsync()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = "-restoredefaultschemes",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync().ConfigureAwait(false);

                lock (_plansLock)
                {
                    _cachedPlans = null;
                }
                var active = GetActivePowerPlan();
                ActivePlanChanged?.Invoke(this, active);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static Guid? DuplicateViaPowercfg(Guid sourceGuid) => DuplicateViaPowercfg(sourceGuid.ToString());

        private static Guid? DuplicateViaPowercfg(string schemeArg)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"-duplicatescheme {schemeArg}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = new Process { StartInfo = psi };
                var outputSb = new StringBuilder();
                process.OutputDataReceived += (s, e) => { if (e.Data != null) outputSb.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(); } catch { }
                    return null;
                }

                if (process.ExitCode == 0)
                {
                    string output = outputSb.ToString();
                    var match = System.Text.RegularExpressions.Regex.Match(
                        output,
                        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

                    if (match.Success && Guid.TryParse(match.Value, out var guid))
                    {
                        return guid;
                    }
                }
            }
            catch
            {
                // Fallback failed
            }

            return null;
        }

        private static Guid? DuplicateViaElevatedPowercfg(string schemeArg)
        {
            try
            {
                string tempOut = Path.Combine(Path.GetTempPath(), $"altpowerplan_dup_{Guid.NewGuid():N}.txt");
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c powercfg -duplicatescheme {schemeArg} > \"{tempOut}\" 2>&1",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(psi);
                if (process == null) return null;
                process.WaitForExit(10000);

                if (File.Exists(tempOut))
                {
                    string output = File.ReadAllText(tempOut);
                    try { File.Delete(tempOut); } catch { }

                    var match = System.Text.RegularExpressions.Regex.Match(
                        output,
                        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

                    if (match.Success && Guid.TryParse(match.Value, out var guid))
                    {
                        return guid;
                    }
                }
            }
            catch
            {
                // Elevation cancelled or failed
            }

            return null;
        }

        public bool DeletePowerPlan(Guid planGuid)
        {
            var activeId = GetActiveSchemeGuid();
            if (activeId.HasValue && activeId.Value == planGuid)
            {
                return false;
            }

            uint result = PowerNativeMethods.PowerDeleteScheme(IntPtr.Zero, ref planGuid);
            if (result != PowerNativeMethods.ERROR_SUCCESS)
            {
                try
                {
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = $"/delete {planGuid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    p?.WaitForExit(3000);
                    result = (p?.ExitCode == 0) ? PowerNativeMethods.ERROR_SUCCESS : result;
                }
                catch { }
            }

            if (result == PowerNativeMethods.ERROR_SUCCESS)
            {
                lock (_plansLock)
                {
                    _cachedPlans = null;
                }
                return true;
            }

            return false;
        }

        public bool UpdatePowerPlan(Guid planGuid, string name, string? description = null)
        {
            bool success = false;

            if (!string.IsNullOrWhiteSpace(name))
            {
                byte[] nameBytes = Encoding.Unicode.GetBytes(name + "\0");
                uint result = PowerNativeMethods.PowerWriteFriendlyName(
                    IntPtr.Zero,
                    ref planGuid,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    nameBytes,
                    (uint)nameBytes.Length);
                success = result == PowerNativeMethods.ERROR_SUCCESS;
            }

            if (description != null)
            {
                byte[] descBytes = Encoding.Unicode.GetBytes(description + "\0");
                uint result = PowerNativeMethods.PowerWriteDescription(
                    IntPtr.Zero,
                    ref planGuid,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    descBytes,
                    (uint)descBytes.Length);
                if (result == PowerNativeMethods.ERROR_SUCCESS)
                {
                    success = true;
                }
            }

            if (success)
            {
                lock (_plansLock)
                {
                    _cachedPlans = null;
                }
            }

            return success;
        }

        public void OpenWindowsEditPlanSettings(Guid? planGuid = null)
        {
            try
            {
                var active = GetActiveSchemeGuid();
                if (planGuid.HasValue && active.HasValue && planGuid.Value == active.Value)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "control.exe",
                        Arguments = "/name Microsoft.PowerOptions /page pagePlanSettings",
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "control.exe",
                        Arguments = "/name Microsoft.PowerOptions",
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
                OpenWindowsPowerOptions();
            }
        }

        public void OpenWindowsPowerOptions()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = "/name Microsoft.PowerOptions",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore error if control panel cannot be opened
            }
        }

        public void RegisterNotification(IntPtr hwnd)
        {
            UnregisterNotification();

            if (hwnd == IntPtr.Zero)
                return;

            Guid powerSchemeGuid = PowerNativeMethods.GUID_POWERSCHEME_PERSONALITY;
            _powerSchemeNotificationHandle = PowerNativeMethods.RegisterPowerSettingNotification(
                hwnd,
                ref powerSchemeGuid,
                PowerNativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);

            Guid acDcGuid = PowerNativeMethods.GUID_ACDC_POWER_SOURCE;
            _acDcNotificationHandle = PowerNativeMethods.RegisterPowerSettingNotification(
                hwnd,
                ref acDcGuid,
                PowerNativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);

            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        public void UnregisterNotification()
        {
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            if (_powerSchemeNotificationHandle != IntPtr.Zero)
            {
                PowerNativeMethods.UnregisterPowerSettingNotification(_powerSchemeNotificationHandle);
                _powerSchemeNotificationHandle = IntPtr.Zero;
            }

            if (_acDcNotificationHandle != IntPtr.Zero)
            {
                PowerNativeMethods.UnregisterPowerSettingNotification(_acDcNotificationHandle);
                _acDcNotificationHandle = IntPtr.Zero;
            }
        }

        private SystemPowerStatusModel? _lastPowerStatus;

        private unsafe IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == PowerNativeMethods.WM_POWERBROADCAST && (int)wParam == PowerNativeMethods.PBT_POWERSETTINGCHANGE)
            {
                if (lParam != IntPtr.Zero)
                {
                    var setting = *(PowerNativeMethods.POWERBROADCAST_SETTING*)lParam;
                    if (setting.PowerSetting == PowerNativeMethods.GUID_POWERSCHEME_PERSONALITY)
                    {
                        var active = GetActivePowerPlan();
                        if (active != null)
                        {
                            bool changed = false;
                            lock (_plansLock)
                            {
                                if (_cachedPlans != null)
                                {
                                    foreach (var p in _cachedPlans)
                                    {
                                        bool isAct = (p.Id == active.Id);
                                        if (p.IsActive != isAct)
                                        {
                                            p.IsActive = isAct;
                                            changed = true;
                                        }
                                    }
                                }
                                else
                                {
                                    changed = true;
                                }
                            }

                            if (changed)
                            {
                                ActivePlanChanged?.Invoke(this, active);
                            }
                        }
                    }
                    else if (setting.PowerSetting == PowerNativeMethods.GUID_ACDC_POWER_SOURCE)
                    {
                        OnPowerSourceChanged();
                    }
                }
                else
                {
                    var active = GetActivePowerPlan();
                    ActivePlanChanged?.Invoke(this, active);
                }
            }
            return IntPtr.Zero;
        }

        private void OnPowerSourceChanged()
        {
            var status = GetPowerStatus();
            if (_lastPowerStatus != null &&
                _lastPowerStatus.IsOnAcPower == status.IsOnAcPower &&
                _lastPowerStatus.IsCharging == status.IsCharging &&
                _lastPowerStatus.BatteryPercentage == status.BatteryPercentage &&
                _lastPowerStatus.HasBattery == status.HasBattery)
            {
                return;
            }
            _lastPowerStatus = status;

            PowerStatusChanged?.Invoke(this, status);

            if (_settingsProvider.Settings.AutoSwitchAcDc)
            {
                if (status.IsOnAcPower && _settingsProvider.Settings.PreferredAcPlanGuid is Guid acPlan)
                {
                    SetActivePowerPlan(acPlan);
                }
                else if (!status.IsOnAcPower && _settingsProvider.Settings.PreferredDcPlanGuid is Guid dcPlan)
                {
                    SetActivePowerPlan(dcPlan);
                }
            }
        }

        private static unsafe Guid? GetActiveSchemeGuid()
        {
            uint result = PowerNativeMethods.PowerGetActiveScheme(IntPtr.Zero, out IntPtr activeGuidPtr);
            if (result == PowerNativeMethods.ERROR_SUCCESS && activeGuidPtr != IntPtr.Zero)
            {
                try
                {
                    return *(Guid*)activeGuidPtr;
                }
                finally
                {
                    PowerNativeMethods.LocalFree(activeGuidPtr);
                }
            }
            return null;
        }

        private static unsafe string ReadSchemeFriendlyName(Guid schemeGuid)
        {
            uint bufferSize = 512;
            Span<byte> stackBuffer = stackalloc byte[512];
            fixed (byte* pBuffer = stackBuffer)
            {
                uint result = PowerNativeMethods.PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pBuffer, ref bufferSize);
                if (result == PowerNativeMethods.ERROR_SUCCESS && bufferSize > 0)
                {
                    ReadOnlySpan<char> chars = MemoryMarshal.Cast<byte, char>(stackBuffer[..(int)(bufferSize & ~1)]).TrimEnd('\0');
                    if (!chars.Trim().IsEmpty)
                        return new string(chars);
                }
                else if (result == PowerNativeMethods.ERROR_MORE_DATA && bufferSize > 512)
                {
                    byte[] rented = ArrayPool<byte>.Shared.Rent((int)bufferSize);
                    try
                    {
                        fixed (byte* pRented = rented)
                        {
                            if (PowerNativeMethods.PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pRented, ref bufferSize) == PowerNativeMethods.ERROR_SUCCESS && bufferSize > 0)
                            {
                                ReadOnlySpan<char> chars = MemoryMarshal.Cast<byte, char>(rented.AsSpan(0, (int)(bufferSize & ~1))).TrimEnd('\0');
                                if (!chars.Trim().IsEmpty)
                                    return new string(chars);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }

            // Fallback: Read from registry
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{schemeGuid}");
                if (key?.GetValue("FriendlyName") is string regName && !string.IsNullOrWhiteSpace(regName))
                {
                    return CleanRegistryString(regName);
                }
            }
            catch { }

            return string.Empty;
        }

        private static unsafe string ReadSchemeDescription(Guid schemeGuid)
        {
            uint bufferSize = 512;
            Span<byte> stackBuffer = stackalloc byte[512];
            fixed (byte* pBuffer = stackBuffer)
            {
                uint result = PowerNativeMethods.PowerReadDescription(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pBuffer, ref bufferSize);
                if (result == PowerNativeMethods.ERROR_SUCCESS && bufferSize > 0)
                {
                    ReadOnlySpan<char> chars = MemoryMarshal.Cast<byte, char>(stackBuffer[..(int)(bufferSize & ~1)]).TrimEnd('\0');
                    if (!chars.Trim().IsEmpty)
                        return new string(chars);
                }
                else if (result == PowerNativeMethods.ERROR_MORE_DATA && bufferSize > 512)
                {
                    byte[] rented = ArrayPool<byte>.Shared.Rent((int)bufferSize);
                    try
                    {
                        fixed (byte* pRented = rented)
                        {
                            if (PowerNativeMethods.PowerReadDescription(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pRented, ref bufferSize) == PowerNativeMethods.ERROR_SUCCESS && bufferSize > 0)
                            {
                                ReadOnlySpan<char> chars = MemoryMarshal.Cast<byte, char>(rented.AsSpan(0, (int)(bufferSize & ~1))).TrimEnd('\0');
                                if (!chars.Trim().IsEmpty)
                                    return new string(chars);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }

            // Fallback: Read from registry
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{schemeGuid}");
                if (key?.GetValue("Description") is string regDesc && !string.IsNullOrWhiteSpace(regDesc))
                {
                    return CleanRegistryString(regDesc);
                }
            }
            catch { }

            return string.Empty;
        }

        private static string CleanRegistryString(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return string.Empty;

            ReadOnlySpan<char> span = val.AsSpan().Trim();
            if (span.StartsWith("@"))
            {
                // Registry strings are formatted as: @<module>,-<resourceId>[,<fallbackText>]
                int firstComma = span.IndexOf(',');
                if (firstComma >= 0)
                {
                    int secondComma = span[(firstComma + 1)..].IndexOf(',');
                    if (secondComma >= 0)
                    {
                        int actualSecondComma = firstComma + 1 + secondComma;
                        if (actualSecondComma < span.Length - 1)
                        {
                            ReadOnlySpan<char> fallback = span[(actualSecondComma + 1)..].Trim();
                            if (!fallback.IsEmpty)
                                return new string(fallback);
                        }
                    }
                    else if (firstComma < span.Length - 1)
                    {
                        ReadOnlySpan<char> candidate = span[(firstComma + 1)..].Trim();
                        if (!int.TryParse(candidate, out _))
                        {
                            return new string(candidate);
                        }
                    }
                }
            }
            return span.Length == val.Length ? val : new string(span);
        }

        private static PowerPlanType DetermineType(Guid guid, string name)
        {
            if (guid == BalancedGuid) return PowerPlanType.Balanced;
            if (guid == HighPerformanceGuid) return PowerPlanType.HighPerformance;
            if (guid == PowerSaverGuid) return PowerPlanType.PowerSaver;
            if (guid == UltimatePerformanceGuid) return PowerPlanType.UltimatePerformance;

            ReadOnlySpan<char> span = name.AsSpan();

            if (span.Contains("ultimate", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("максимальн", StringComparison.OrdinalIgnoreCase))
                return PowerPlanType.UltimatePerformance;

            if (span.Contains("high performance", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("висок", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("продуктивн", StringComparison.OrdinalIgnoreCase))
                return PowerPlanType.HighPerformance;

            if (span.Contains("saver", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("saving", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("економ", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("заощадж", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("battery", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("акумулятор", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("батаре", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("енергозбереж", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("eco", StringComparison.OrdinalIgnoreCase))
                return PowerPlanType.PowerSaver;

            if (span.Contains("balanced", StringComparison.OrdinalIgnoreCase) ||
                span.Contains("збаланс", StringComparison.OrdinalIgnoreCase))
                return PowerPlanType.Balanced;

            return PowerPlanType.Custom;
        }

        private static string GetFallbackName(Guid guid)
        {
            if (guid == BalancedGuid) return "Balanced";
            if (guid == HighPerformanceGuid) return "High performance";
            if (guid == PowerSaverGuid) return "Power saver";
            if (guid == UltimatePerformanceGuid) return "Ultimate Performance";
            Span<char> guidChars = stackalloc char[36];
            if (guid.TryFormat(guidChars, out _))
            {
                return string.Concat("Power Plan (", guidChars[..8], ")");
            }
            return "Power Plan";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnregisterNotification();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
