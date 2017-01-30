﻿using MSHU.CarWash.UWP.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Popups;

namespace MSHU.CarWash.UWP.Services
{
    class WhatsNewService : IWhatsNew
    {
        private const string whatsNewKey = "WhatsNewSeenForAppVersion";
        private ISettingsStore settingsStore;

        public WhatsNewService(ISettingsStore settingsStore)
        {
            this.settingsStore = settingsStore;
        }
        public Task FetchWhatsNewAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<bool> IsSomethingNewAsync()
        {
            var value = await settingsStore.TryRetrieveSettingAsync<string>(whatsNewKey);
            if (value != null)
            {
                var version = Package.Current.Id.Version;
                return VersionToString(version) != value;
            }

            return true;
        }

        private static string VersionToString(PackageVersion version)
        {
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        public async Task ShowWhatsNewAsync()
        {
            var value = await settingsStore.TryRetrieveSettingAsync<string>(whatsNewKey);
            if (value == null)
            {
                value = "0.0.0";
            }
            var version = VersionToString(Package.Current.Id.Version);

            var changes = new[] 
            {
                new { version = "1.4.7", changes = new [] { "Support for managing car wash reservations in your calendar." } }
            };

            var changeList = new StringBuilder("Here's what's new:\r\n");
            foreach(var versionChange in changes)
            {
                if(String.Compare(versionChange.version, value) > 0)
                {
                    foreach(var change in versionChange.changes)
                    {
                        changeList.AppendLine($"• {change}");
                    }
                }
            }

            var dialog = new MessageDialog(changeList.ToString());
            dialog.Title = "Carwash has just got updated!";
            dialog.Commands.Add(new UICommand { Label = "Ok", Id = 0 });
            var res = await dialog.ShowAsync();

            await settingsStore.StoreSettingAsync(whatsNewKey, VersionToString(Package.Current.Id.Version));
        }
    }
}
