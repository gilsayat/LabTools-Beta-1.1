using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public class CustomTemplateRecord
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool IsDual { get; set; } = false;
    }

    public class PcWorkstationProfile
    {
        public bool HybridShip { get; set; } = false;
        public string SelectedMachine { get; set; } = "Machine C";
        public string UsbDrive { get; set; } = "D";
        public bool IsDarkMode { get; set; } = true;

        // Per-PC local folder paths (used until a global Folder Sync is triggered)
        public bool HasLocalFolderOverrides { get; set; } = false;
        public long AppliedFolderSyncVersion { get; set; } = 0;
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
    }

    public class AppConfig
    {
        private const string RegistryKeyPath = @"SOFTWARE\LabTools";
        public const string DefaultMasterSyncFolder = @"Z:\PhotoLab\EHM\GIL";
        public const string MasterSyncFileName = "ActiveVoyage.txt";

        // ========================================================================
        // SHARED GLOBAL PROPERTIES (Stored in Master Sync File)
        // ========================================================================
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

        public string CurrentVoyage { get; set; } = "PO101126";
        public int LastOrderId { get; set; } = 6000000;
        public long FolderSyncVersion { get; set; } = 1;

        public List<string> MediaSizes { get; set; } = new List<string>();
        public List<MediaSizeRule> MediaSizeRules { get; set; } = new List<MediaSizeRule>();
        public List<CustomTemplateRecord> CustomTemplates { get; set; } = new List<CustomTemplateRecord>();
        public Dictionary<string, PcWorkstationProfile> Workstations { get; set; } = new Dictionary<string, PcWorkstationProfile>(StringComparer.OrdinalIgnoreCase);

        // ========================================================================
        // PER-PC RUNTIME PROPERTIES (Not overwritten by other PCs)
        // ========================================================================
        [JsonIgnore]
        public string MasterSyncFolder { get; set; } = DefaultMasterSyncFolder;

        [JsonIgnore]
        public bool HybridShip { get; set; } = false;

        [JsonIgnore]
        public string SelectedMachine { get; set; } = "Machine C";

        [JsonIgnore]
        public string UsbDrive { get; set; } = "D";

        [JsonIgnore]
        public bool IsDarkModePreference { get; set; } = true;

        private static string CurrentPcName => string.IsNullOrWhiteSpace(Environment.MachineName) ? "DEFAULT_PC" : Environment.MachineName.Trim();

        // ========================================================================
        // REGISTRY & SUPER-HIDDEN MASTER FILE HELPERS
        // ========================================================================
        public static string GetLocalMasterSyncPathFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                if (key != null)
                {
                    string? saved = key.GetValue("MasterSyncFolder") as string;
                    if (!string.IsNullOrWhiteSpace(saved))
                        return saved.Trim();
                }
            }
            catch { }
            return DefaultMasterSyncFolder;
        }

        public static void SetLocalMasterSyncPathInRegistry(string folderOrFilePath)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                key?.SetValue("MasterSyncFolder", (folderOrFilePath ?? DefaultMasterSyncFolder).Trim());
            }
            catch { }
        }

        public static string ResolveMasterFilePath(string? folderOrFilePath)
        {
            string raw = string.IsNullOrWhiteSpace(folderOrFilePath) ? DefaultMasterSyncFolder : folderOrFilePath.Trim();
            try
            {
                if (raw.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                    raw.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                    raw.EndsWith(".sync", StringComparison.OrdinalIgnoreCase))
                {
                    string? dir = Path.GetDirectoryName(raw);
                    if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                    return raw;
                }
                Directory.CreateDirectory(raw);
                return Path.Combine(raw, MasterSyncFileName);
            }
            catch
            {
                return Path.Combine(DefaultMasterSyncFolder, MasterSyncFileName);
            }
        }

        public static string ResolveMasterDirectoryPath(string? folderOrFilePath)
        {
            string filePath = ResolveMasterFilePath(folderOrFilePath);
            return Path.GetDirectoryName(filePath) ?? DefaultMasterSyncFolder;
        }

        public static void ApplySuperHiddenAttributes(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    FileAttributes attrs = File.GetAttributes(filePath);
                    File.SetAttributes(filePath, attrs | FileAttributes.Hidden | FileAttributes.System);
                }
            }
            catch { }
        }

        public static void ClearHiddenAttributesForWrite(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    FileAttributes attrs = File.GetAttributes(filePath);
                    File.SetAttributes(filePath, attrs & ~FileAttributes.Hidden & ~FileAttributes.System & ~FileAttributes.ReadOnly);
                }
            }
            catch { }
        }

        // ========================================================================
        // ONE-FILE BRAIN LOADER (Line 1 = Voyage Code, Line 2+ = JSON Payload)
        // ========================================================================
        public static AppConfig Load()
        {
            string masterFolderSetting = GetLocalMasterSyncPathFromRegistry();
            string masterFilePath = ResolveMasterFilePath(masterFolderSetting);

            AppConfig config = new AppConfig
            {
                MasterSyncFolder = masterFolderSetting
            };

            bool loadedFromMaster = false;

            try
            {
                if (File.Exists(masterFilePath))
                {
                    string rawText = Program.SafeReadAllText(masterFilePath);
                    if (!string.IsNullOrWhiteSpace(rawText))
                    {
                        int firstBrace = rawText.IndexOf('{');
                        if (firstBrace >= 0)
                        {
                            string headerPart = rawText.Substring(0, firstBrace).Trim();
                            string jsonPart = rawText.Substring(firstBrace).Trim();

                            var parsed = JsonSerializer.Deserialize<AppConfig>(jsonPart);
                            if (parsed != null)
                            {
                                config = parsed;
                                config.MasterSyncFolder = masterFolderSetting;
                                if (!string.IsNullOrWhiteSpace(headerPart))
                                {
                                    string firstLine = headerPart.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
                                    if (!string.IsNullOrWhiteSpace(firstLine))
                                        config.CurrentVoyage = firstLine.ToUpperInvariant();
                                }
                                loadedFromMaster = true;
                            }
                        }
                        else
                        {
                            // Legacy ActiveVoyage.txt with only the voyage string on line 1
                            string firstLine = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
                            if (!string.IsNullOrWhiteSpace(firstLine))
                            {
                                config.CurrentVoyage = firstLine.ToUpperInvariant();
                            }
                        }
                    }
                    ApplySuperHiddenAttributes(masterFilePath);
                }
            }
            catch { }

            // One-time seamless migration from legacy local files if present
            bool migratedLegacyFiles = false;
            try
            {
                if (File.Exists("config.json"))
                {
                    string legacyJson = File.ReadAllText("config.json");
                    var legacyCfg = JsonSerializer.Deserialize<AppConfig>(legacyJson);
                    if (legacyCfg != null && !loadedFromMaster)
                    {
                        string currentVoyageBackup = config.CurrentVoyage;
                        config = legacyCfg;
                        config.MasterSyncFolder = masterFolderSetting;
                        if (!string.IsNullOrWhiteSpace(currentVoyageBackup))
                            config.CurrentVoyage = currentVoyageBackup;
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
                            bool tDual = parts[2].Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                            if (!string.IsNullOrEmpty(tName) && !config.CustomTemplates.Any(t => t.Name.Equals(tName, StringComparison.OrdinalIgnoreCase)))
                            {
                                config.CustomTemplates.Add(new CustomTemplateRecord { Name = tName, Path = tPath, IsDual = tDual });
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

            if (config.MediaSizes == null || config.MediaSizes.Count == 0)
                config.MediaSizes = new List<string>(MainForm.DefaultMediaSizes);

            if (config.MediaSizeRules == null)
                config.MediaSizeRules = new List<MediaSizeRule>();

            if (config.CustomTemplates == null)
                config.CustomTemplates = new List<CustomTemplateRecord>();

            if (config.Workstations == null)
                config.Workstations = new Dictionary<string, PcWorkstationProfile>(StringComparer.OrdinalIgnoreCase);

            // Apply Per-PC Profile for this workstation
            if (config.Workstations.TryGetValue(CurrentPcName, out PcWorkstationProfile? pcProfile) && pcProfile != null)
            {
                config.HybridShip = pcProfile.HybridShip;
                config.SelectedMachine = string.IsNullOrWhiteSpace(pcProfile.SelectedMachine) ? "Machine C" : pcProfile.SelectedMachine;
                config.UsbDrive = string.IsNullOrWhiteSpace(pcProfile.UsbDrive) ? "D" : pcProfile.UsbDrive;
                config.IsDarkModePreference = pcProfile.IsDarkMode;

                // Use local folder overrides only if this PC saved local paths AFTER the last global Sync button press
                if (pcProfile.HasLocalFolderOverrides && pcProfile.AppliedFolderSyncVersion >= config.FolderSyncVersion)
                {
                    config.PrintedFolder = pcProfile.PrintedFolder;
                    config.OrdersFolder = pcProfile.OrdersFolder;
                    config.DownloadsFolder = pcProfile.DownloadsFolder;
                    config.BorderedPrintsFolder = pcProfile.BorderedPrintsFolder;
                    config.PrintshopFolder = pcProfile.PrintshopFolder;
                    config.HubAppCanvasFolder = pcProfile.HubAppCanvasFolder;
                    config.CopyrightFolder = pcProfile.CopyrightFolder;
                    config.CCFolder = pcProfile.CCFolder;
                    config.DestinationForWhiteImages = pcProfile.DestinationForWhiteImages;
                    config.ToPrintFolder = pcProfile.ToPrintFolder;
                    config.MachineA = pcProfile.MachineA;
                    config.MachineB = pcProfile.MachineB;
                    config.MachineC = pcProfile.MachineC;
                    config.MachineD = pcProfile.MachineD;
                }
            }

            if (migratedLegacyFiles || !File.Exists(masterFilePath))
            {
                config.Save(syncAllFoldersAcrossPcs: false);
            }

            return config;
        }

        // ========================================================================
        // ONE-FILE BRAIN SAVER (Preserves Shared Folders unless Sync is Triggered)
        // ========================================================================
        public void Save(bool syncAllFoldersAcrossPcs = false)
        {
            SetLocalMasterSyncPathInRegistry(this.MasterSyncFolder);
            string masterFilePath = ResolveMasterFilePath(this.MasterSyncFolder);

            try
            {
                AppConfig? diskConfig = null;
                if (File.Exists(masterFilePath))
                {
                    string rawText = Program.SafeReadAllText(masterFilePath);
                    int firstBrace = rawText.IndexOf('{');
                    if (firstBrace >= 0)
                    {
                        try
                        {
                            diskConfig = JsonSerializer.Deserialize<AppConfig>(rawText.Substring(firstBrace).Trim());
                        }
                        catch { }
                    }
                }

                if (diskConfig != null)
                {
                    // Always keep the highest LastOrderId across the network
                    if (diskConfig.LastOrderId > this.LastOrderId)
                        this.LastOrderId = diskConfig.LastOrderId;

                    if (diskConfig.Workstations != null)
                    {
                        foreach (var kvp in diskConfig.Workstations)
                        {
                            if (!kvp.Key.Equals(CurrentPcName, StringComparison.OrdinalIgnoreCase))
                            {
                                this.Workstations[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }

                if (syncAllFoldersAcrossPcs)
                {
                    // Global Folder Sync requested: Increment FolderSyncVersion and clear local overrides on all PCs
                    this.FolderSyncVersion = Math.Max(this.FolderSyncVersion, diskConfig?.FolderSyncVersion ?? 1) + 1;
                    foreach (var ws in this.Workstations.Values)
                    {
                        ws.HasLocalFolderOverrides = false;
                        ws.AppliedFolderSyncVersion = this.FolderSyncVersion;
                    }
                }

                // Update this PC's profile
                var myProfile = new PcWorkstationProfile
                {
                    HybridShip = this.HybridShip,
                    SelectedMachine = string.IsNullOrWhiteSpace(this.SelectedMachine) ? "Machine C" : this.SelectedMachine,
                    UsbDrive = string.IsNullOrWhiteSpace(this.UsbDrive) ? "D" : this.UsbDrive,
                    IsDarkMode = this.IsDarkModePreference,
                    HasLocalFolderOverrides = !syncAllFoldersAcrossPcs,
                    AppliedFolderSyncVersion = this.FolderSyncVersion,
                    PrintedFolder = this.PrintedFolder,
                    OrdersFolder = this.OrdersFolder,
                    DownloadsFolder = this.DownloadsFolder,
                    BorderedPrintsFolder = this.BorderedPrintsFolder,
                    PrintshopFolder = this.PrintshopFolder,
                    HubAppCanvasFolder = this.HubAppCanvasFolder,
                    CopyrightFolder = this.CopyrightFolder,
                    CCFolder = this.CCFolder,
                    DestinationForWhiteImages = this.DestinationForWhiteImages,
                    ToPrintFolder = this.ToPrintFolder,
                    MachineA = this.MachineA,
                    MachineB = this.MachineB,
                    MachineC = this.MachineC,
                    MachineD = this.MachineD
                };
                this.Workstations[CurrentPcName] = myProfile;

                // If NOT syncing folders across all PCs and diskConfig exists, keep the shared global folders untouched
                // so other PCs are not affected by local folder edits on this PC
                AppConfig payloadToWrite = new AppConfig
                {
                    CurrentVoyage = string.IsNullOrWhiteSpace(this.CurrentVoyage) ? "PO101126" : this.CurrentVoyage.Trim().ToUpperInvariant(),
                    LastOrderId = this.LastOrderId,
                    FolderSyncVersion = this.FolderSyncVersion,
                    PrintedFolder = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.PrintedFolder : diskConfig.PrintedFolder,
                    OrdersFolder = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.OrdersFolder : diskConfig.OrdersFolder,
                    DownloadsFolder = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.DownloadsFolder : diskConfig.DownloadsFolder,
                    BorderedPrintsFolder = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.BorderedPrintsFolder : diskConfig.BorderedPrintsFolder,
                    PrintshopFolder = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.PrintshopFolder : diskConfig.PrintshopFolder,
                    HubAppCanvasFolder = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.HubAppCanvasFolder : diskConfig.HubAppCanvasFolder,
                    CopyrightFolder = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.CopyrightFolder : diskConfig.CopyrightFolder,
                    CCFolder = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.CCFolder : diskConfig.CCFolder,
                    DestinationForWhiteImages = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.DestinationForWhiteImages : diskConfig.DestinationForWhiteImages,
                    ToPrintFolder = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.ToPrintFolder : diskConfig.ToPrintFolder,
                    MachineA = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.MachineA : diskConfig.MachineA,
                    MachineB = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.MachineB : diskConfig.MachineB,
                    MachineC = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.MachineC : diskConfig.MachineC,
                    MachineD = (syncAllFoldersAcrossPcs || diskConfig == null) ? this.MachineD : diskConfig.MachineD,
                    MediaSizes = this.MediaSizes,
                    MediaSizeRules = this.MediaSizeRules,
                    CustomTemplates = this.CustomTemplates,
                    Workstations = this.Workstations
                };

                string json = JsonSerializer.Serialize(payloadToWrite, new JsonSerializerOptions { WriteIndented = true });
                string combinedContent = payloadToWrite.CurrentVoyage + Environment.NewLine + json;

                string? dir = Path.GetDirectoryName(masterFilePath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                ClearHiddenAttributesForWrite(masterFilePath);
                Program.SafeWriteAllText(masterFilePath, combinedContent);
                ApplySuperHiddenAttributes(masterFilePath);
            }
            catch { }
        }

        // ========================================================================
        // ATOMIC GLOBAL ORDER ID GENERATOR (Never Resets on Clear or New Voyage)
        // ========================================================================
        public int AllocateNextOrderId(Func<int>? initialHistoryFallbackScanner = null)
        {
            string masterFilePath = ResolveMasterFilePath(this.MasterSyncFolder);
            int highestKnown = Math.Max(6000000, this.LastOrderId);

            try
            {
                if (File.Exists(masterFilePath))
                {
                    string rawText = Program.SafeReadAllText(masterFilePath);
                    int firstBrace = rawText.IndexOf('{');
                    if (firstBrace >= 0)
                    {
                        var diskConfig = JsonSerializer.Deserialize<AppConfig>(rawText.Substring(firstBrace).Trim());
                        if (diskConfig != null && diskConfig.LastOrderId > highestKnown)
                        {
                            highestKnown = diskConfig.LastOrderId;
                        }
                    }
                }
            }
            catch { }

            // One-time bootstrap scan if LastOrderId hasn't been initialized from existing history yet
            if (highestKnown <= 6000000 && initialHistoryFallbackScanner != null)
            {
                try
                {
                    int scannedMax = initialHistoryFallbackScanner();
                    if (scannedMax > highestKnown && scannedMax <= 9999999)
                        highestKnown = scannedMax;
                }
                catch { }
            }

            int nextId = highestKnown + 1;
            if (nextId > 9999999) nextId = 6000001;

            this.LastOrderId = nextId;
            Save(syncAllFoldersAcrossPcs: false);
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
