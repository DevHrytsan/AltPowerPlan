namespace AltPowerPlan.Services.Startup
{
    public interface IStartupService
    {
        bool IsStartupEnabled();
        bool SetStartup(bool enable, bool startMinimized = false);
    }
}
