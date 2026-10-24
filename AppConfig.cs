using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

namespace LabTools
{
    public class AppConfig
    {
        public string PrintedFolder { get; set; } = @"Z:\Printed";
        public string OrdersFolder { get; set; } = @"Z:\PixelsOrder\Processed";
        public string DownloadsFolder { get; set; } = @"C:\Downloads";
        public string BorderedPrintsFolder { get; set; } = @"Z:\PixelsOrder\Prints";
        public string PrintshopFolder { get; set; } = @"Z:\PhotoLab\Printshop";
        public string HubAppCanvasFolder { get; set; } = @"Z:\PixelsOrder\HubAppCanvas";
        public string CCFolder { get; set; } = @"Z:\CC";
        public string DestinationForWhiteImages { get; set; } = @"Z:\WhiteImages";
        public string MachineA { get; set; } = @"Z:\MachineA";
        public string MachineB { get; set; } = @"Z:\MachineB";
        public string MachineC { get; set; } = @"Z:\MachineC";
        public string MachineD { get; set; } = @"Z:\MachineD";
        public string CurrentVoyage { get; set; } = "PO101126";
        public List<string> MediaSizes { get; set; } = new List<string>();

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

                // Clean up legacy text files if they exist and migrate values
                if (File.Exists("HubAppCanvasFolder.txt"))
                {
                    string legacyPath = File.ReadAllText("HubAppCanvasFolder.txt").Trim();
                    if (!string.IsNullOrWhiteSpace(legacyPath)) config.HubAppCanvasFolder = legacyPath;
                    try { File.Delete("HubAppCanvasFolder.txt"); } catch { }
                    config.Save();
                }

                if (File.Exists("MediaSizes.txt"))
                {
                    var lines = new List<string>();
                    foreach (var line in File.ReadAllLines("MediaSizes.txt"))
                    {
                        string s = line.Trim();
                        if (!string.IsNullOrEmpty(s) && !lines.Contains(s, StringComparer.OrdinalIgnoreCase))
                            lines.Add(s);
                    }
                    if (lines.Count > 0) config.MediaSizes = lines;
                    try { File.Delete("MediaSizes.txt"); } catch { }
                    config.Save();
                }
            }
            catch { }

            if (config.MediaSizes == null || config.MediaSizes.Count == 0)
            {
                config.MediaSizes = new List<string>(MainForm.DefaultMediaSizes);
            }

            return config;
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
