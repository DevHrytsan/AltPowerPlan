using System;
using System.Runtime.InteropServices;

namespace AltPowerPlan.Services.Power.Native
{
    internal static class PowerNativeMethods
    {
        public const uint ACCESS_SCHEME = 16; // 0x10
        public const uint ERROR_SUCCESS = 0;
        public const uint ERROR_MORE_DATA = 234;
        public const uint ERROR_NO_MORE_ITEMS = 259;

        public const int WM_POWERBROADCAST = 0x0218;
        public const int PBT_POWERSETTINGCHANGE = 0x8013;
        public const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

        public static readonly Guid GUID_POWERSCHEME_PERSONALITY = new("245d8241-2611-420e-b7e3-34015037d67c");
        public static readonly Guid GUID_ACDC_POWER_SOURCE = new("5d3e9a59-e9d5-4b00-a6bd-ff34ff516548");

        [StructLayout(LayoutKind.Sequential)]
        public struct POWERBROADCAST_SETTING
        {
            public Guid PowerSetting;
            public uint DataLength;
            public byte Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;         // 0: Offline, 1: Online, 255: Unknown
            public byte BatteryFlag;          // 1: High, 2: Low, 4: Critical, 8: Charging, 128: No Battery, 255: Unknown
            public byte BatteryLifePercent;    // 0-100%, 255: Unknown
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern unsafe uint PowerEnumerate(
            IntPtr rootPowerKey,
            IntPtr schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            uint accessFlags,
            uint index,
            byte* buffer,
            ref uint bufferSize);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerGetActiveScheme(
            IntPtr userRootPowerKey,
            out IntPtr activePolicyGuid);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerSetActiveScheme(
            IntPtr userRootPowerKey,
            ref Guid schemeGuid);

        [DllImport("powrprof.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern unsafe uint PowerReadFriendlyName(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            IntPtr powerSettingGuid,
            byte* buffer,
            ref uint bufferSize);

        [DllImport("powrprof.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern unsafe uint PowerReadDescription(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            IntPtr powerSettingGuid,
            byte* buffer,
            ref uint bufferSize);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerDuplicateScheme(
            IntPtr rootPowerKey,
            ref Guid sourceSchemeGuid,
            out IntPtr destinationSchemeGuid);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerDeleteScheme(
            IntPtr rootPowerKey,
            ref Guid schemeGuid);

        [DllImport("powrprof.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint PowerWriteFriendlyName(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            IntPtr powerSettingGuid,
            byte[] buffer,
            uint bufferSize);

        [DllImport("powrprof.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint PowerWriteDescription(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            IntPtr powerSettingGuid,
            byte[] buffer,
            uint bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr RegisterPowerSettingNotification(
            IntPtr hRecipient,
            ref Guid powerSettingGuid,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);
    }
}
