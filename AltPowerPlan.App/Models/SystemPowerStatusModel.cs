namespace AltPowerPlan.Models
{
    public class SystemPowerStatusModel
    {
        public bool HasBattery { get; set; }
        public bool IsOnAcPower { get; set; }
        public bool IsCharging { get; set; }
        public byte BatteryPercentage { get; set; } // 0-100%

        public string DisplayText
        {
            get
            {
                if (!HasBattery)
                    return "AC Power";

                string state = IsCharging ? "Charging" : (IsOnAcPower ? "Plugged in" : "Battery");
                return $"{BatteryPercentage}% ({state})";
            }
        }
    }
}
