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
        public string CopyrightFolder { get; set; } = @"Z:\PhotoLab\Misc";
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

                // Clean up any legacy text files if they exist
                if (File.Exists("HubAppCanvasFolder.txt")) { try { File.Delete("HubAppCanvasFolder.txt"); } catch { } }
                if (File.Exists("MediaSizes.txt")) { try { File.Delete("MediaSizes.txt"); } catch { } }
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
