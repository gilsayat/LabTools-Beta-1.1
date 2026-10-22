using System;
using System.IO;
using System.Text.Json;

namespace LabTools
{
    public class AppConfig
    {
        public string PrintedFolder { get; set; } = @"Z:\Printed";
        public string DownloadsFolder { get; set; } = @"Z:\PixelsOrder\Downloads";
        public string PrintshopFolder { get; set; } = @"Z:\PrintshopShare\PO101026";
        public string CCFolder { get; set; } = @"Z:\CC";
        public string OrdersFolder { get; set; } = @"Z:\PixelsOrder\Processed";
        public string DestinationForWhiteImages { get; set; } = @"Z:\TEMP";
        public string DefaultMedia { get; set; } = "Glossy";
        public string BorderedPrintsFolder { get; set; } = @"Z:\PixelsOrder\Prints";
        public string MachineA { get; set; } = @"Z:\PixelsOrder\Processed\ToMacA";
        public string MachineB { get; set; } = @"Z:\PixelsOrder\Processed\ToMacB";
        public string MachineC { get; set; } = @"Z:\PixelsOrder\Processed\ToMacC";
        public string MachineD { get; set; } = @"Z:\PixelsOrder\Processed\ToMacD";
        public string CurrentVoyage { get; set; } = "PO101126";

        private static readonly string configPath = "config.json";

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch { }
        }
    }
}
