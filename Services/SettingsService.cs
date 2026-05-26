using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Entities;
using Recruit_Finder_AI.Models;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.ViewModels;

namespace Recruit_Finder_AI.Services
{
    public class SettingsService
    {
        private readonly Recruit_Finder_AIContext _context;
        private readonly ILogger<SettingsService> _logger;

        public SettingsService(Recruit_Finder_AIContext context, ILogger<SettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<AdminSettingsViewModel> GetAdminSettingsAsync()
        {
            var settingsDict = await _context.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value);

            var defaultSettings = new Dictionary<string, string>
            {
                { "PasswordExpirationDays", "30" },
                { "PasswordHistoryDepth", "5" },
                { "MinPasswordLength", "6" },
                { "EnableRegistration", "true" }
            };

            bool needsUpdate = false;
            foreach (var kvp in defaultSettings)
            {
                if (!settingsDict.ContainsKey(kvp.Key))
                {
                    _context.SystemSettings.Add(new SystemSetting
                    {
                        Key = kvp.Key,
                        Value = kvp.Value,
                        Description = $"Default {kvp.Key}"
                    });
                    settingsDict[kvp.Key] = kvp.Value;
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                await _context.SaveChangesAsync();
            }

            return new AdminSettingsViewModel
            {
                PasswordExpirationDays = int.TryParse(settingsDict["PasswordExpirationDays"], out int pDays) ? pDays : 30,
                PasswordHistoryDepth = int.TryParse(settingsDict["PasswordHistoryDepth"], out int pDepth) ? pDepth : 5,
                MinPasswordLength = int.TryParse(settingsDict["MinPasswordLength"], out int mLen) ? mLen : 6,
                EnableRegistration = bool.TryParse(settingsDict["EnableRegistration"], out bool eReg) ? eReg : true
            };
        }

        public async Task<bool> SaveAdminSettingsAsync(AdminSettingsViewModel model)
        {
            if (model.MinPasswordLength < 6) model.MinPasswordLength = 6;

            var settingsToUpdate = new Dictionary<string, string>
            {
                { "PasswordExpirationDays", model.PasswordExpirationDays.ToString() },
                { "PasswordHistoryDepth", model.PasswordHistoryDepth.ToString() },
                { "MinPasswordLength", model.MinPasswordLength.ToString() },
                { "EnableRegistration", model.EnableRegistration.ToString().ToLower() }
            };

            try
            {
                var keys = settingsToUpdate.Keys.ToList();
                var existingSettings = await _context.SystemSettings
                    .Where(s => keys.Contains(s.Key))
                    .ToListAsync();

                foreach (var item in settingsToUpdate)
                {
                    var setting = existingSettings.FirstOrDefault(s => s.Key == item.Key);

                    if (setting == null)
                    {
                        _context.SystemSettings.Add(new SystemSetting
                        {
                            Key = item.Key,
                            Value = item.Value,
                            Description = item.Key
                        });
                    }
                    else
                    {
                        if (setting.Value != item.Value)
                        {
                            setting.Value = item.Value;
                            _context.Entry(setting).State = EntityState.Modified;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving administrator settings");
                return false;
            }
        }
    }
}