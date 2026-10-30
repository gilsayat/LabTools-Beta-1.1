using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

namespace LabTools V3.5
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
        public bool HybridShip { get; set; } = false;
        public string MachineA { get; set; } = @"Z:\MachineA";
        public string MachineB { get; set; } = @"Z:\MachineB";
        public string MachineC { get; set; } = @"Z:\MachineC";
        public string MachineD { get; set; } = @"Z:\MachineD";
        public string CurrentVoyage { get; set; } = "PO101126";
        public List<string> MediaSizes { get; set; } = new List<string>();
        public List<MediaSizeRule> MediaSizeRules { get; set; } = new List<MediaSizeRule>();

        private static readonly string ConfigFilePath = "config.json";

        public static AppConfig Load()
        {
            AppConfig config = new AppConfig();
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                    if (loaded != null)
                    {
                        config = loaded;
                    }
                }

                // Clean up any legacy text files if they exist
                if (File.Exists("HubAppCanvasFolder.txt")) { try { File.Delete("HubAppCanvasFolder.txt"); } catch { } }
                if (File.Exists("MediaSizes.txt")) { try { File.Delete("MediaSizes.txt"); } catch { } }
            }
            catch { }

            if (config.MediaSizes == null || config.MediaSizes.Count == 0)
            {
                config.MediaSizes = new List<string>(MainForm.DefaultMediaSizes);
            }

            if (config.MediaSizeRules == null)
            {
                config.MediaSizeRules = new List<MediaSizeRule>();
            }

            return config;
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

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch { }
        }
    }
}
