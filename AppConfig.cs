using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public class CustomTemplateItem
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool IsDual { get; set; } = false;
    }

    public class PcProfile
    {
        public bool HybridShip { get; set; } = false;
        public string SelectedMachine { get; set; } = "Machine C";
        public string UsbDrive { get; set; } = "D";
        public bool IsDarkMode { get; set; } = true;

        // Per-PC local folder overrides (used until a global Folder Sync is triggered)
        public longAdoptedSyncVersion { get; set; } = 0;
        public string? PrintedFolder { get; set; }
        public string? OrdersFolder { get; set; }
        public string? DownloadsFolder { get; set; }
        public string? ToPrintFolder { get; set; }
        public string? BorderedPrintsFolder { get; set; }
        public string? PrintshopFolder { get; set; }
        public string? HubAppCanvasFolder { get; set; }
        public string? CopyrightFolder { get; set; }
        public string? CCFolder { get; set; }
        public string? DestinationForWhiteImages { get; set; }
        public string? MachineA { get; set; }
        public string? MachineB { get; set; }
        public string? MachineC { get; set; }
        public string? MachineD { get; set; }
    }

    public class AppConfig
    {
        private const string RegistryKeyPath = @"Software\LabTools";
        public const string DefaultMasterSyncFolder = @"Z:\PhotoLab\EHM\GIL";
        public const string MasterFileName = "ActiveVoyage.txt";

        // Global Synced State
        public string CurrentVoyage { get; set; } = "PO101126";
        public int LastOrderId { get; set; } = 6000000;
        public long FolderSyncVersion { get; set; } = 1;

        // Shared Default / Synced Folder Paths
        public string PrintedFolder { get; set; } = @"Z:\Printed";
        public string OrdersFolder { get; set; } = @"Z:\PixelsOrder\Processed";
        public string DownloadsFolder { get; set; } = @"C:\Downloads";
        public string BorderedPrintsFolder { get; set; } = @"Z:\PixelsOrder\Prints";
        public string PrintshopFolder { get; set; } = @"Z:\PhotoLab\Printshop";
        public string HubAppCanvasFolder { get; set; } = @"Z:\PixelsOrder\HubAppCanvas";
        public string CopyrightFolder { get; set; } = @"Z:\PhotoLab\Misc";
        public string CCFolder { get; set; } = @"Z:\CC";
        public string DestinationForWhiteImages { get; set; } = @"Z:\WhiteImages";
        public string ToPrintFolder { get; set; } = @"Z:\ToPrint";
        public string MachineA { get; set; } = @"Z:\MachineA";
        public string MachineB { get; set; } = @"Z:\MachineB";
        public string MachineC { get; set; } = @"Z:\MachineC";
        public string MachineD { get; set; } = @"Z:\MachineD";

        public List<string> MediaSizes { get; set; } = new List<string>();
        public List<MediaSizeRule> MediaSizeRules { get; set; } = new List<MediaSizeRule>();
        public List<CustomTemplateItem> CustomTemplates { get; set; } = new List<CustomTemplateItem>();
        public Dictionary<string, PcProfile> PerPcSettings { get; set; } = new Dictionary<string, PcProfile>(StringComparer.OrdinalIgnoreCase);

        // Local Runtime Properties (Ignored in root JSON, mapped via PerPcSettings & Registry)
        [JsonIgnore]
        public string MasterSyncFolder { get; set; } = DefaultMasterSyncFolder;

        [JsonIgnore]
        public bool HybridShip { get; set; } = false;

        [JsonIgnore]
        public string SelectedMachine { get; set; } = "Machine C";

        [JsonIgnore]
        public string UsbDrive { get; set; } = "D";

        [JsonIgnore]
        public bool IsDarkMode { get; set; } = true;

        private static string PcKey => Environment.MachineName;

        public static string GetLocalMasterSyncFolder()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                string? val = key?.GetValue("MasterSyncFolder") as string;
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }
            catch { }
            return DefaultMasterSyncFolder;
        }

        public static void SetLocalMasterSyncFolder(string folderOrFilePath)
        {
            if (string.IsNullOrWhiteSpace(folderOrFilePath)) folderOrFilePath = DefaultMasterSyncFolder;
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                key?.SetValue("MasterSyncFolder", folderOrFilePath.Trim());
            }
            catch { }
        }

        public static string ResolveMasterFilePath(string? folderOrFile = null)
        {
            string raw = string.IsNullOrWhiteSpace(folderOrFile) ? GetLocalMasterSyncFolder() : folderOrFile.Trim();
            try
            {
                if (raw.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                    raw.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return raw;
                }
                return Path.Combine(raw, MasterFileName);
            }
            catch
            {
                return Path.Combine(DefaultMasterSyncFolder, MasterFileName);
            }
        }

        public static string ResolveMasterDirectory(string? folderOrFile = null)
        {
            string filePath = ResolveMasterFilePath(folderOrFile);
            return Path.GetDirectoryName(filePath) ?? DefaultMasterSyncFolder;
        }

        private static void WriteSuperHiddenFile(string filePath, string content)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(filePath))
            {
                try
                {
                    FileAttributes attrs = File.GetAttributes(filePath);
                    File.SetAttributes(filePath, attrs & ~(FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly));
                }
                catch { }
            }

            Program.SafeWriteAllText(filePath, content);

            try
            {
                File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden | FileAttributes.System);
            }
            catch { }
        }

        public static AppConfig Load()
        {
            string masterFolderSetting = GetLocalMasterSyncFolder();
            string masterFilePath = ResolveMasterFilePath(masterFolderSetting);

            AppConfig config = new AppConfig
            {
                MasterSyncFolder = masterFolderSetting
            };

            bool loadedFromMaster = false;
            string legacyVoyageOnly = "";

            try
            {
                if (File.Exists(masterFilePath))
                {
                    string raw = Program.SafeReadAllText(masterFilePath).Trim();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        if (raw.StartsWith("{"))
                        {
                            var parsed = JsonSerializer.Deserialize<AppConfig>(raw);
                            if (parsed != null)
                            {
                                config = parsed;
                                config.MasterSyncFolder = masterFolderSetting;
                                loadedFromMaster = true;
                            }
                        }
                        else
                        {
                            // Legacy single-line ActiveVoyage.txt format
                            legacyVoyageOnly = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
                        }
                    }
                }
            }
            catch { }

            // One-time migration from legacy local files (config.json & CustomTemplates.txt)
            bool migratedLegacyFiles = false;
            try
            {
                if (File.Exists("config.json"))
                {
                    string localJson = Program.SafeReadAllText("config.json");
                    if (!string.IsNullOrWhiteSpace(localJson))
                    {
                        var legacyCfg = JsonSerializer.Deserialize<AppConfig>(localJson);
                        if (legacyCfg != null && !loadedFromMaster)
                        {
                            config.PrintedFolder = legacyCfg.PrintedFolder;
                            config.OrdersFolder = legacyCfg.OrdersFolder;
                            config.DownloadsFolder = legacyCfg.DownloadsFolder;
                            config.BorderedPrintsFolder = legacyCfg.BorderedPrintsFolder;
                            config.PrintshopFolder = legacyCfg.PrintshopFolder;
                            config.HubAppCanvasFolder = legacyCfg.HubAppCanvasFolder;
                            config.CopyrightFolder = legacyCfg.CopyrightFolder;
                            config.CCFolder = legacyCfg.CCFolder;
                            config.DestinationForWhiteImages = legacyCfg.DestinationForWhiteImages;
                            config.ToPrintFolder = legacyCfg.ToPrintFolder;
                            config.MachineA = legacyCfg.MachineA;
                            config.MachineB = legacyCfg.MachineB;
                            config.MachineC = legacyCfg.MachineC;
                            config.MachineD = legacyCfg.MachineD;
                            config.HybridShip = legacyCfg.HybridShip;
                            if (!string.IsNullOrWhiteSpace(legacyCfg.CurrentVoyage))
                                config.CurrentVoyage = legacyCfg.CurrentVoyage;
                            if (legacyCfg.MediaSizes != null && legacyCfg.MediaSizes.Count > 0)
                                config.MediaSizes = legacyCfg.MediaSizes;
                            if (legacyCfg.MediaSizeRules != null && legacyCfg.MediaSizeRules.Count > 0)
                                config.MediaSizeRules = legacyCfg.MediaSizeRules;
                        }
                    }
                    try { File.Delete("config.json"); } catch { }
                    migratedLegacyFiles = true;
                }

                if (File.Exists("CustomTemplates.txt"))
                {
                    foreach (var line in Program.SafeReadAllLines("CustomTemplates.txt"))
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 3)
                        {
                            string tName = parts[0].Trim();
                            string tPath = parts[1].Trim();
                            bool isDual = parts[2].Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                            if (!config.CustomTemplates.Any(t => t.Name.Equals(tName, StringComparison.OrdinalIgnoreCase)))
                            {
                                config.CustomTemplates.Add(new CustomTemplateItem { Name = tName, Path = tPath, IsDual = isDual });
                            }
                        }
                    }
                    try { File.Delete("CustomTemplates.txt"); } catch { }
                    migratedLegacyFiles = true;
                }

                if (File.Exists("HubAppCanvasFolder.txt")) { try { File.Delete("HubAppCanvasFolder.txt"); } catch { } }
                if (File.Exists("MediaSizes.txt")) { try { File.Delete("MediaSizes.txt"); } catch { } }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(legacyVoyageOnly))
            {
                config.CurrentVoyage = legacyVoyageOnly;
                migratedLegacyFiles = true;
            }

            if (config.MediaSizes == null || config.MediaSizes.Count == 0)
                config.MediaSizes = new List<string>(MainForm.DefaultMediaSizes);

            if (config.MediaSizeRules == null)
                config.MediaSizeRules = new List<MediaSizeRule>();

            if (config.CustomTemplates == null)
                config.CustomTemplates = new List<CustomTemplateItem>();

            if (config.PerPcSettings == null)
                config.PerPcSettings = new Dictionary<string, PcProfile>(StringComparer.OrdinalIgnoreCase);

            // Apply Per-PC Profile for this workstation
            config.ApplyPcProfileToRuntime();

            if (!loadedFromMaster || migratedLegacyFiles)
            {
                config.Save(syncAllFolders: false);
            }

            return config;
        }

        public void ApplyPcProfileToRuntime()
        {
            if (PerPcSettings != null && PerPcSettings.TryGetValue(PcKey, out PcProfile? pc) && pc != null)
            {
                this.HybridShip = pc.HybridShip;
                this.SelectedMachine = string.IsNullOrWhiteSpace(pc.SelectedMachine) ? "Machine C" : pc.SelectedMachine;
                this.UsbDrive = string.IsNullOrWhiteSpace(pc.UsbDrive) ? "D" : pc.UsbDrive;
                this.IsDarkMode = pc.IsDarkMode;

                // If this PC has not yet adopted a newer global folder sync, use its local folder paths
                if (pc.longAdoptedSyncVersion >= this.FolderSyncVersion)
                {
                    if (!string.IsNullOrWhiteSpace(pc.PrintedFolder)) this.PrintedFolder = pc.PrintedFolder;
                    if (!string.IsNullOrWhiteSpace(pc.OrdersFolder)) this.OrdersFolder = pc.OrdersFolder;
                    if (!string.IsNullOrWhiteSpace(pc.DownloadsFolder)) this.DownloadsFolder = pc.DownloadsFolder;
                    if (!string.IsNullOrWhiteSpace(pc.ToPrintFolder)) this.ToPrintFolder = pc.ToPrintFolder;
                    if (!string.IsNullOrWhiteSpace(pc.BorderedPrintsFolder)) this.BorderedPrintsFolder = pc.BorderedPrintsFolder;
                    if (!string.IsNullOrWhiteSpace(pc.PrintshopFolder)) this.PrintshopFolder = pc.PrintshopFolder;
                    if (!string.IsNullOrWhiteSpace(pc.HubAppCanvasFolder)) this.HubAppCanvasFolder = pc.HubAppCanvasFolder;
                    if (!string.IsNullOrWhiteSpace(pc.CopyrightFolder)) this.CopyrightFolder = pc.CopyrightFolder;
                    if (!string.IsNullOrWhiteSpace(pc.CCFolder)) this.CCFolder = pc.CCFolder;
                    if (!string.IsNullOrWhiteSpace(pc.DestinationForWhiteImages)) this.DestinationForWhiteImages = pc.DestinationForWhiteImages;
                    if (!string.IsNullOrWhiteSpace(pc.MachineA)) this.MachineA = pc.MachineA;
                    if (!string.IsNullOrWhiteSpace(pc.MachineB)) this.MachineB = pc.MachineB;
                    if (!string.IsNullOrWhiteSpace(pc.MachineC)) this.MachineC = pc.MachineC;
                    if (!string.IsNullOrWhiteSpace(pc.MachineD)) this.MachineD = pc.MachineD;
                }
                else
                {
                    // Global sync occurred! Adopt global folders for this PC
                    pc.longAdoptedSyncVersion = this.FolderSyncVersion;
                    pc.PrintedFolder = this.PrintedFolder;
                    pc.OrdersFolder = this.OrdersFolder;
                    pc.DownloadsFolder = this.DownloadsFolder;
                    pc.ToPrintFolder = this.ToPrintFolder;
                    pc.BorderedPrintsFolder = this.BorderedPrintsFolder;
                    pc.PrintshopFolder = this.PrintshopFolder;
                    pc.HubAppCanvasFolder = this.HubAppCanvasFolder;
                    pc.CopyrightFolder = this.CopyrightFolder;
                    pc.CCFolder = this.CCFolder;
                    pc.DestinationForWhiteImages = this.DestinationForWhiteImages;
                    pc.MachineA = this.MachineA;
                    pc.MachineB = this.MachineB;
                    pc.MachineC = this.MachineC;
                    pc.MachineD = this.MachineD;
                }
            }
            else
            {
                // Fallback to local registry if master file wasn't reachable yet
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                    if (key != null)
                    {
                        if (key.GetValue("HybridShip") is int hs) this.HybridShip = (hs == 1);
                        if (key.GetValue("SelectedMachine") is string sm && !string.IsNullOrWhiteSpace(sm)) this.SelectedMachine = sm;
                        if (key.GetValue("UsbDrive") is string ud && !string.IsNullOrWhiteSpace(ud)) this.UsbDrive = ud;
                        if (key.GetValue("IsDarkMode") is int dm) this.IsDarkMode = (dm == 1);
                    }
                }
                catch { }
            }
        }

        public void Save(bool syncAllFolders = false)
        {
            SetLocalMasterSyncFolder(this.MasterSyncFolder);

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                key?.SetValue("HybridShip", this.HybridShip ? 1 : 0);
                key?.SetValue("SelectedMachine", this.SelectedMachine ?? "Machine C");
                key?.SetValue("UsbDrive", this.UsbDrive ?? "D");
                key?.SetValue("IsDarkMode", this.IsDarkMode ? 1 : 0);
            }
            catch { }

            string masterFilePath = ResolveMasterFilePath(this.MasterSyncFolder);

            try
            {
                AppConfig? diskConfig = null;
                if (File.Exists(masterFilePath))
                {
                    string existingRaw = Program.SafeReadAllText(masterFilePath).Trim();
                    if (existingRaw.StartsWith("{"))
                    {
                        try { diskConfig = JsonSerializer.Deserialize<AppConfig>(existingRaw); } catch { }
                    }
                }

                if (diskConfig != null)
                {
                    this.LastOrderId = Math.Max(this.LastOrderId, diskConfig.LastOrderId);

                    if (diskConfig.PerPcSettings != null)
                    {
                        foreach (var kvp in diskConfig.PerPcSettings)
                        {
                            if (!kvp.Key.Equals(PcKey, StringComparison.OrdinalIgnoreCase))
                            {
                                this.PerPcSettings[kvp.Key] = kvp.Value;
                            }
                        }
                    }

                    if (!syncAllFolders)
                    {
                        // Keep global shared folder paths on disk untouched unless Sync was explicitly pressed
                        this.FolderSyncVersion = diskConfig.FolderSyncVersion;
                    }
                }

                if (syncAllFolders)
                {
                    this.FolderSyncVersion = DateTime.UtcNow.Ticks;
                    foreach (var profile in this.PerPcSettings.Values)
                    {
                        profile.longAdoptedSyncVersion = this.FolderSyncVersion;
                        profile.PrintedFolder = this.PrintedFolder;
                        profile.OrdersFolder = this.OrdersFolder;
                        profile.DownloadsFolder = this.DownloadsFolder;
                        profile.ToPrintFolder = this.ToPrintFolder;
                        profile.BorderedPrintsFolder = this.BorderedPrintsFolder;
                        profile.PrintshopFolder = this.PrintshopFolder;
                        profile.HubAppCanvasFolder = this.HubAppCanvasFolder;
                        profile.CopyrightFolder = this.CopyrightFolder;
                        profile.CCFolder = this.CCFolder;
                        profile.DestinationForWhiteImages = this.DestinationForWhiteImages;
                        profile.MachineA = this.MachineA;
                        profile.MachineB = this.MachineB;
                        profile.MachineC = this.MachineC;
                        profile.MachineD = this.MachineD;
                    }
                }

                this.PerPcSettings[PcKey] = new PcProfile
                {
                    HybridShip = this.HybridShip,
                    SelectedMachine = this.SelectedMachine,
                    UsbDrive = this.UsbDrive,
                    IsDarkMode = this.IsDarkMode,
                    longAdoptedSyncVersion = this.FolderSyncVersion,
                    PrintedFolder = this.PrintedFolder,
                    OrdersFolder = this.OrdersFolder,
                    DownloadsFolder = this.DownloadsFolder,
                    ToPrintFolder = this.ToPrintFolder,
                    BorderedPrintsFolder = this.BorderedPrintsFolder,
                    PrintshopFolder = this.PrintshopFolder,
                    HubAppCanvasFolder = this.HubAppCanvasFolder,
                    CopyrightFolder = this.CopyrightFolder,
                    CCFolder = this.CCFolder,
                    DestinationForWhiteImages = this.DestinationForWhiteImages,
                    MachineA = this.MachineA,
                    MachineB = this.MachineB,
                    MachineC = this.MachineC,
                    MachineD = this.MachineD
                };

                if (!syncAllFolders && diskConfig != null)
                {
                    // Restore disk's global shared folder properties before writing so other PCs aren't affected
                    string myPrinted = this.PrintedFolder;
                    string myOrders = this.OrdersFolder;
                    string myDownloads = this.DownloadsFolder;
                    string myToPrint = this.ToPrintFolder;
                    string myBordered = this.BorderedPrintsFolder;
                    string myPrintshop = this.PrintshopFolder;
                    string myHub = this.HubAppCanvasFolder;
                    string myCopyright = this.CopyrightFolder;
                    string myCC = this.CCFolder;
                    string myWhite = this.DestinationForWhiteImages;
                    string myA = this.MachineA;
                    string myB = this.MachineB;
                    string myC = this.MachineC;
                    string myD = this.MachineD;

                    this.PrintedFolder = diskConfig.PrintedFolder;
                    this.OrdersFolder = diskConfig.OrdersFolder;
                    this.DownloadsFolder = diskConfig.DownloadsFolder;
                    this.ToPrintFolder = diskConfig.ToPrintFolder;
                    this.BorderedPrintsFolder = diskConfig.BorderedPrintsFolder;
                    this.PrintshopFolder = diskConfig.PrintshopFolder;
                    this.HubAppCanvasFolder = diskConfig.HubAppCanvasFolder;
                    this.CopyrightFolder = diskConfig.CopyrightFolder;
                    this.CCFolder = diskConfig.CCFolder;
                    this.DestinationForWhiteImages = diskConfig.DestinationForWhiteImages;
                    this.MachineA = diskConfig.MachineA;
                    this.MachineB = diskConfig.MachineB;
                    this.MachineC = diskConfig.MachineC;
                    this.MachineD = diskConfig.MachineD;

                    string jsonToWrite = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                    WriteSuperHiddenFile(masterFilePath, jsonToWrite);

                    this.PrintedFolder = myPrinted;
                    this.OrdersFolder = myOrders;
                    this.DownloadsFolder = myDownloads;
                    this.ToPrintFolder = myToPrint;
                    this.BorderedPrintsFolder = myBordered;
                    this.PrintshopFolder = myPrintshop;
                    this.HubAppCanvasFolder = myHub;
                    this.CopyrightFolder = myCopyright;
                    this.CCFolder = myCC;
                    this.DestinationForWhiteImages = myWhite;
                    this.MachineA = myA;
                    this.MachineB = myB;
                    this.MachineC = myC;
                    this.MachineD = myD;
                }
                else
                {
                    string jsonToWrite = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                    WriteSuperHiddenFile(masterFilePath, jsonToWrite);
                }
            }
            catch { }
        }

        public int IncrementAndSaveNextOrderId(int fallbackMaxFromHistory)
        {
            string masterFilePath = ResolveMasterFilePath(this.MasterSyncFolder);
            int currentMax = Math.Max(6000000, Math.Max(this.LastOrderId, fallbackMaxFromHistory));

            try
            {
                if (File.Exists(masterFilePath))
                {
                    string existingRaw = Program.SafeReadAllText(masterFilePath).Trim();
                    if (existingRaw.StartsWith("{"))
                    {
                        var diskConfig = JsonSerializer.Deserialize<AppConfig>(existingRaw);
                        if (diskConfig != null && diskConfig.LastOrderId > currentMax)
                        {
                            currentMax = diskConfig.LastOrderId;
                        }
                    }
                }
            }
            catch { }

            int nextId = currentMax + 1;
            if (nextId > 9999999) nextId = 6000001;

            this.LastOrderId = nextId;
            Save(syncAllFolders: false);
            return nextId;
        }

        public MediaSizeRule GetEffectiveRule(string sizeName)
        {
            string cleanName = (sizeName ?? "").Trim();
            if (MediaSizeRules != null)
            {
                var saved = MediaSizeRules.FirstOrDefault(r => r.Name.Equals(cleanName, StringComparison.OrdinalIgnoreCase));
                if (saved != null && !string.IsNullOrWhiteSpace(saved.Block1Size))
                {
                    return new MediaSizeRule
                    {
                        Name = cleanName,
                        IsMultiBlock = saved.IsMultiBlock,
                        Block1Size = saved.Block1Size,
                        Block1PrintName = string.IsNullOrWhiteSpace(saved.Block1PrintName) ? saved.Block1Size : saved.Block1PrintName,
                        Block2Size = saved.Block2Size,
                        Block2PrintName = string.IsNullOrWhiteSpace(saved.Block2PrintName) ? saved.Block2Size : saved.Block2PrintName
                    };
                }
            }

            return BuildDefaultRule(cleanName);
        }

        public static MediaSizeRule BuildDefaultRule(string sizeName)
        {
            string cleanName = (sizeName ?? "").Trim();

            // Rule 1: If it contains "Combo", it is always 1 sheet (Single Block)
            if (cleanName.IndexOf("combo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new MediaSizeRule
                {
                    Name = cleanName,
                    IsMultiBlock = false,
                    Block1Size = cleanName,
                    Block1PrintName = cleanName
                };
            }

            // Rule 2: Specific preset for 8x10 and 4W on (10in)
            if (cleanName.Equals("8x10 and 4W on (10in)", StringComparison.OrdinalIgnoreCase))
            {
                return new MediaSizeRule
                {
                    Name = cleanName,
                    IsMultiBlock = true,
                    Block1Size = "8x10",
                    Block1PrintName = "8x10 Print",
                    Block2Size = "4-Wallets (vert)",
                    Block2PrintName = "4-Wallets (vert)"
                };
            }

            // Rule 3: Generic "and" (without "Combo") splits into 2 blocks
            int andIdx = cleanName.IndexOf(" and ", StringComparison.OrdinalIgnoreCase);
            if (andIdx > 0)
            {
                string part1 = cleanName.Substring(0, andIdx).Trim();
                string part2 = cleanName.Substring(andIdx + 5).Trim();

                int onIdx = part2.IndexOf(" on ", StringComparison.OrdinalIgnoreCase);
                if (onIdx > 0) part2 = part2.Substring(0, onIdx).Trim();

                string b1Size = part1;
                string b1Print = part1.Equals("8x10", StringComparison.OrdinalIgnoreCase) ? "8x10 Print" : part1;

                string b2Size = (part2.Equals("4W", StringComparison.OrdinalIgnoreCase) || part2.StartsWith("4-Wallet", StringComparison.OrdinalIgnoreCase))
                    ? "4-Wallets (vert)"
                    : part2;
                string b2Print = b2Size;

                return new MediaSizeRule
                {
                    Name = cleanName,
                    IsMultiBlock = true,
                    Block1Size = b1Size,
                    Block1PrintName = b1Print,
                    Block2Size = b2Size,
                    Block2PrintName = b2Print
                };
            }

            // Rule 4: Preset for 4-2.5x3.5 Wallets (8x10)
            if (cleanName.Equals("4-2.5x3.5 Wallets (8x10)", StringComparison.OrdinalIgnoreCase) ||
                cleanName.Equals("4-2.5x3.5Wallets(8x10)", StringComparison.OrdinalIgnoreCase))
            {
                return new MediaSizeRule
                {
                    Name = cleanName,
                    IsMultiBlock = false,
                    Block1Size = "4-2.5x3.5Wallets(8x10)",
                    Block1PrintName = "4-2.5x3.5 Wallets (8x10)"
                };
            }

            // Default: Single block with identical Size and PrintName
            return new MediaSizeRule
            {
                Name = cleanName,
                IsMultiBlock = false,
                Block1Size = cleanName,
                Block1PrintName = cleanName
            };
        }
    }
}
