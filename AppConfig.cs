using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Win32;

namespace LabTools
{
    public class MediaSizeRule
    {
        public string Name { get; set; } = "";
        public bool IsMultiBlock { get; set; } = false;
        public string Block1Size { get; set; } = "";
        public string Block1PrintName { get; set; } = "";
        public string Block2Size { get; set; } = "";
        public string Block2PrintName { get; set; } = "";
    }

    public class AppConfig
    {
        private static readonly string RegistryKeyPath = @"SOFTWARE\LabTools";
        private static readonly string DefaultMasterFolder = @"Z:\PhotoLab\EHM\GIL";

        // Per-PC Settings (Never overwritten by other PCs)
        public bool HybridShip { get; set; } = false;
        public string MasterSyncFolder { get; set; } = DefaultMasterFolder;

        // Shared Lab Settings (Synced via Master File)
        public string CurrentVoyage { get; set; } = "PO101126";
        public string PrintedFolder { get; set; } = @"Z:\Printed";
        public string OrdersFolder { get; set; } = @"Z:\PixelsOrder\Processed";
        public string DownloadsFolder { get; set; } = @"Z:\PixelsOrder\Downloads";
        public string ToPrintFolder { get; set; } = @"Z:\ToPrint";
        public string BorderedPrintsFolder { get; set; } = @"Z:\PixelsOrder\Prints";
        public string PrintshopFolder { get; set; } = @"Z:\PhotoLab\Printshop";
        public string HubAppCanvasFolder { get; set; } = @"Z:\PhotoLab\HubCanvas";
        public string CopyrightFolder { get; set; } = @"Z:\PhotoLab\Copyright";
        public string CCFolder { get; set; } = @"Z:\CC";
        public string DestinationForWhiteImages { get; set; } = @"Z:\PhotoLab\Misc";
        public string MachineA { get; set; } = @"Z:\PhotoLab\Misc\A";
        public string MachineB { get; set; } = @"Z:\PhotoLab\Misc\B";
        public string MachineC { get; set; } = @"Z:\PhotoLab\Misc\C";
        public string MachineD { get; set; } = @"Z:\PhotoLab\Misc\D";

        public List<string> MediaSizes { get; set; } = new List<string>(MainForm.DefaultMediaSizes);
        public List<MediaSizeRule> MediaSizeRules { get; set; } = new List<MediaSizeRule>();

        public MediaSizeRule GetEffectiveRule(string sizeName)
        {
            if (MediaSizeRules != null)
            {
                var match = MediaSizeRules.Find(r => r.Name.Equals(sizeName, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            bool isMulti = sizeName.IndexOf(" and ", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isMulti)
            {
                var parts = RegexSplitSize(sizeName);
                return new MediaSizeRule
                {
                    Name = sizeName,
                    IsMultiBlock = true,
                    Block1Size = parts.Item1,
                    Block1PrintName = parts.Item1,
                    Block2Size = parts.Item2,
                    Block2PrintName = parts.Item2
                };
            }

            return new MediaSizeRule
            {
                Name = sizeName,
                IsMultiBlock = false,
                Block1Size = sizeName,
                Block1PrintName = sizeName,
                Block2Size = "",
                Block2PrintName = ""
            };
        }

        private static (string, string) RegexSplitSize(string combined)
        {
            int idx = combined.IndexOf(" and ", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return (combined.Substring(0, idx).Trim(), combined.Substring(idx + 5).Trim());
            }
            return (combined, "4-Wallets (vert)");
        }

        private static string GetMasterFilePath(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "MasterSync.json");
                if (File.Exists(path))
                {
                    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden | FileAttributes.System);
                }
                return path;
            }
            catch
            {
                return "MasterSync.json";
            }
        }

        public static string GetRegistryMasterFolder()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                if (key != null)
                {
                    string? val = key.GetValue("MasterSyncFolder") as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
            catch { }
            return DefaultMasterFolder;
        }

        public static void SetRegistryMasterFolder(string folder)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                key?.SetValue("MasterSyncFolder", folder);
            }
            { }
        }

        public static AppConfig Load()
        {
            string masterFolder = GetRegistryMasterFolder();
            string path = GetMasterFilePath(masterFolder);
            AppConfig config = new AppConfig { MasterSyncFolder = masterFolder };

            try
            {
                if (File.Exists(path))
                {
                    string json = Program.SafeReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                    if (loaded != null)
                    {
                        config.CurrentVoyage = loaded.CurrentVoyage;
                        config.PrintedFolder = loaded.PrintedFolder;
                        config.OrdersFolder = loaded.OrdersFolder;
                        config.DownloadsFolder = loaded.DownloadsFolder;
                        config.ToPrintFolder = loaded.ToPrintFolder;
                        config.BorderedPrintsFolder = loaded.BorderedPrintsFolder;
                        config.PrintshopFolder = loaded.PrintshopFolder;
                        config.HubAppCanvasFolder = loaded.HubAppCanvasFolder;
                        config.CopyrightFolder = loaded.CopyrightFolder;
                        config.CCFolder = loaded.CCFolder;
                        config.DestinationForWhiteImages = loaded.DestinationForWhiteImages;
                        config.MachineA = loaded.MachineA;
                        config.MachineB = loaded.MachineB;
                        config.MachineC = loaded.MachineC;
                        config.MachineD = loaded.MachineD;
                        if (loaded.MediaSizes != null && loaded.MediaSizes.Count > 0) config.MediaSizes = loaded.MediaSizes;
                        if (loaded.MediaSizeRules != null) config.MediaSizeRules = loaded.MediaSizeRules;
                    }
                }
            }
            catch { }

            // Load per-PC settings from local app data or registry if needed
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath + "\\" + Environment.MachineName);
                if (key != null)
                {
                    if (key.GetValue("HybridShip") is int hs) config.HybridShip = (hs == 1);
                }
            }
            catch { }

            return config;
        }

        public void Save(bool syncSharedFolders = false)
        {
            SetRegistryMasterFolder(this.MasterSyncFolder);
            string path = GetMasterFilePath(this.MasterSyncFolder);

            try
            {
                AppConfig dataToSave = this;
                if (!syncSharedFolders && File.Exists(path))
                {
                    // Keep existing shared fields intact if we aren't pushing a sync
                    string existingJson = Program.SafeReadAllText(path);
                    var existing = JsonSerializer.Deserialize<AppConfig>(existingJson);
                    if (existing != null)
                    {
                        this.CurrentVoyage = existing.CurrentVoyage;
                        // Keep other shared paths unchanged unless sync is true
                    }
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                if (File.Exists(path))
                {
                    File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.Hidden & ~FileAttributes.System);
                }
                Program.SafeWriteAllText(path, json);
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden | FileAttributes.System);
            }
            catch { }

            // Save per-PC settings locally
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath + "\\" + Environment.MachineName);
                key?.SetValue("HybridShip", this.HybridShip ? 1 : 0);
            }
            catch { }
        }
    }
}
