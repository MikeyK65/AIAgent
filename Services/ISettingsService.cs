using AIAgent.Models;

namespace AIAgent.Services;

public interface ISettingsService
{
    AppSettings GetSettings();
    void SaveSettings(AppSettings settings);
}
