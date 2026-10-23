using System;  
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using System.Text.Json;  
using System.Windows.Forms;  
using System.Drawing;  
using System.Text;  
using System.Text.RegularExpressions;  
using System.Drawing.Drawing2D;  
using System.Drawing.Imaging;  
using System.Linq;  
using System.Threading;  
using System.Threading.Tasks;  
using System.Collections.Generic;  
using System.Security.Cryptography;  
using System.Runtime.InteropServices;  
  
// ========================================================================  
// LabTools-Beta - Laboratory Workflow Automation System  
// Publisher: Gil Sayat (Stored in Application Metadata)  
// ========================================================================  
  
namespace LabTools  
{  
    static class Program  
    {  
        [STAThread]  
        static void Main()  
        {  
            Application.EnableVisualStyles();  
            Application.SetCompatibleTextRenderingDefault(false);  

            // Run the two-stage Adobe-inspired splash sequence on boot
            using (var splash = new SplashForm())
            {
                splash.ShowDialog();
            }

            Application.Run(new MainForm());  
        }  
  
        // ========================================================================  
        // MULTI-PC TRAFFIC CONTROLLERS (FILE LOCK SAFETY)  
        // ========================================================================  
        public static void SafeAppendAllText(string path, string content)
        {
            const int maxAttempts = 15;
            for (int attempt = 1; ; attempt++)
            {
                try { File.AppendAllText(path, content, Encoding.UTF8); return; }
                catch (IOException) when (attempt < maxAttempts) { Thread.Sleep(200); }
            }
        }

        public static void SafeWriteAllText(string path, string content)
        {
            const int maxAttempts = 15;
            for (int attempt = 1; ; attempt++)
            {
                try { File.WriteAllText(path, content, Encoding.UTF8); return; }
                catch (IOException) when (attempt < maxAttempts) { Thread.Sleep(200); }
            }
        }

        public static string[] SafeReadAllLines(string path)  
        {  
            int retries = 15;  
            while (retries > 0)  
            {  
                try   
                {   
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))  
                    using (var sr = new StreamReader(fs))  
                    {  
                        var lines = new List<string>();  
                        while (!sr.EndOfStream) { string? ln = sr.ReadLine(); if (ln != null) lines.Add(ln); }  
                        return lines.ToArray();  
                    }  
                }  
                catch (IOException) { Thread.Sleep(200); retries--; }  
            }  
            return Array.Empty<string>();  
        }  
    }  
  
    public class MainForm : Form  
    {  
#pragma warning disable CS8618  
        public const string Publisher = "Gil Sayat";  
        public const string AppTitle = "LabTools-Beta";  
  
        // Pre-compiled Regex for massive search speed improvements  
        private static readonly Regex OrderSearchRegex = new Regex(@"^\d{7,8}$", RegexOptions.Compiled);  
        private static readonly Regex CabinSearchRegex = new Regex(@"^\d{4,5}$", RegexOptions.Compiled);  
        private static readonly Regex PhotoRegex = new Regex(@"\b([pP]\d{6,12}(?:\.[jJ][pP][eE]?[gG])?|[A-Za-z0-9_\-\.]+\.(?:jpe?g|png|tif|bmp))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);  
        private static readonly Regex CabinMatchRegex = new Regex(@"(?:ExtCabin|Cabin)\s*=\s*([0-9A-Za-z]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);  
        private static readonly Regex SizeMatchRegex = new Regex(@"(?:Size|PrintName)\s*=\s*([^\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);  
        private static readonly Regex ExactPhotoRegex = new Regex(@"^[pP]\d{6,12}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);  
  
        // Palette  
        private readonly Color darkBackground = Color.FromArgb(43, 43, 43);  
        private readonly Color panelBackground = Color.FromArgb(54, 54, 54);  
        private readonly Color accentBlue = Color.FromArgb(28, 115, 189);  
        private readonly Color accentTeal = Color.FromArgb(17, 122, 114);  
        private readonly Color textWhite = Color.White;  
  
        private readonly Color[] logoStripeColors = new Color[]  
        {  
            Color.FromArgb(128, 130, 133), Color.FromArgb(0, 188, 212),  
            Color.FromArgb(0, 114, 206), Color.FromArgb(0, 47, 108),  
            Color.FromArgb(67, 176, 42), Color.FromArgb(243, 146, 0),  
            Color.FromArgb(227, 27, 35)  
        };  
  
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)   
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" };  

        public static readonly string[] DefaultMediaSizes = new string[] 
        { 
            "8x10", "4x6", "5x7", "2-5x7", "4x8", "4-Wallets (vert)", 
            "Wallet Combo (4W)", "8x10 and 4W on (10in)", 
            "5x7 and 4-Wallet Combo (8x10)", "8x10 PORTHOLE 8in", 
            "8x10 Port Hole", "8X10 Mini PORTHOLE" 
        };
  
        private AppConfig config = null!;  
        private TextBox txtSearch = null!;  
        private Button btnFind = null!;  
        private Button btnCancelSearch = null!;  
        private CheckedListBox lstFiles = null!;   
        private PictureBox picLogo = null!;      
        private ComboBox cmbMachine = null!;  
        private Label lblMachineStatus = null!;  
        private TextBox txtUsbDrive = null!;  
        private TextBox txtCabin = null!;             
        private ComboBox cmbCanvasSize = null!;  
        private TextBox txtCanvasCabin = null!;       
        private TextBox txtSeller = null!;  
        private Label lblCanvasHeader = null!;  
        private TextBox txtPrintCabinNum = null!;  
        private Label lblPrintQty = null!;  
        private NumericUpDown numPrintQty = null!;  
        private ComboBox cmbPrintSizeInput = null!;  
        private CheckBox chkAuto4W = null!;  
        private ComboBox cmbOrderType = null!;   
        private Label lblStatusLeft = null!;  
        private Label lblStatusRight = null!;  
        private Label lblLastOrder = null!;          
        private Label lblFilesFound = null!;  
        private ProgressBar progressBar = null!;  
        private ComboBox cmbAspectCrop = null!;  
  
        private FileSystemWatcher? folderWatcher;  
        private FileSystemWatcher? printshopWatcher;  
        private System.Windows.Forms.Timer? flashTimer;  
        private int flashCounter = 0;  
        private bool isSyncingCabin = false;  
        private int nextCanvasNumber = 1;  
        private CancellationTokenSource? searchCts;  
        private CancellationTokenSource? copyCts;
        private int searchGeneration = 0;  
  
        private Dictionary<string, (string Path, bool IsDual)> customTemplates = new Dictionary<string, (string Path, bool IsDual)>(StringComparer.OrdinalIgnoreCase);  
  
        [DllImport("user32.dll", CharSet = CharSet.Auto)]  
        extern static bool DestroyIcon(IntPtr handle);  
        private static Icon? appIcon = null;  
  
        [DllImport("user32.dll", CharSet = CharSet.Auto)]  
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);  
        private const int EM_SETCUEBANNER = 0x1501;  
  
        private void SetPlaceholder(TextBox textBox, string placeholder)  
        {  
            if (textBox.IsHandleCreated) SendMessage(textBox.Handle, EM_SETCUEBANNER, 1, placeholder);  
            else textBox.HandleCreated += (s, e) => SendMessage(textBox.Handle, EM_SETCUEBANNER, 1, placeholder);  
        }  
  
        // ========================================================================  
        // ROBUST CSV PARSER & SANITIZERS  
        // ========================================================================  
        private static string EscapeCsvField(string? value)
        {
            value ??= string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string HtmlEncode(string? value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private string[] ParseCsvLine(string line)  
        {  
            var result = new List<string>();  
            bool inQuotes = false;  
            StringBuilder currentField = new StringBuilder();  
            for (int i = 0; i < line.Length; i++)  
            {  
                char c = line[i];  
                if (c == '\"')  
                {  
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')  
                    {  
                        currentField.Append('\"'); i++;  
                    }  
                    else { inQuotes = !inQuotes; }  
                }  
                else if (c == ',' && !inQuotes)  
                {  
                    result.Add(currentField.ToString());  
                    currentField.Clear();  
                }  
                else { currentField.Append(c); }  
            }  
            result.Add(currentField.ToString());  
            return result.ToArray();  
        }  
  
        // ========================================================================  
        // NETWORK VOYAGE SYNC ENGINE  
        // ========================================================================  
        private string GetNetworkVoyage()  
        {  
            string networkFile = @"Z:\PhotoLab\EHM\GIL\ActiveVoyage.txt";  
            try  
            {  
                if (File.Exists(networkFile))  
                {  
                    string v = Program.SafeReadAllLines(networkFile).FirstOrDefault()?.Trim() ?? "";  
                    if (!string.IsNullOrEmpty(v)) return v;  
                }  
            }  
            catch { }  
            return string.IsNullOrWhiteSpace(config.CurrentVoyage) ? "PO101126" : config.CurrentVoyage;  
        }  
  
        private void SetNetworkVoyage(string voyage)  
        {  
            string networkFile = @"Z:\PhotoLab\EHM\GIL\ActiveVoyage.txt";  
            try  
            {  
                Directory.CreateDirectory(@"Z:\PhotoLab\EHM\GIL");  
                if (File.Exists(networkFile)) {  
                    File.SetAttributes(networkFile, File.GetAttributes(networkFile) & ~FileAttributes.Hidden);  
                }  
                Program.SafeWriteAllText(networkFile, voyage);  
                File.SetAttributes(networkFile, File.GetAttributes(networkFile) | FileAttributes.Hidden);  
            }  
            catch { }  
            config.CurrentVoyage = voyage;  
            config.Save();  
        }  
  
        public MainForm()  
        {  
            this.DoubleBuffered = true;   
              
            config = AppConfig.Load();  
            InitializeCustomComponent();  
            LoadMediaSizes();
            LoadCustomTemplates();  
            SetupFolderWatcher();  
            SetupPrintshopWatcher();  
            UpdateNextCanvasNumber();  
            CheckMachineHealthAsync();  
            CheckLatestDownloadOrder();  
        }  

        public void LoadMediaSizes()
        {
            string currentSelection = cmbPrintSizeInput.Text;
            cmbPrintSizeInput.Items.Clear();
            var sizes = new List<string>();

            try
            {
                if (File.Exists("MediaSizes.txt"))
                {
                    foreach (var line in Program.SafeReadAllLines("MediaSizes.txt"))
                    {
                        string s = line.Trim();
                        if (!string.IsNullOrEmpty(s) && 
                            !s.Equals("8x10 (canvas copy)", StringComparison.OrdinalIgnoreCase) && 
                            !s.Equals("8x10 (Canvas Copy)", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!sizes.Contains(s, StringComparer.OrdinalIgnoreCase))
                                sizes.Add(s);
                        }
                    }
                }
            }
            catch { }

            if (sizes.Count == 0)
            {
                sizes.AddRange(DefaultMediaSizes);
                try { Program.SafeWriteAllText("MediaSizes.txt", string.Join(Environment.NewLine, sizes)); } catch { }
            }

            foreach (var size in sizes)
            {
                cmbPrintSizeInput.Items.Add(size);
            }

            if (!string.IsNullOrEmpty(currentSelection) && cmbPrintSizeInput.Items.Contains(currentSelection))
                cmbPrintSizeInput.Text = currentSelection;
            else if (cmbPrintSizeInput.Items.Count > 0)
                cmbPrintSizeInput.Text = cmbPrintSizeInput.Items.Contains("4-Wallets (vert)") ? "4-Wallets (vert)" : cmbPrintSizeInput.Items[0].ToString();
        }
  
        private void CheckLatestDownloadOrder()  
        {  
            try  
            {  
                string downloadsDir = config.DownloadsFolder;  
                if (!string.IsNullOrWhiteSpace(downloadsDir) && Directory.Exists(downloadsDir))  
                {  
                    var latestDir = new DirectoryInfo(downloadsDir).GetDirectories()  
                                    .OrderByDescending(d => d.CreationTime)  
                                    .FirstOrDefault();  
                    if (latestDir != null)  
                    {  
                        lblLastOrder.Text = "Latest Order:" + Environment.NewLine + "Cabin " + latestDir.Name;  
                        lblLastOrder.ForeColor = Color.FromArgb(120, 240, 150);  
                    }  
                }  
            }  
            catch { }  
        }  
  
        private void LoadCustomTemplates()  
        {  
            customTemplates.Clear();  
            try  
            {  
                if (File.Exists("CustomTemplates.txt"))  
                {  
                    foreach (var line in Program.SafeReadAllLines("CustomTemplates.txt"))  
                    {  
                        var parts = line.Split('|');  
                        if (parts.Length >= 3) customTemplates[parts[0].Trim()] = (parts[1].Trim(), parts[2].Trim().Equals("True", StringComparison.OrdinalIgnoreCase));  
                    }  
                }  
            }  
            catch { }  
              
            foreach (var key in customTemplates.Keys)  
            {  
                if (!cmbPrintSizeInput.Items.Contains(key)) cmbPrintSizeInput.Items.Add(key);  
            }  
        }  
  
        private void GenerateAndSetAppIcon()  
        {  
            if (appIcon == null)  
            {  
                using (Bitmap bmp = new Bitmap(128, 128))  
                {  
                    using (Graphics g = Graphics.FromImage(bmp))  
                    {  
                        g.Clear(Color.FromArgb(18, 19, 22));   
                        g.SmoothingMode = SmoothingMode.AntiAlias;  
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;  
  
                        using (Font fontPixels = new Font("Segoe UI", 28, FontStyle.Bold))  
                        {  
                            SizeF textSize = g.MeasureString("pixels", fontPixels);  
                            g.DrawString("pixels", fontPixels, Brushes.White, new PointF((128 - textSize.Width) / 2, 25));  
                        }  
  
                        int segWidth = 16, segHeight = 8;  
                        int totalWidth = logoStripeColors.Length * segWidth;  
                        int barX = (128 - totalWidth) / 2, barY = 75;  
  
                        for (int i = 0; i < logoStripeColors.Length; i++)  
                        {  
                            using (Brush b = new SolidBrush(logoStripeColors[i]))  
                            {  
                                g.FillRectangle(b, barX + (i * segWidth), barY, segWidth, segHeight);  
                            }  
                        }  
                    }  
                    IntPtr hIcon = bmp.GetHicon();  
                    appIcon = (Icon)Icon.FromHandle(hIcon).Clone();  
                    DestroyIcon(hIcon);  
                }  
  
                try  
                {  
                    if (!File.Exists("labtools_icon.ico") && appIcon != null)  
                        using (var fs = new FileStream("labtools_icon.ico", FileMode.Create)) { appIcon.Save(fs); }  
                }  
                catch { }  
            }  
            this.Icon = appIcon;  
        }  
  
        private void InitializeCustomComponent()  
        {  
            this.Text = AppTitle;  
              
            this.Size = new Size(930, 850);  
            this.MinimumSize = new Size(930, 850);  
            this.MaximumSize = new Size(930, 850);  
            this.FormBorderStyle = FormBorderStyle.FixedSingle;  
            this.MaximizeBox = false;  
            this.StartPosition = FormStartPosition.CenterScreen;  
            this.BackColor = darkBackground;  
  
            GenerateAndSetAppIcon();  

            this.AllowDrop = true;
            this.DragEnter += HandleFileDragEnter;
            this.DragDrop += HandleFileDragDrop;
  
            Panel topBar = new Panel() { Location = new Point(0, 0), Width = 930, Height = 46, BackColor = Color.FromArgb(22, 23, 27) };  
            Label lblTitle = new Label() { Text = AppTitle, Location = new Point(15, 12), ForeColor = textWhite, Font = new Font("Segoe UI", 11f, FontStyle.Bold), AutoSize = true };  
            Button btnHistory = new Button() { Text = "Voyage Order History", Location = new Point(590, 8), Width = 170, Height = 30, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };  
            btnHistory.Click += (s, e) => { OpenOrderHistoryDialog(); };  
            Button btnSettings = new Button() { Text = "Settings / Folders", Location = new Point(770, 8), Width = 140, Height = 30, BackColor = accentTeal, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };  
            btnSettings.Click += (s, e) => { OpenSettingsDialog(); };  
              
            topBar.Controls.Add(lblTitle);  
            topBar.Controls.Add(btnHistory);  
            topBar.Controls.Add(btnSettings);  
            this.Controls.Add(topBar);  
  
            txtSearch = new TextBox() { Text = "", Location = new Point(20, 58), Width = 290, Height = 30, Font = new Font("Segoe UI", 10) };  
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SearchFilesAsync(txtSearch.Text); e.Handled = true; e.SuppressKeyPress = true; } };  
              
            btnFind = new Button() { Text = "Find", Location = new Point(320, 56), Width = 85, Height = 32, BackColor = accentBlue, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            btnFind.Click += (s, e) => { SearchFilesAsync(txtSearch.Text); };  
  
            btnCancelSearch = new Button() { Text = "Cancel", Location = new Point(410, 56), Width = 75, Height = 32, BackColor = Color.FromArgb(180, 50, 50), ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Enabled = false, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };  
            btnCancelSearch.Click += (s, e) => { searchCts?.Cancel(); btnCancelSearch.Enabled = false; SetStatus("Search cancelled by operator."); };  
  
            Button btnClearAll = new Button() { Text = "Clear All", Location = new Point(490, 56), Width = 90, Height = 32, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f) };  
            btnClearAll.Click += (s, e) => {   
                txtSearch.Clear(); lstFiles.Items.Clear(); ResetLogoPreview();   
                txtCabin.Clear(); txtCanvasCabin.Clear(); txtPrintCabinNum.Clear(); txtSeller.Clear();  
                if (numPrintQty != null) numPrintQty.Value = 1;  
                lblFilesFound.Text = "0 Files Found"; SetStatus("Cleared all search results, cabin numbers, and seller.");   
            };  
  
            Button btnOtherSource = new Button() { Text = "Select From Other Source (JPG)", Location = new Point(20, 98), Width = 230, Height = 35, BackColor = accentTeal, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };  
            btnOtherSource.Click += (s, e) => { SelectFilesFromOtherSource(); };  
  
            Button btnExcelList = new Button() { Text = "Import From CSV List / Clipboard", Location = new Point(260, 98), Width = 235, Height = 35, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };  
            btnExcelList.Click += (s, e) => { ImportExcelOrClipboardList(); };  
  
            lstFiles = new CheckedListBox() { Location = new Point(20, 142), Width = 340, Height = 145, BackColor = Color.FromArgb(70, 70, 70), ForeColor = textWhite, AllowDrop = true };  
            lstFiles.SelectedIndexChanged += (s, e) => { PreviewSelectedImageSafe(); };  
            lstFiles.DragEnter += HandleFileDragEnter;
            lstFiles.DragDrop += HandleFileDragDrop;
              
            Button btnSelectAll = new Button() { Text = "Select All", Location = new Point(370, 142), Width = 210, Height = 32, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat };  
            btnSelectAll.Click += (s, e) => { lstFiles.BeginUpdate(); for (int i = 0; i < lstFiles.Items.Count; i++) lstFiles.SetItemChecked(i, true); lstFiles.EndUpdate(); };  
  
            Button btnUnselectAll = new Button() { Text = "Unselect All", Location = new Point(370, 180), Width = 210, Height = 32, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat };  
            btnUnselectAll.Click += (s, e) => { lstFiles.BeginUpdate(); for (int i = 0; i < lstFiles.Items.Count; i++) lstFiles.SetItemChecked(i, false); lstFiles.EndUpdate(); };  
  
            lblFilesFound = new Label() { Text = "0 Files Found", Location = new Point(370, 222), Width = 210, Height = 25, ForeColor = textWhite, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) };  
  
            progressBar = new ProgressBar() { Location = new Point(20, 292), Width = 560, Height = 8, Style = ProgressBarStyle.Continuous, Visible = false };  
  
            Label lblMachine = new Label() { Text = "Select Machine:", Location = new Point(20, 312), ForeColor = textWhite, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            cmbMachine = new ComboBox() { Location = new Point(130, 310), Width = 160, Height = 25, DropDownStyle = ComboBoxStyle.DropDownList };  
            cmbMachine.Items.AddRange(new string[] { "Machine A", "Machine B", "Machine C", "Machine D" });  
            cmbMachine.SelectedIndex = 2;  
            cmbMachine.SelectedIndexChanged += (s, e) => { CheckMachineHealthAsync(); };  
  
            lblMachineStatus = new Label() { Text = "● Checking...", Location = new Point(300, 313), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };  
  
            Label lblUsb = new Label() { Text = "USB Drive", Location = new Point(20, 347), ForeColor = textWhite, AutoSize = true };  
            Label lblUsbCabin = new Label() { Text = "Cabin", Location = new Point(160, 347), ForeColor = textWhite, AutoSize = true };  
              
            txtUsbDrive = new TextBox() { Text = "D", Location = new Point(20, 372), Width = 120, Height = 25, TextAlign = HorizontalAlignment.Center };  
            txtCabin = new TextBox() { Text = "", Location = new Point(155, 372), Width = 120, Height = 25 };   
            SetPlaceholder(txtCabin, "cabin");  
  
            Button btnCopyUsb = new Button() { Text = "Copy To USB", Location = new Point(290, 345), Width = 135, Height = 52, BackColor = accentTeal, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            btnCopyUsb.Click += (s, e) => { CopyCheckedFilesToUsb(); };  
  
            Button btnCopyDownloads = new Button() { Text = "Copy To Downloads", Location = new Point(435, 345), Width = 135, Height = 52, BackColor = accentTeal, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            btnCopyDownloads.Click += (s, e) => { CopyToDownloadsFolder(); };  
  
            lblCanvasHeader = new Label() { Text = "Next Canvas No. 001", Location = new Point(20, 417), ForeColor = Color.FromArgb(130, 220, 210), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };  
            txtCanvasCabin = new TextBox() { Text = "", Location = new Point(245, 414), Width = 115, Height = 25 };   
            SetPlaceholder(txtCanvasCabin, "cabin");  
  
            txtSeller = new TextBox() { Text = "", Location = new Point(245, 444), Width = 115, Height = 25 };  
            SetPlaceholder(txtSeller, "seller");  
  
            cmbCanvasSize = new ComboBox() { Location = new Point(20, 444), Width = 215, Height = 25, DropDownStyle = ComboBoxStyle.DropDownList };  
            cmbCanvasSize.Items.AddRange(new string[] { "16x20", "20x30", "11x14", "8x10" });  
            cmbCanvasSize.SelectedIndex = 0;  
  
            Button btnPostCanvas = new Button() { Text = "Post Canvas Order", Location = new Point(370, 414), Width = 200, Height = 55, BackColor = accentTeal, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };  
            btnPostCanvas.Click += async (s, e) => { await PostCanvasOrderAsync(); };  
  
            Label lblPrintCabinLabel = new Label() { Text = "Cabin", Location = new Point(20, 490), ForeColor = textWhite, AutoSize = true };  
            txtPrintCabinNum = new TextBox() { Text = "", Location = new Point(65, 487), Width = 80, Height = 25, TextAlign = HorizontalAlignment.Center };  
            SetPlaceholder(txtPrintCabinNum, "cabin");  
  
            lblPrintQty = new Label() { Text = "Qty", Location = new Point(155, 490), ForeColor = textWhite, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            numPrintQty = new NumericUpDown() { Location = new Point(185, 487), Width = 50, Height = 25, Minimum = 1, Maximum = 99, Value = 1, TextAlign = HorizontalAlignment.Center, Font = new Font("Segoe UI", 9.5f) };  
  
            cmbPrintSizeInput = new ComboBox() { Location = new Point(245, 487), Width = 325, Height = 25, DropDownStyle = ComboBoxStyle.DropDown };  
  
            txtCabin.TextChanged += (s, e) => { if (isSyncingCabin) return; isSyncingCabin = true; txtCanvasCabin.Text = txtCabin.Text; txtPrintCabinNum.Text = txtCabin.Text; isSyncingCabin = false; };  
            txtCanvasCabin.TextChanged += (s, e) => { if (isSyncingCabin) return; isSyncingCabin = true; txtCabin.Text = txtCanvasCabin.Text; txtPrintCabinNum.Text = txtCanvasCabin.Text; isSyncingCabin = false; };  
            txtPrintCabinNum.TextChanged += (s, e) => { if (isSyncingCabin) return; isSyncingCabin = true; txtCabin.Text = txtPrintCabinNum.Text; txtCanvasCabin.Text = txtPrintCabinNum.Text; isSyncingCabin = false; };  
  
            chkAuto4W = new CheckBox() { Text = "Auto 4W", Location = new Point(30, 527), Width = 120, Height = 25, ForeColor = textWhite };  
              
            cmbOrderType = new ComboBox() { Location = new Point(20, 557), Width = 270, Height = 25, DropDownStyle = ComboBoxStyle.DropDown };  
            cmbOrderType.Items.AddRange(new string[] { "Package", "Manual Order", "Express Order", "Online Order" });  
            cmbOrderType.Text = "Package";  
  
            Button btnPrint = new Button() { Text = "Print", Location = new Point(300, 527), Width = 270, Height = 55, BackColor = accentTeal, ForeColor = textWhite, Font = new Font("Segoe UI", 12, FontStyle.Bold), FlatStyle = FlatStyle.Flat };  
            btnPrint.Click += (s, e) => { ExecutePrintJobAsync(); };  
  
            lblStatusLeft = new Label() { Text = "Ready", Location = new Point(20, 765), Width = 560, Height = 30, ForeColor = textWhite, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11, FontStyle.Bold) };  
  
            Panel rightPanel = new Panel() { Location = new Point(600, 55), Width = 310, Height = 745, BackColor = panelBackground };  
  
            picLogo = new PictureBox() { Location = new Point(15, 15), Width = 280, Height = 210, BackColor = Color.FromArgb(18, 19, 22), SizeMode = PictureBoxSizeMode.Zoom };  
            ResetLogoPreview();  
  
            Label lblCrop = new Label() { Text = "Aspect Ratio Crop Guide:", Location = new Point(15, 235), ForeColor = textWhite, AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };  
            cmbAspectCrop = new ComboBox() { Location = new Point(15, 255), Width = 280, Height = 25, DropDownStyle = ComboBoxStyle.DropDownList };  
            cmbAspectCrop.Items.AddRange(new string[] { "Full Frame (No Crop)", "8x10 Ratio (4:5)", "4x6 Ratio (2:3)", "5x7 Ratio (5:7)", "16x20 Canvas (+1.5in Wrap)" });  
            cmbAspectCrop.SelectedIndex = 0;  
            cmbAspectCrop.SelectedIndexChanged += (s, e) => { PreviewSelectedImageSafe(); };  
  
            Panel alertBox = new Panel() { Location = new Point(15, 310), Width = 280, Height = 95, BackColor = Color.FromArgb(30, 31, 35), BorderStyle = BorderStyle.FixedSingle };  
            Label lblOrderHeader = new Label() { Text = "DIGITAL ORDER MONITOR", Location = new Point(5, 8), Width = 268, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };  
            lblLastOrder = new Label() { Text = "Waiting for orders...", Location = new Point(5, 35), Width = 268, Height = 45, ForeColor = textWhite, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) };  
            alertBox.Controls.Add(lblOrderHeader);  
            alertBox.Controls.Add(lblLastOrder);  
  
            lblStatusRight = new Label() { Text = "Ready", Location = new Point(15, 695), Width = 280, Height = 30, ForeColor = textWhite, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11, FontStyle.Bold) };  
  
            rightPanel.Controls.Add(picLogo);  
            rightPanel.Controls.Add(lblCrop);  
            rightPanel.Controls.Add(cmbAspectCrop);  
            rightPanel.Controls.Add(alertBox);  
            rightPanel.Controls.Add(lblStatusRight);  
  
            this.Controls.Add(txtSearch);  
            this.Controls.Add(btnFind);  
            this.Controls.Add(btnCancelSearch);  
            this.Controls.Add(btnClearAll);  
            this.Controls.Add(btnOtherSource);  
            this.Controls.Add(btnExcelList);  
            this.Controls.Add(lstFiles);  
            this.Controls.Add(btnSelectAll);  
            this.Controls.Add(btnUnselectAll);  
            this.Controls.Add(lblFilesFound);  
            this.Controls.Add(progressBar);  
            this.Controls.Add(lblMachine);  
            this.Controls.Add(cmbMachine);  
            this.Controls.Add(lblMachineStatus);  
            this.Controls.Add(lblUsb);  
            this.Controls.Add(lblUsbCabin);  
            this.Controls.Add(txtUsbDrive);  
            this.Controls.Add(txtCabin);  
            this.Controls.Add(btnCopyUsb);  
            this.Controls.Add(btnCopyDownloads);  
            this.Controls.Add(lblCanvasHeader);  
            this.Controls.Add(txtCanvasCabin);  
            this.Controls.Add(txtSeller);  
            this.Controls.Add(cmbCanvasSize);  
            this.Controls.Add(btnPostCanvas);  
            this.Controls.Add(lblPrintCabinLabel);  
            this.Controls.Add(txtPrintCabinNum);  
            this.Controls.Add(lblPrintQty);  
            this.Controls.Add(numPrintQty);  
            this.Controls.Add(cmbPrintSizeInput);  
            this.Controls.Add(chkAuto4W);  
            this.Controls.Add(cmbOrderType);  
            this.Controls.Add(btnPrint);  
            this.Controls.Add(lblStatusLeft);  
            this.Controls.Add(rightPanel);  
        }  

        private void HandleFileDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void HandleFileDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[]? dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropped == null || dropped.Length == 0) return;

            var filesToAdd = new List<string>();
            string detectedCabin = string.Empty;

            foreach (string path in dropped)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (File.Exists(path))
                {
                    string fName = Path.GetFileName(path);
                    if (fName.IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    string ext = Path.GetExtension(path);
                    if (ImageExtensions.Contains(ext) || ExactPhotoRegex.IsMatch(fName))
                        filesToAdd.Add(path);

                    if (string.IsNullOrEmpty(detectedCabin))
                    {
                        string dirName = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
                        if (CabinSearchRegex.IsMatch(dirName)) detectedCabin = dirName;
                    }
                }
                else if (Directory.Exists(path))
                {
                    string dirName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (CabinSearchRegex.IsMatch(dirName)) detectedCabin = dirName;

                    foreach (string f in FastEnumerateImageFiles(path, CancellationToken.None))
                    {
                        if (Path.GetFileName(f).IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        filesToAdd.Add(f);
                    }
                }
            }

            int addedCount = 0;
            lstFiles.BeginUpdate();
            foreach (string filePath in filesToAdd)
            {
                bool exists = lstFiles.Items.Cast<FileItem>().Any(i => i.FullPath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    lstFiles.Items.Add(new FileItem { FileName = Path.GetFileName(filePath), FullPath = filePath }, true);
                    addedCount++;
                }
            }
            lstFiles.EndUpdate();

            if (!string.IsNullOrEmpty(detectedCabin) && string.IsNullOrEmpty(txtCabin.Text.Trim()))
            {
                txtCabin.Text = detectedCabin;
            }

            lblFilesFound.Text = lstFiles.Items.Count + " Files Total";
            SetStatus("Added " + addedCount + " photo(s) via drag-and-drop.");

            if (lstFiles.Items.Count > 0)
            {
                lstFiles.SelectedIndex = lstFiles.Items.Count - 1;
                PreviewSelectedImageSafe();
            }
        }
  
        private void SelectFilesFromOtherSource()  
        {  
            using (OpenFileDialog ofd = new OpenFileDialog())  
            {  
                ofd.Title = "Select JPG / Image Files to Add and Print";  
                ofd.Filter = "JPEG Images (*.jpg;*.jpeg)|*.jpg;*.jpeg|All Images (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tif)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tif|All Files (*.*)|*.*";  
                ofd.Multiselect = true;  
  
                if (ofd.ShowDialog() == DialogResult.OK)  
                {  
                    int addedCount = 0;  
                    lstFiles.BeginUpdate();   
                    foreach (string filePath in ofd.FileNames)  
                    {  
                        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) continue;  
                        if (Path.GetFileName(filePath).IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) >= 0) continue;  
  
                        bool exists = lstFiles.Items.Cast<FileItem>().Any(i => i.FullPath.Equals(filePath, StringComparison.OrdinalIgnoreCase));  
                        if (!exists)  
                        {  
                            lstFiles.Items.Add(new FileItem { FileName = Path.GetFileName(filePath), FullPath = filePath }, true);  
                            addedCount++;  
                        }  
                    }  
                    lstFiles.EndUpdate();  
  
                    lblFilesFound.Text = lstFiles.Items.Count + " Files Total";  
                    SetStatus("Added " + addedCount + " image(s) from other source. Selected for print.");  
  
                    if (lstFiles.Items.Count > 0)  
                    {  
                        lstFiles.SelectedIndex = lstFiles.Items.Count - 1;  
                        PreviewSelectedImageSafe();  
                    }  
  
                    if (addedCount > 0 && !string.IsNullOrWhiteSpace(config.DownloadsFolder))  
                    {  
                        try  
                        {  
                            string firstFile = ofd.FileNames[0];  
                            string dir = Path.GetDirectoryName(firstFile) ?? "";  
                            string dlFolder = Path.GetFullPath(config.DownloadsFolder).TrimEnd('\\');  
                            string fullDir = Path.GetFullPath(dir).TrimEnd('\\');  
  
                            if (fullDir.StartsWith(dlFolder, StringComparison.OrdinalIgnoreCase) && fullDir.Length > dlFolder.Length)  
                            {  
                                string folderName = Path.GetFileName(fullDir);  
                                if (CabinSearchRegex.IsMatch(folderName))  
                                {  
                                    txtCabin.Text = folderName;  
                                    SetStatus("Auto-extracted Cabin " + folderName + " from Downloads folder path.");  
                                }  
                            }  
                        }  
                        catch { }  
                    }  
                }  
            }  
        }  
  
        // ========================================================================  
        // 2. ASYNC SEARCH ENGINE (CANCELLABLE + EXACT-MATCH FAST PATH)  
        // ========================================================================  
        private async void SearchFilesAsync(string query)  
        {  
            int thisSearch = Interlocked.Increment(ref searchGeneration);
            searchCts?.Cancel();  
            searchCts = new CancellationTokenSource();  
            var token = searchCts.Token;  
  
            lstFiles.BeginUpdate();  
            lstFiles.Items.Clear();  
            lstFiles.EndUpdate();  
            ResetLogoPreview();  
            query = query?.Trim() ?? "";  
  
            if (string.IsNullOrWhiteSpace(query) || query == "FileName - OrderID - Cabin No")  
            {  
                lblFilesFound.Text = "0 Files Found";  
                btnFind.Enabled = true;
                btnCancelSearch.Enabled = false;
                progressBar.Visible = false;
                SetStatus("Ready");  
                return;  
            }  
  
            btnFind.Enabled = false;  
            btnCancelSearch.Enabled = true;  
            progressBar.Visible = true;  
            progressBar.Style = ProgressBarStyle.Marquee;  
            SetStatus("Searching for '" + query + "'...");  
  
            try  
            {  
                var searchResult = await Task.Run(() =>  
                {  
                    var results = new List<string>();  
  
                    string ordersDir = !string.IsNullOrWhiteSpace(config.OrdersFolder) ? config.OrdersFolder : @"Z:\PixelsOrder\Processed";  
                    string printedDir = !string.IsNullOrWhiteSpace(config.PrintedFolder) ? config.PrintedFolder : @"Z:\Printed";  
                    string downloadsDir = !string.IsNullOrWhiteSpace(config.DownloadsFolder) ? config.DownloadsFolder : @"C:\Downloads";  
  
                    string cleanQuery = query.Trim();  
                    string queryWithoutExt = cleanQuery;  
                    if (queryWithoutExt.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))  
                        queryWithoutExt = queryWithoutExt.Substring(0, queryWithoutExt.Length - 4).Trim();  
                    else if (queryWithoutExt.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || queryWithoutExt.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))  
                        queryWithoutExt = queryWithoutExt.Substring(0, queryWithoutExt.LastIndexOf('.')).Trim();  
  
                    if (CabinSearchRegex.IsMatch(cleanQuery))  
                    {  
                        if (Directory.Exists(downloadsDir))  
                        {  
                            string directCabinFolder = Path.Combine(downloadsDir, cleanQuery);  
                            var candidateFolders = new List<string>();  
                            if (Directory.Exists(directCabinFolder))  
                            {  
                                candidateFolders.Add(directCabinFolder);  
                            }  
                            else  
                            {  
                                var pendingDirs = new Stack<string>();  
                                pendingDirs.Push(downloadsDir);  
                                while (pendingDirs.Count > 0)  
                                {  
                                    if (token.IsCancellationRequested) break;  
                                    string currDir = pendingDirs.Pop();  
                                    try  
                                    {  
                                        foreach (var d in Directory.EnumerateDirectories(currDir))  
                                        {  
                                            if (Path.GetFileName(d).Equals(cleanQuery, StringComparison.OrdinalIgnoreCase))  
                                                candidateFolders.Add(d);  
                                            else  
                                                pendingDirs.Push(d);  
                                        }  
                                    }  
                                    catch { }  
                                }  
                            }  
  
                            foreach (string folder in candidateFolders)  
                            {  
                                foreach (string f in FastEnumerateImageFiles(folder, token))  
                                {  
                                    if (Path.GetFileName(f).IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) >= 0) continue;  
                                    results.Add(f);  
                                }  
                            }  
                        }  
                        return new OrderSearchResult { IsOrder = false, Cabin = cleanQuery, Files = results };  
                    }  
  
                    if (OrderSearchRegex.IsMatch(queryWithoutExt))  
                    {  
                        string? orderFilePath = null;  
                        if (Directory.Exists(ordersDir))  
                        {  
                            string direct1 = Path.Combine(ordersDir, queryWithoutExt + ".txt");  
                            string direct2 = Path.Combine(ordersDir, cleanQuery);  
                            if (File.Exists(direct1)) orderFilePath = direct1;  
                            else if (File.Exists(direct2)) orderFilePath = direct2;  
                            else  
                            {  
                                var pendingDirs = new Stack<string>();  
                                pendingDirs.Push(ordersDir);  
  
                                while (pendingDirs.Count > 0 && orderFilePath == null)  
                                {  
                                    if (token.IsCancellationRequested) break;  
                                    string currDir = pendingDirs.Pop();  
                                    try  
                                    {  
                                        var match = Directory.EnumerateFiles(currDir, "*" + queryWithoutExt + "*.txt").FirstOrDefault();  
                                        if (match != null) { orderFilePath = match; break; }  
                                    }  
                                    catch { }  
                                    try { foreach (var d in Directory.EnumerateDirectories(currDir)) pendingDirs.Push(d); } catch { }  
                                }  
                            }  
                        }  
  
                        if (orderFilePath != null && File.Exists(orderFilePath))  
                        {  
                            string orderText = File.ReadAllText(orderFilePath);  
                            var targetPhotos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);  
                            var targetPhotosWithoutExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);  
  
                            string[] lines = orderText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);  
                            foreach (var line in lines)  
                            {  
                                int fpIdx = line.IndexOf("FilePath", StringComparison.OrdinalIgnoreCase);  
                                if (fpIdx >= 0)  
                                {  
                                    int eqIdx = line.IndexOf('=', fpIdx);  
                                    if (eqIdx >= 0)  
                                    {  
                                        string rawPath = line.Substring(eqIdx + 1).Trim().Trim('"', '\'');  
                                        string fName = Path.GetFileName(rawPath);  
                                        if (!string.IsNullOrEmpty(fName))  
                                        {  
                                            targetPhotos.Add(fName);  
                                            targetPhotosWithoutExt.Add(Path.GetFileNameWithoutExtension(fName));  
                                        }  
                                    }  
                                }  
                            }  
  
                            foreach (Match m in PhotoRegex.Matches(orderText))  
                            {  
                                string fName = Path.GetFileName(m.Groups[1].Value.Trim());  
                                if (!string.IsNullOrEmpty(fName) && fName.IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) < 0)  
                                {  
                                    targetPhotos.Add(fName);  
                                    targetPhotosWithoutExt.Add(Path.GetFileNameWithoutExtension(fName));  
                                }  
                            }  
  
                            var cabinMatch = CabinMatchRegex.Match(orderText);  
                            string extractedCabin = cabinMatch.Success ? cabinMatch.Groups[1].Value.Trim() : string.Empty;  
  
                            var sizeMatch = SizeMatchRegex.Match(orderText);  
                            string extractedSize = sizeMatch.Success ? sizeMatch.Groups[1].Value.Trim() : string.Empty;  
  
                            if (Directory.Exists(printedDir) && targetPhotosWithoutExt.Count > 0)  
                            {  
                                foreach (string file in FastEnumerateImageFiles(printedDir, token))  
                                {  
                                    string fileName = Path.GetFileName(file);  
                                    string fileNameNoExt = Path.GetFileNameWithoutExtension(file);  
  
                                    if (targetPhotos.Contains(fileName) || targetPhotosWithoutExt.Contains(fileNameNoExt))  
                                    {  
                                        results.Add(file);  
                                        targetPhotos.Remove(fileName);  
                                        targetPhotosWithoutExt.Remove(fileNameNoExt);  
                                        if (targetPhotosWithoutExt.Count == 0) break;  
                                    }  
                                }  
                            }  
  
                            return new OrderSearchResult  
                            {  
                                IsOrder = true,  
                                OrderId = Path.GetFileNameWithoutExtension(orderFilePath),  
                                Cabin = extractedCabin,  
                                Size = extractedSize,  
                                Files = results  
                            };  
                        }  
                          
                        return new OrderSearchResult { IsOrder = true, OrderId = queryWithoutExt, Cabin = string.Empty, Files = results };  
                    }  
  
                    string[] tokens = cleanQuery.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)  
                                           .Select(t => t.Replace(" ", "").Trim())  
                                           .Where(t => !string.IsNullOrEmpty(t))  
                                           .ToArray();  
  
                    if (tokens.Length > 0)  
                    {  
                        var targetTokens = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);  

                        string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".webp" };
                        foreach (string root in new[] { downloadsDir, printedDir }.Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            if (!Directory.Exists(root)) continue;
                            foreach (string tokenName in targetTokens.ToArray())
                            {
                                token.ThrowIfCancellationRequested();
                                string safeName = Path.GetFileName(tokenName);
                                string direct = Path.Combine(root, safeName);
                                string? match = File.Exists(direct) ? direct : null;
                                if (match == null && string.IsNullOrEmpty(Path.GetExtension(safeName)))
                                {
                                    foreach (string ext in imageExtensions)
                                    {
                                        string candidate = Path.Combine(root, safeName + ext);
                                        if (File.Exists(candidate)) { match = candidate; break; }
                                    }
                                }
                                if (match != null)
                                {
                                    results.Add(match);
                                    targetTokens.Remove(tokenName);
                                }
                            }
                        }
  
                        if (targetTokens.Count > 0 && Directory.Exists(downloadsDir))  
                        {  
                            foreach (string file in FastEnumerateImageFiles(downloadsDir, token))  
                            {  
                                string fileName = Path.GetFileName(file);  
                                if (fileName.IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) >= 0)  
                                    continue;  
  
                                string fileNameNoExt = Path.GetFileNameWithoutExtension(file);  
  
                                string? matchedToken = targetTokens.FirstOrDefault(tk =>   
                                    fileName.Equals(tk, StringComparison.OrdinalIgnoreCase) ||   
                                    fileNameNoExt.Equals(tk, StringComparison.OrdinalIgnoreCase));  
  
                                if (matchedToken == null)  
                                    matchedToken = targetTokens.FirstOrDefault(tk => fileName.IndexOf(tk, StringComparison.OrdinalIgnoreCase) >= 0);  
  
                                if (matchedToken != null)  
                                {  
                                    results.Add(file);  
                                    if (fileName.Equals(matchedToken, StringComparison.OrdinalIgnoreCase) || fileNameNoExt.Equals(matchedToken, StringComparison.OrdinalIgnoreCase))  
                                    {  
                                        targetTokens.Remove(matchedToken);  
                                        if (targetTokens.Count == 0) break;   
                                    }  
                                }  
                            }  
                        }  
  
                        if (targetTokens.Count > 0 && Directory.Exists(printedDir))  
                        {  
                            foreach (string file in FastEnumerateImageFiles(printedDir, token))  
                            {  
                                string fileName = Path.GetFileName(file);  
                                if (fileName.IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) >= 0)  
                                    continue;  
  
                                string fileNameNoExt = Path.GetFileNameWithoutExtension(file);  
  
                                string? matchedToken = targetTokens.FirstOrDefault(tk =>   
                                    fileName.Equals(tk, StringComparison.OrdinalIgnoreCase) ||   
                                    fileNameNoExt.Equals(tk, StringComparison.OrdinalIgnoreCase));  
  
                                if (matchedToken == null)  
                                    matchedToken = targetTokens.FirstOrDefault(tk => fileName.IndexOf(tk, StringComparison.OrdinalIgnoreCase) >= 0);  
  
                                if (matchedToken != null)  
                                {  
                                    results.Add(file);  
                                    if (fileName.Equals(matchedToken, StringComparison.OrdinalIgnoreCase) || fileNameNoExt.Equals(matchedToken, StringComparison.OrdinalIgnoreCase))  
                                    {  
                                        targetTokens.Remove(matchedToken);  
                                        if (targetTokens.Count == 0) break;  
                                    }  
                                }  
                            }  
                        }  
                    }  
  
                    return new OrderSearchResult { IsOrder = false, Cabin = null, Files = results };  
  
                }, token);  
  
                token.ThrowIfCancellationRequested();
                if (thisSearch != Volatile.Read(ref searchGeneration)) return;

                lstFiles.BeginUpdate();  
                int count = 0;  
                foreach (string filePath in searchResult.Files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase))  
                {  
                    lstFiles.Items.Add(new FileItem { FileName = Path.GetFileName(filePath), FullPath = filePath }, true);  
                    count++;  
                }  
                lstFiles.EndUpdate();  
  
                if (!string.IsNullOrEmpty(searchResult.Cabin)) txtCabin.Text = searchResult.Cabin;  
                if (!string.IsNullOrEmpty(searchResult.Size))  
                {  
                    if (customTemplates.ContainsKey(searchResult.Size))  
                    {  
                        cmbPrintSizeInput.Text = searchResult.Size;  
                    }  
                    else if (searchResult.Size.IndexOf("mini porthole", StringComparison.OrdinalIgnoreCase) >= 0)  
                    {  
                        cmbPrintSizeInput.Text = "8X10 Mini PORTHOLE";  
                    }  
                    else if (searchResult.Size.IndexOf("porthole", StringComparison.OrdinalIgnoreCase) >= 0 ||  
                             searchResult.Size.IndexOf("port hole", StringComparison.OrdinalIgnoreCase) >= 0)  
                    {  
                        cmbPrintSizeInput.Text = "8x10 PORTHOLE 8in";  
                    }  
                    else  
                    {  
                        if (!cmbPrintSizeInput.Items.Contains(searchResult.Size)) cmbPrintSizeInput.Items.Add(searchResult.Size);  
                        cmbPrintSizeInput.Text = searchResult.Size;  
                    }  
                }  
                lblFilesFound.Text = count + " Files Found";  
  
                if (searchResult.IsOrder)  
                {  
                    string printedPath = !string.IsNullOrWhiteSpace(config.PrintedFolder) ? config.PrintedFolder : @"Z:\Printed";  
                    SetStatus("Found Order " + searchResult.OrderId + " with " + count + " photo(s) (ready to print).");  
                }  
                else  
                {  
                    SetStatus("Ready - Found " + count + " matching files.");  
                }  
  
                if (lstFiles.Items.Count > 0)  
                {  
                    lstFiles.SelectedIndex = 0;  
                    PreviewSelectedImageSafe();  
                }  
                else  
                {  
                    if (searchResult.IsOrder) SetStatus("Order " + searchResult.OrderId + " found, but photos are not yet in Z:\\Printed.");  
                }  
            }  
            catch (OperationCanceledException)
            {
                if (thisSearch == Volatile.Read(ref searchGeneration)) SetStatus("Search cancelled by operator.");
            }
            catch (Exception ex)
            {
                if (thisSearch == Volatile.Read(ref searchGeneration))
                {
                    MessageBox.Show("Search Error: " + ex.Message, "Search Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SetStatus("Search failed.");
                }
            }
            finally
            {
                if (thisSearch == Volatile.Read(ref searchGeneration))
                {
                    btnFind.Enabled = true;
                    btnCancelSearch.Enabled = false;
                    progressBar.Visible = false;
                }
            }  
        }  
  
        private static IEnumerable<string> FastEnumerateImageFiles(string root, CancellationToken token)  
        {  
            var pending = new Stack<string>();  
            pending.Push(root);  
            while (pending.Count > 0)  
            {  
                if (token.IsCancellationRequested) yield break;  
                string current = pending.Pop();  
  
                IEnumerable<string>? files = null;  
                try { files = Directory.EnumerateFiles(current); } catch { }  
  
                if (files != null)  
                {  
                    foreach (string file in files)  
                    {  
                        if (token.IsCancellationRequested) yield break;  
                        string fileName = Path.GetFileName(file);  
                        if (fileName.IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) >= 0) continue;  
                        string ext = Path.GetExtension(file);  
  
                        if (ImageExtensions.Contains(ext) || ExactPhotoRegex.IsMatch(fileName))  
                        {  
                            yield return file;  
                        }  
                    }  
                }  
  
                IEnumerable<string>? dirs = null;  
                try { dirs = Directory.EnumerateDirectories(current); } catch { }  
                if (dirs != null) foreach (string dir in dirs) pending.Push(dir);  
            }  
        }  
  
        private static string ReadImportFileText(string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    var sharedStrings = new List<string>();
                    ZipArchiveEntry? sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
                    if (sharedEntry != null)
                    {
                        using var sharedStream = sharedEntry.Open();
                        XDocument sharedDoc = XDocument.Load(sharedStream);
                        foreach (XElement item in sharedDoc.Descendants(ns + "si"))
                            sharedStrings.Add(string.Concat(item.Descendants(ns + "t").Select(t => t.Value)));
                    }

                    ZipArchiveEntry? sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                    if (sheetEntry == null)
                        throw new InvalidDataException("The workbook does not contain a readable first worksheet.");

                    using var sheetStream = sheetEntry.Open();
                    XDocument sheetDoc = XDocument.Load(sheetStream);
                    var output = new StringBuilder();
                    foreach (XElement row in sheetDoc.Descendants(ns + "row"))
                    {
                        var cells = new SortedDictionary<int, string>();
                        int maxColumn = -1;
                        foreach (XElement cell in row.Elements(ns + "c"))
                        {
                            string reference = (string?)cell.Attribute("r") ?? string.Empty;
                            int column = GetExcelColumnIndex(reference);
                            if (column < 0) continue;
                            string type = (string?)cell.Attribute("t") ?? string.Empty;
                            string value;
                            if (type == "s")
                            {
                                string rawIndex = cell.Element(ns + "v")?.Value ?? string.Empty;
                                value = int.TryParse(rawIndex, out int index) && index >= 0 && index < sharedStrings.Count
                                    ? sharedStrings[index] : string.Empty;
                            }
                            else if (type == "inlineStr")
                                value = string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
                            else
                                value = cell.Element(ns + "v")?.Value ?? string.Empty;
                            cells[column] = value;
                            if (column > maxColumn) maxColumn = column;
                        }
                        if (maxColumn >= 0)
                        {
                            for (int column = 0; column <= maxColumn; column++)
                            {
                                if (column > 0) output.Append('\t');
                                if (cells.TryGetValue(column, out string? value)) output.Append(value.Replace("\r", " ").Replace("\n", " "));
                            }
                            output.AppendLine();
                        }
                    }
                    return output.ToString();
                }
            }

            if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Legacy .xls files are not supported directly. Save the workbook as .xlsx or copy the cells in Excel and use clipboard import.");

            return File.ReadAllText(path);
        }

        private static int GetExcelColumnIndex(string cellReference)
        {
            int index = 0;
            int letters = 0;
            foreach (char c in cellReference)
            {
                if (c < 'A' || c > 'Z')
                {
                    if (c >= 'a' && c <= 'z') index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
                    else break;
                    letters++;
                }
                else
                {
                    index = index * 26 + (c - 'A' + 1);
                    letters++;
                }
            }
            return letters == 0 ? -1 : index - 1;
        }

        private async void ImportExcelOrClipboardList()  
        {  
            try  
            {  
                string rawInput = string.Empty;  
  
                if (Clipboard.ContainsText())  
                {  
                    string clipText = Clipboard.GetText();  
                    if (!string.IsNullOrWhiteSpace(clipText) && (clipText.Contains("\n") || clipText.Contains("\r") || clipText.EndsWith(".JPG", StringComparison.OrdinalIgnoreCase)))  
                    {  
                        DialogResult dr = MessageBox.Show("Do you want to import file names directly from your Clipboard (copied from Excel)?\n\nClick 'Yes' to use Clipboard, or 'No' to browse for a CSV, text, or .xlsx file.", "Import Source", MessageBoxButtons.YesNo, MessageBoxIcon.Question);  
                        if (dr == DialogResult.Yes) rawInput = clipText;  
                    }  
                }  
  
                if (string.IsNullOrEmpty(rawInput))  
                {  
                    using (OpenFileDialog ofd = new OpenFileDialog() { Title = "Select CSV or List File", Filter = "CSV/Text/Excel Files|*.csv;*.txt;*.xlsx;*.xls|All Files|*.*" })  
                    {  
                        if (ofd.ShowDialog() == DialogResult.OK) rawInput = ReadImportFileText(ofd.FileName);  
                        else return;  
                    }  
                }  
  
                lstFiles.BeginUpdate();  
                lstFiles.Items.Clear();  
                lstFiles.EndUpdate();  
                ResetLogoPreview();  
  
                string printedDir = !string.IsNullOrWhiteSpace(config.PrintedFolder) ? config.PrintedFolder : @"Z:\Printed";  
                var lines = rawInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);  
                var targetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);  
  
                foreach (string line in lines)  
                {  
                    string trimmedLine = line.Trim();  
                    if (string.IsNullOrEmpty(trimmedLine)) continue;  
                    string[] subParts = trimmedLine.Split(new[] { '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);  
                    foreach (string part in subParts)  
                    {  
                        string cleanName = part.Trim('"', ' ', '\t');  
                        if (string.IsNullOrEmpty(cleanName)) continue;  
                        if (cleanName.Equals("File Name", StringComparison.OrdinalIgnoreCase) || cleanName.Equals("Filename", StringComparison.OrdinalIgnoreCase) || cleanName.Equals("FilePath", StringComparison.OrdinalIgnoreCase)) continue;  
                        if (cleanName.IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) >= 0) continue;  
                          
                        targetFiles.Add(Path.GetFileName(cleanName));  
                    }  
                }  
  
                SetStatus("Importing & locating " + targetFiles.Count + " files...");  
                var resolvedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);  
  
                await Task.Run(() =>  
                {  
                    if (Directory.Exists(printedDir) && targetFiles.Count > 0)  
                    {  
                        foreach (string file in FastEnumerateImageFiles(printedDir, CancellationToken.None))  
                        {  
                            string fName = Path.GetFileName(file);  
                            if (targetFiles.Contains(fName))  
                            {  
                                resolvedFiles.Add(file);  
                                targetFiles.Remove(fName);  
                                if (targetFiles.Count == 0) break;  
                            }  
                        }  
                    }  
                });  
  
                lstFiles.BeginUpdate();  
                int count = 0;  
                foreach (string filePath in resolvedFiles.OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase))  
                {  
                    lstFiles.Items.Add(new FileItem { FileName = Path.GetFileName(filePath), FullPath = filePath }, true);  
                    count++;  
                }  
                lstFiles.EndUpdate();  
  
                lblFilesFound.Text = count + " Files Found";  
                SetStatus("Imported " + count + " file(s) successfully.");  
                MessageBox.Show("Successfully resolved and imported " + count + " file reference(s).", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);  
  
                if (lstFiles.Items.Count > 0)  
                {  
                    lstFiles.SelectedIndex = 0;  
                    PreviewSelectedImageSafe();  
                }  
            }  
            catch (Exception ex)  
            {  
                MessageBox.Show("Import Error: " + ex.Message, "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);  
            }  
        }  
  
        // ========================================================================  
        // 4. PRINT DISPATCH  
        // ========================================================================  
        private async void ExecutePrintJobAsync()  
        {  
            try  
            {  
                string machineTargetFolder = config.MachineC;   
                string selectedMachine = cmbMachine.SelectedItem != null ? cmbMachine.SelectedItem.ToString() : "Machine C";  
  
                if (selectedMachine == "Machine A") machineTargetFolder = config.MachineA;  
                else if (selectedMachine == "Machine B") machineTargetFolder = config.MachineB;  
                else if (selectedMachine == "Machine C") machineTargetFolder = config.MachineC;  
                else if (selectedMachine == "Machine D") machineTargetFolder = config.MachineD;  
  
                if (string.IsNullOrWhiteSpace(machineTargetFolder)) throw new InvalidOperationException("The target folder for " + selectedMachine + " is not configured.");  
                Directory.CreateDirectory(machineTargetFolder);  
  
                var checkedItems = lstFiles.CheckedItems.Cast<object>().OfType<FileItem>().ToList();  
                if (checkedItems.Count == 0) { MessageBox.Show("Please check at least one file in the list to print.", "No Files Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }  
  
                string printSize = cmbPrintSizeInput.Text.Trim();  
                if (string.IsNullOrEmpty(printSize)) printSize = "4-Wallets (vert)";  
  
                // -- AUTO 4W FILTER LOGIC --  
                bool isWalletSize = printSize.IndexOf("wallet", StringComparison.OrdinalIgnoreCase) >= 0 ||   
                                    printSize.IndexOf("4w", StringComparison.OrdinalIgnoreCase) >= 0;  
                bool appendAuto4W = chkAuto4W.Checked && !isWalletSize;  
  
                string ticketPrintSize = printSize;  
                bool isCustomTemplate = customTemplates.ContainsKey(printSize);  
                bool isPortHole = printSize.IndexOf("porthole", StringComparison.OrdinalIgnoreCase) >= 0 && printSize.IndexOf("mini", StringComparison.OrdinalIgnoreCase) < 0;  
                bool isMiniPortHole = printSize.IndexOf("mini porthole", StringComparison.OrdinalIgnoreCase) >= 0;  
  
                if (isCustomTemplate) ticketPrintSize = printSize;   
                else if (isPortHole) ticketPrintSize = "8x10 Port Hole";  
                else if (isMiniPortHole) ticketPrintSize = "Mini Porthole 8x10";  
  
                string orderType = cmbOrderType.Text.Trim();  
                if (string.IsNullOrEmpty(orderType)) orderType = "Package";  
  
                string cabinNum = txtPrintCabinNum.Text.Trim();  
                if (string.IsNullOrWhiteSpace(cabinNum)) { MessageBox.Show("Enter a cabin number before printing.", "Missing Cabin Number", MessageBoxButtons.OK, MessageBoxIcon.Warning); txtPrintCabinNum.Focus(); return; }  
                  
                if (isPortHole || isMiniPortHole || isCustomTemplate)  
                {  
                    string cachedBorderPngPath = "";  
                    bool isDual = false;  
  
                    if (isCustomTemplate)  
                    {  
                        cachedBorderPngPath = customTemplates[printSize].Path;  
                        isDual = customTemplates[printSize].IsDual;  
                    }  
                    else if (isMiniPortHole)   
                    {  
                        cachedBorderPngPath = @"Z:\PhotoLab\Borders\Misc\MiniPorthole.png";  
                        isDual = true;  
                    }  
                    else  
                    {  
                        cachedBorderPngPath = @"z:\photolab\poborders\digital borders unzip\misc\PORTHOLE.png";  
                        isDual = false;  
                    }  
  
                    if (!File.Exists(cachedBorderPngPath))  
                    {  
                        MessageBox.Show("Cannot find the assigned border file at:\n" + cachedBorderPngPath + "\n\nPlease locate it.", "Missing Border", MessageBoxButtons.OK, MessageBoxIcon.Warning);  
                        using (OpenFileDialog ofd = new OpenFileDialog() { Title = "Select Border PNG", Filter = "PNG Files|*.png" })  
                        {  
                            if (ofd.ShowDialog() == DialogResult.OK) cachedBorderPngPath = ofd.FileName;  
                            else return;  
                        }  
                    }  
  
                    string tempBorderDir = string.IsNullOrWhiteSpace(config.BorderedPrintsFolder) ? @"Z:\PixelsOrder\Prints" : config.BorderedPrintsFolder;  
                    Directory.CreateDirectory(tempBorderDir);  
  
                    var finalFilesToPrint = new List<FileItem>();  
                    string prefix = isCustomTemplate ? "CUSTOM_" : (isMiniPortHole ? "MINIPORTHOLE_" : "PORTHOLE_");  
  
                    foreach (var item in checkedItems)  
                    {  
                        using (var adjustForm = new BorderAdjustForm(item.FullPath, cachedBorderPngPath, isDual))  
                        {  
                            if (adjustForm.ShowDialog(this) == DialogResult.OK && adjustForm.FlattenedImage != null)  
                            {  
                                string destPath = Path.Combine(tempBorderDir, prefix + Path.GetFileName(item.FullPath));  
                                adjustForm.FlattenedImage.Save(destPath, ImageFormat.Jpeg);  
                                finalFilesToPrint.Add(new FileItem { FileName = Path.GetFileName(destPath), FullPath = destPath });  
                            }  
                            else  
                            {  
                                SetStatus("Print job cancelled during border adjustment.");  
                                return;  
                            }  
                        }  
                    }  
                    checkedItems = finalFilesToPrint;  
                    ticketPrintSize = "8x10";   
                }  
  
                string mediaType = "Glossy";  
                StringBuilder sb = new StringBuilder();  
                sb.AppendLine("OrderReferenceID=");  
                sb.AppendLine("OrderFirstName = " + orderType);  
                sb.AppendLine("OrderLastName = ");  
                sb.AppendLine("ExtCabin = " + cabinNum);  
                sb.AppendLine("ExtFolio = ");  
                sb.AppendLine();  
  
                int qty = (int)numPrintQty.Value;  
                if (qty < 1) qty = 1;  
  
                foreach (var item in checkedItems)  
                {  
                    sb.AppendLine("Qty=" + qty);  
                    sb.AppendLine("Size=" + ticketPrintSize);  
                    sb.AppendLine("PrintName=" + ticketPrintSize);  
                    sb.AppendLine("Media=" + mediaType);  
                    sb.AppendLine("FilePath=" + item.FullPath);  
                    sb.AppendLine();  
                      
                    if (appendAuto4W)  
                    {  
                        sb.AppendLine("Qty=" + qty);  
                        sb.AppendLine("Size=4-Wallets (vert)");  
                        sb.AppendLine("PrintName=4-Wallets (vert)");  
                        sb.AppendLine("Media=" + mediaType);  
                        sb.AppendLine("FilePath=" + item.FullPath);  
                        sb.AppendLine();  
                    }  
                }  
  
                string orderId = GetNext7CharOrderId();  
                string tempFilePath = Path.Combine(machineTargetFolder, orderId + ".tmp");  
                string finalFilePath = Path.Combine(machineTargetFolder, orderId + ".txt");  
  
                Program.SafeWriteAllText(tempFilePath, sb.ToString());  
                File.Move(tempFilePath, finalFilePath);  
  
                string auditSize = isCustomTemplate ? printSize : ticketPrintSize;  
                if (appendAuto4W) auditSize += " + Auto 4W";  
                  
                List<string> fileNamesToLog = checkedItems.Select(x => x.FileName).ToList();  
                LogOrderToAuditHistory(orderId, cabinNum, selectedMachine, auditSize, checkedItems.Count * qty, orderType, fileNamesToLog);  
  
                string statusMsg = "Printed " + checkedItems.Count + " file(s).";  
                string dialogMsg = checkedItems.Count + " files successfully printed.";  
                MessageBox.Show(dialogMsg, "Print Job Created", MessageBoxButtons.OK, MessageBoxIcon.Information);  
                SetStatus(statusMsg);  
            }  
            catch (Exception ex)  
            {  
                MessageBox.Show("Print Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);  
            }  
        }  
  
        // ========================================================================  
        // LIVE NETWORK ORDER ID SCANNER (Prevents duplicate IDs across 3 PCs)  
        // ========================================================================  
        private string GetNext7CharOrderId()
        {
            int maxId = 6000000;
            try
            {
                string ordersDir = !string.IsNullOrWhiteSpace(config.OrdersFolder) ? config.OrdersFolder : @"Z:\PixelsOrder\Processed";
                if (Directory.Exists(ordersDir))
                {
                    // Non-locking, instantaneous scan over active voyage history CSV files
                    foreach (string file in Directory.GetFiles(ordersDir, "*Print History.csv"))
                    {
                        var lines = Program.SafeReadAllLines(file).Skip(1);
                        foreach (var line in lines)
                        {
                            var parts = ParseCsvLine(line);
                            if (parts.Length >= 2)
                            {
                                string idStr = parts[1].Trim('"');
                                if (int.TryParse(idStr, out int val) && val > maxId && val <= 9999999)
                                {
                                    maxId = val;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            int nextNum = maxId + 1;
            if (nextNum > 9999999) nextNum = 6000001; 
            return nextNum.ToString("D7");
        }

        // ========================================================================  
        // 5. PREVIEW & CROP  
        // ========================================================================  
        private void PreviewSelectedImageSafe()  
        {  
            if (lstFiles.SelectedItem is FileItem item && File.Exists(item.FullPath))  
            {  
                try  
                {  
                    byte[] fileBytes = File.ReadAllBytes(item.FullPath);  
                    using (var ms = new MemoryStream(fileBytes))  
                    using (var originalImg = Image.FromStream(ms))  
                    {  
                        Bitmap thumb = new Bitmap(280, 210);  
                        using (Graphics g = Graphics.FromImage(thumb))  
                        {  
                            g.Clear(Color.FromArgb(18, 19, 22));  
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;   
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;  
                            g.SmoothingMode = SmoothingMode.HighQuality;  
  
                            double ratioX = 280.0 / originalImg.Width;  
                            double ratioY = 210.0 / originalImg.Height;  
                            double ratio = Math.Min(ratioX, ratioY);  
  
                            int newWidth = (int)(originalImg.Width * ratio);  
                            int newHeight = (int)(originalImg.Height * ratio);  
                            int posX = (280 - newWidth) / 2;  
                            int posY = (210 - newHeight) / 2;  
  
                            g.DrawImage(originalImg, posX, posY, newWidth, newHeight);  
  
                            string cropSelection = cmbAspectCrop?.SelectedItem?.ToString() ?? "Full Frame";  
                            if (cropSelection != "Full Frame (No Crop)")  
                                DrawAspectCropGuide(g, posX, posY, newWidth, newHeight, cropSelection);  
                        }  
  
                        Image oldImg = picLogo.Image;  
                        picLogo.Image = thumb;  
                        oldImg?.Dispose();  
                    }  
                }  
                catch { ResetLogoPreview(); }  
            }  
        }  
  
        private void DrawAspectCropGuide(Graphics g, int imgX, int imgY, int imgW, int imgH, string cropType)  
        {  
            float targetRatio = 1.0f;   
            bool isCanvasWrap = false;  
  
            if (cropType.StartsWith("8x10")) targetRatio = 8.0f / 10.0f;  
            else if (cropType.StartsWith("4x6")) targetRatio = 4.0f / 6.0f;  
            else if (cropType.StartsWith("5x7")) targetRatio = 5.0f / 7.0f;  
            else if (cropType.StartsWith("16x20")) { targetRatio = 16.0f / 20.0f; isCanvasWrap = true; }  
  
            if (imgW > imgH && targetRatio < 1.0f) targetRatio = 1.0f / targetRatio;  
  
            int cropW = imgW;  
            int cropH = (int)(cropW / targetRatio);  
  
            if (cropH > imgH) { cropH = imgH; cropW = (int)(cropH * targetRatio); }  
  
            int cropX = imgX + (imgW - cropW) / 2;  
            int cropY = imgY + (imgH - cropH) / 2;  
  
            using (Brush maskBrush = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))  
            {  
                g.FillRectangle(maskBrush, imgX, imgY, imgW, cropY - imgY);  
                g.FillRectangle(maskBrush, imgX, cropY + cropH, imgW, (imgY + imgH) - (cropY + cropH));  
                g.FillRectangle(maskBrush, imgX, cropY, cropX - imgX, cropH);  
                g.FillRectangle(maskBrush, cropX + cropW, cropY, (imgX + imgW) - (cropX + cropW), cropH);  
            }  
  
            using (Pen cropPen = new Pen(Color.Gold, 2f) { DashStyle = DashStyle.Dash }) g.DrawRectangle(cropPen, cropX, cropY, cropW, cropH);  
  
            if (isCanvasWrap)  
            {  
                int wrapMarginX = (int)(cropW * 0.08f);  
                int wrapMarginY = (int)(cropH * 0.08f);  
                using (Pen wrapPen = new Pen(Color.Cyan, 1.5f) { DashStyle = DashDotPen() })  
                    g.DrawRectangle(wrapPen, cropX + wrapMarginX, cropY + wrapMarginY, cropW - wrapMarginX * 2, cropH - wrapMarginY * 2);  
            }  
        }  
  
        private static DashStyle DashDotPen() => DashStyle.Dot;  
  
        private void ResetLogoPreview()  
        {  
            Bitmap logoBmp = new Bitmap(280, 210);  
            using (Graphics g = Graphics.FromImage(logoBmp))  
            {  
                g.Clear(Color.FromArgb(18, 19, 22));  
                g.SmoothingMode = SmoothingMode.AntiAlias;  
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;  
  
                using (Font fontPixels = new Font("Segoe UI", 32, FontStyle.Bold))  
                    g.DrawString("pixels", fontPixels, Brushes.White, new PointF(30, 40));  
  
                int barX = 32, barY = 100, segWidth = 30, segHeight = 9;  
                for (int i = 0; i < logoStripeColors.Length; i++)  
                {  
                    using (Brush b = new SolidBrush(logoStripeColors[i]))  
                        g.FillRectangle(b, barX + (i * segWidth), barY, segWidth, segHeight);  
                }  
  
                using (Font fontSub = new Font("Segoe UI", 13.5f, FontStyle.Bold))  
                    g.DrawString("LAB TECHNICIAN", fontSub, Brushes.White, new PointF(32, 118));  
            }  
            Image oldImg = picLogo.Image;  
            picLogo.Image = logoBmp;  
            oldImg?.Dispose();  
        }  
  
        private void SetupFolderWatcher()  
        {  
            try  
            {  
                string downloadsDir = config.DownloadsFolder;  
                Directory.CreateDirectory(downloadsDir);  
                folderWatcher?.Dispose();  
                folderWatcher = new FileSystemWatcher(downloadsDir)  
                {  
                    IncludeSubdirectories = false,  
                    InternalBufferSize = 65536,  
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName  
                };  
                folderWatcher.Created += (s, e) =>  
                {  
                    if (Directory.Exists(e.FullPath) && !IsDisposed && IsHandleCreated)  
                        try { BeginInvoke(new Action(() => HighlightNewOrder(Path.GetFileName(e.FullPath)))); } catch { }  
                };  
                folderWatcher.Error += (s, e) => { if (!IsDisposed && IsHandleCreated) try { BeginInvoke(new Action(() => SetStatus("Folder watcher warning."))); } catch { } };  
                folderWatcher.EnableRaisingEvents = true;  
            }  
            catch (Exception ex) { SetStatus("Folder watcher unavailable: " + ex.Message); }  
        }  
  
        private void SetupPrintshopWatcher()  
        {  
            try  
            {  
                string printshopDir = config.PrintshopFolder;  
                if (!string.IsNullOrWhiteSpace(printshopDir)) Directory.CreateDirectory(printshopDir);  
                printshopWatcher?.Dispose();  
                if (!string.IsNullOrWhiteSpace(printshopDir) && Directory.Exists(printshopDir))  
                {  
                    printshopWatcher = new FileSystemWatcher(printshopDir)  
                    {  
                        IncludeSubdirectories = false,  
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime  
                    };  
  
                    FileSystemEventHandler onChange = (s, e) => { if (!IsDisposed && IsHandleCreated) try { BeginInvoke(new Action(() => UpdateNextCanvasNumber())); } catch { } };  
                    printshopWatcher.Created += onChange;  
                    printshopWatcher.Deleted += onChange;  
                    printshopWatcher.Renamed += (s, e) => { if (!IsDisposed && IsHandleCreated) try { BeginInvoke(new Action(() => UpdateNextCanvasNumber())); } catch { } };  
                    printshopWatcher.EnableRaisingEvents = true;  
                }  
            }  
            catch { }  
        }  
  
        private void HighlightNewOrder(string folderName)  
        {  
            lblLastOrder.Text = "Order Received:" + Environment.NewLine + "Cabin " + folderName;  
            lblLastOrder.ForeColor = Color.FromArgb(120, 240, 150);  
  
            flashCounter = 0;  
            if (flashTimer == null)  
            {  
                flashTimer = new System.Windows.Forms.Timer() { Interval = 800 };   
                flashTimer.Tick += (s, e) => {  
                    flashCounter++;  
                    lblLastOrder.ForeColor = (flashCounter % 2 == 0) ? Color.FromArgb(120, 240, 150) : textWhite;  
                    if (flashCounter >= 12) { lblLastOrder.ForeColor = textWhite; flashTimer.Stop(); }  
                };  
            }  
            flashTimer.Stop();  
            flashTimer.Start();  
        }  
  
        private async void CheckMachineHealthAsync()  
        {  
            string selected = cmbMachine.SelectedItem != null ? cmbMachine.SelectedItem.ToString() : "Machine C";  
            string path = config.MachineC;  
            if (selected == "Machine A") path = config.MachineA;  
            else if (selected == "Machine B") path = config.MachineB;  
            else if (selected == "Machine C") path = config.MachineC;  
            else if (selected == "Machine D") path = config.MachineD;  
  
            lblMachineStatus.Text = "● Checking...";  
            lblMachineStatus.ForeColor = Color.Gold;  
  
            bool isOnline = await Task.Run(() => { try { return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path); } catch { return false; } });  
  
            if (isOnline) { lblMachineStatus.Text = "● Online (Ready)"; lblMachineStatus.ForeColor = Color.FromArgb(100, 240, 120); }  
            else { lblMachineStatus.Text = "● Offline / Unreachable"; lblMachineStatus.ForeColor = Color.FromArgb(240, 80, 80); }  
        }  
  
        private void UpdateNextCanvasNumber()  
        {  
            try  
            {  
                string targetDir = config.PrintshopFolder;  
                if (!Directory.Exists(targetDir)) { lblCanvasHeader.Text = "Next Canvas No. 001"; nextCanvasNumber = 1; return; }  
  
                int highest = 0;  
                var files = Directory.GetFiles(targetDir, "*.jpg", SearchOption.TopDirectoryOnly);  
                foreach (string f in files)  
                {  
                    string name = Path.GetFileNameWithoutExtension(f);  
                    string[] parts = name.Split('_');  
                    if (parts.Length > 0 && int.TryParse(parts[0], out int val))  
                    {  
                        if (val > highest) highest = val;  
                    }  
                }  
                nextCanvasNumber = highest + 1;  
                lblCanvasHeader.Text = "Next Canvas No. " + nextCanvasNumber.ToString("D3");  
            }  
            catch { lblCanvasHeader.Text = "Next Canvas No. 001"; }  
        }  
  
        private async Task PostCanvasOrderAsync()  
        {  
            try  
            {  
                string targetDir = config.PrintshopFolder;  
                Directory.CreateDirectory(targetDir);  
  
                var checkedItems = lstFiles.CheckedItems.Cast<object>().OfType<FileItem>().ToList();  
                if (checkedItems.Count == 0) { MessageBox.Show("Please check at least one file for canvas printing.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }  
  
                string cabin = txtCanvasCabin.Text.Trim();  
                if (string.IsNullOrWhiteSpace(cabin)) { MessageBox.Show("Please enter a cabin number for canvas order.", "Missing Cabin", MessageBoxButtons.OK, MessageBoxIcon.Warning); txtCanvasCabin.Focus(); return; }  
  
                string size = cmbCanvasSize.SelectedItem != null ? cmbCanvasSize.SelectedItem.ToString() : "16x20";  
                string seller = txtSeller.Text.Trim();  
                if (string.IsNullOrEmpty(seller)) seller = "FREE";  
  
                int postedCount = 0;  
                List<string> generatedCanvasNumbers = new List<string>();  
  
                foreach (var item in checkedItems)  
                {  
                    string numPrefix = nextCanvasNumber.ToString("D3");  
                    generatedCanvasNumbers.Add(numPrefix);  
  
                    string baseName = Path.GetFileNameWithoutExtension(item.FileName);  
                    string newFileName = numPrefix + "_" + cabin + "_" + size + "_" + baseName + "_" + seller + ".jpg";  
                    string destinationPath = Path.Combine(targetDir, newFileName);  
  
                    File.Copy(item.FullPath, destinationPath, true);  
                    nextCanvasNumber++;  
                    postedCount++;  
                }  
  
                UpdateNextCanvasNumber();  
                SetStatus("Posted " + postedCount + " canvas order(s) to Printshop.");  
                  
                DialogResult dr = MessageBox.Show(  
                    "Successfully generated " + postedCount + " Canvas Orders in Printshop Folder.\n\nDo you want to print the picture(s) posted as an 8x10 print as well?",   
                    "Canvas Order Created",   
                    MessageBoxButtons.YesNo,   
                    MessageBoxIcon.Question);  
  
                if (dr == DialogResult.Yes)  
                {  
                    string machineTargetFolder = config.MachineC;   
                    string selectedMachine = cmbMachine.SelectedItem != null ? cmbMachine.SelectedItem.ToString() : "Machine C";  
  
                    if (selectedMachine == "Machine A") machineTargetFolder = config.MachineA;  
                    else if (selectedMachine == "Machine B") machineTargetFolder = config.MachineB;  
                    else if (selectedMachine == "Machine C") machineTargetFolder = config.MachineC;  
                    else if (selectedMachine == "Machine D") machineTargetFolder = config.MachineD;  
  
                    if (string.IsNullOrWhiteSpace(machineTargetFolder)) throw new InvalidOperationException("The target folder for " + selectedMachine + " is not configured.");  
                    Directory.CreateDirectory(machineTargetFolder);  
  
                    string canvasNumbersStr = "Canvas NO " + string.Join(", ", generatedCanvasNumbers);  
  
                    StringBuilder sb = new StringBuilder();  
                    sb.AppendLine("OrderReferenceID=");  
                    sb.AppendLine("OrderFirstName = " + canvasNumbersStr);  
                    sb.AppendLine("OrderLastName = " + seller);  
                    sb.AppendLine("ExtCabin = " + cabin);  
                    sb.AppendLine("ExtFolio = ");  
                    sb.AppendLine();  
  
                    foreach (var item in checkedItems)  
                    {  
                        sb.AppendLine("Qty=1");  
                        sb.AppendLine("Size=8x10");  
                        sb.AppendLine("PrintName=8x10");  
                        sb.AppendLine("Media=Glossy");  
                        sb.AppendLine("FilePath=" + item.FullPath);  
                        sb.AppendLine();  
                    }  
  
                    string orderId = GetNext7CharOrderId();  
                    string tempFilePath = Path.Combine(machineTargetFolder, orderId + ".tmp");  
                    string finalFilePath = Path.Combine(machineTargetFolder, orderId + ".txt");  
  
                    Program.SafeWriteAllText(tempFilePath, sb.ToString());  
                    File.Move(tempFilePath, finalFilePath);  
  
                    List<string> fileNamesToLog = checkedItems.Select(x => Path.GetFileName(x.FullPath)).ToList();  
                    LogOrderToAuditHistory(orderId, cabin, selectedMachine, "8x10", checkedItems.Count, seller, fileNamesToLog);  
  
                    SetStatus("Dispatched 8x10 canvas copies to " + selectedMachine + ".");  
                }  
            }  
            catch (Exception ex) { MessageBox.Show("Canvas Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }  
        }  
  
        private async void CopyCheckedFilesToUsb()  
        {  
            string driveLetter = txtUsbDrive.Text.Trim().TrimEnd(':', '\\');  
            if (string.IsNullOrWhiteSpace(driveLetter)) { MessageBox.Show("Please enter a target USB drive letter.", "Missing USB Drive", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }  
  
            string targetDir = driveLetter + @":\";  
            string cabin = txtCabin.Text.Trim();  
            if (!string.IsNullOrEmpty(cabin)) targetDir = Path.Combine(targetDir, cabin);  
  
            await CopyFilesWithProgressAsync(targetDir, "USB Drive");  
        }  
  
        private async void CopyToDownloadsFolder()  
        {  
            string targetDir = config.DownloadsFolder;  
            string cabin = txtCabin.Text.Trim();  
            if (!string.IsNullOrEmpty(cabin)) targetDir = Path.Combine(targetDir, cabin);  
  
            await CopyFilesWithProgressAsync(targetDir, "Downloads Folder");  
        }  
  
        private async Task CopyFilesWithProgressAsync(string destinationDir, string description)  
        {  
            var checkedItems = lstFiles.CheckedItems.Cast<object>().OfType<FileItem>().ToList();  
            if (checkedItems.Count == 0) { MessageBox.Show("Please check at least one file to copy.", "No Files Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }  
  
            copyCts?.Cancel(); copyCts = new CancellationTokenSource(); var token = copyCts.Token;  
            progressBar.Visible = true; progressBar.Style = ProgressBarStyle.Continuous; progressBar.Value = 0; progressBar.Maximum = checkedItems.Count;  
            SetStatus("Copying files to " + description + "...");  
  
            try  
            {  
                Directory.CreateDirectory(destinationDir);  
                int copied = 0;

                await Task.Run(() =>
                {
                    for (int i = 0; i < checkedItems.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var item = checkedItems[i];
                        string destFile = Path.Combine(destinationDir, item.FileName);

                        if (!File.Exists(destFile) || !AreFilesIdentical(item.FullPath, destFile))
                            File.Copy(item.FullPath, destFile, true);

                        copied++;
                        if ((i % 10 == 0) || i == checkedItems.Count - 1)
                        {
                            int progress = i + 1;
                            if (!IsDisposed && IsHandleCreated)
                                BeginInvoke(new Action(() => progressBar.Value = Math.Min(progressBar.Maximum, progress)));
                        }
                    }
                }, token);

                SetStatus("Copied " + copied + "/" + checkedItems.Count + " files to " + description + ".");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Copy cancelled. Some files may already have been copied; completed files were kept.");
            }
            catch (Exception ex) { MessageBox.Show("Copy failed: " + ex.Message, "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error); SetStatus("Copy failed: " + ex.Message); }
            finally { progressBar.Visible = false; }  
        }  
  
        private static bool AreFilesIdentical(string file1, string file2)
        {
            try
            {
                var fi1 = new FileInfo(file1);
                var fi2 = new FileInfo(file2);
                if (fi1.Length != fi2.Length) return false;
                using (var fs1 = File.OpenRead(file1))
                using (var fs2 = File.OpenRead(file2))
                {
                    byte[] b1 = new byte[64 * 1024];
                    byte[] b2 = new byte[64 * 1024];
                    while (true)
                    {
                        int r1 = fs1.Read(b1, 0, b1.Length);
                        int r2 = fs2.Read(b2, 0, b2.Length);
                        if (r1 != r2) return false;
                        if (r1 == 0) return true;
                        for (int i = 0; i < r1; i++)
                            if (b1[i] != b2[i]) return false;
                    }
                }
            }
            catch { return false; }
        }

        private void SetStatus(string message) { if (!IsDisposed && IsHandleCreated) try { Invoke(new Action(() => { lblStatusLeft.Text = message; lblStatusRight.Text = message; })); } catch { } }  
  
        // ========================================================================  
        // 6. SETTINGS DIALOG  
        // ========================================================================  
        private void OpenSettingsDialog()  
        {  
            Form settingsForm = new Form()  
            {  
                Text = AppTitle + " - Settings & HotFolder Paths",  
                Size = new Size(690, 660),  
                StartPosition = FormStartPosition.CenterParent,  
                BackColor = darkBackground,  
                FormBorderStyle = FormBorderStyle.FixedDialog,  
                MaximizeBox = false, MinimizeBox = false,  
                Icon = appIcon  
            };  
  
            int y = 20;  
            AddSettingRow(settingsForm, "Printed Folder:", config.PrintedFolder, ref y, (val) => config.PrintedFolder = val);  
            AddSettingRow(settingsForm, "Orders Folder:", config.OrdersFolder, ref y, (val) => config.OrdersFolder = val);  
            AddSettingRow(settingsForm, "Downloads Folder:", config.DownloadsFolder, ref y, (val) => config.DownloadsFolder = val);  
            AddSettingRow(settingsForm, "Bordered Prints:", string.IsNullOrWhiteSpace(config.BorderedPrintsFolder) ? @"Z:\PixelsOrder\Prints" : config.BorderedPrintsFolder, ref y, (val) => config.BorderedPrintsFolder = val);  
            AddSettingRow(settingsForm, "Printshop Folder:", config.PrintshopFolder, ref y, (val) => config.PrintshopFolder = val);  
            AddSettingRow(settingsForm, "CC Folder:", config.CCFolder, ref y, (val) => config.CCFolder = val);  
            AddSettingRow(settingsForm, "White Images:", config.DestinationForWhiteImages, ref y, (val) => config.DestinationForWhiteImages = val);  
            AddSettingRow(settingsForm, "Machine A:", config.MachineA, ref y, (val) => config.MachineA = val);  
            AddSettingRow(settingsForm, "Machine B:", config.MachineB, ref y, (val) => config.MachineB = val);  
            AddSettingRow(settingsForm, "Machine C:", config.MachineC, ref y, (val) => config.MachineC = val);  
            AddSettingRow(settingsForm, "Machine D:", config.MachineD, ref y, (val) => config.MachineD = val);  
  
            Button btnMediaSizes = new Button() { Text = "Manage Media Sizes", Location = new Point(20, y + 15), Width = 175, Height = 36, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            btnMediaSizes.Click += (s, e) => {
                using (var smf = new MediaSizeManagerForm()) {
                    smf.ShowDialog(settingsForm);
                    LoadMediaSizes();
                    LoadCustomTemplates();
                }
            };

            Button btnTemplates = new Button() { Text = "Manage Custom Borders", Location = new Point(205, y + 15), Width = 195, Height = 36, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };  
            btnTemplates.Click += (s, e) => {  
                using (var tmf = new TemplateManagerForm()) {  
                    tmf.ShowDialog(settingsForm);  
                    LoadCustomTemplates();  
                }  
            };  
  
            Button btnSave = new Button() { Text = "Save Settings", Location = new Point(420, y + 15), Width = 215, Height = 36, BackColor = accentTeal, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };  
            btnSave.Click += (s, e) =>  
            {  
                config.Save(); SetupPrintshopWatcher(); UpdateNextCanvasNumber(); CheckMachineHealthAsync(); CheckLatestDownloadOrder();  
                MessageBox.Show("Settings and HotFolder paths saved successfully!", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);  
                settingsForm.Close();  
            };  
              
            settingsForm.Controls.Add(btnMediaSizes);
            settingsForm.Controls.Add(btnTemplates);  
            settingsForm.Controls.Add(btnSave);   
            settingsForm.ShowDialog();  
        }  
  
        private void AddSettingRow(Form parent, string labelName, string initialValue, ref int yPos, Action<string> onValueChanged)  
        {  
            Label lbl = new Label() { Text = labelName, Location = new Point(20, yPos + 3), Width = 140, Height = 20, ForeColor = textWhite };  
            TextBox txt = new TextBox() { Text = initialValue, Location = new Point(170, yPos), Width = 380, Height = 25 };  
            Button btnBrowse = new Button() { Text = "Browse", Location = new Point(560, yPos - 1), Width = 75, Height = 27, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat };  
  
            btnBrowse.Click += (s, e) => { using (FolderBrowserDialog fbd = new FolderBrowserDialog()) { if (fbd.ShowDialog() == DialogResult.OK) { txt.Text = fbd.SelectedPath; onValueChanged(fbd.SelectedPath); } } };  
            txt.TextChanged += (s, e) => { onValueChanged(txt.Text); };  
  
            parent.Controls.Add(lbl); parent.Controls.Add(txt); parent.Controls.Add(btnBrowse); yPos += 35;  
        }  
  
        // ========================================================================  
        // 7. ORDER AUDIT HISTORY (NETWORK SYNC & AUTO LOCKED EXCEL GENERATOR)  
        // ========================================================================  
        private void LogOrderToAuditHistory(string orderId, string cabin, string machine, string size, int count, string type, List<string> fileNames)  
        {  
            try  
            {  
                string ordersDir = config.OrdersFolder;  
                if (!string.IsNullOrWhiteSpace(ordersDir))  
                {  
                    Directory.CreateDirectory(ordersDir);  
                    string voyageName = GetNetworkVoyage();  
                      
                    // 1. Log to the internal background CSV  
                    string csvPath = Path.Combine(ordersDir, voyageName + " Print History.csv");  
                    if (!File.Exists(csvPath))   
                    {  
                        Program.SafeWriteAllText(csvPath, "\"Date & Time\",\"Order ID\",\"Cabin Number\",\"Assigned Printer\",\"Print Size\",\"Quantity\",\"Order Category\",\"File Name(s)\"" + Environment.NewLine);  
                    }  
  
                    string filesStr = string.Join(" | ", fileNames);
                    string line = string.Join(",", new[]
                    {
                        EscapeCsvField(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                        EscapeCsvField(orderId), EscapeCsvField(cabin), EscapeCsvField(machine),
                        EscapeCsvField(size), EscapeCsvField(count.ToString()), EscapeCsvField(type), EscapeCsvField(filesStr)
                    }) + Environment.NewLine;
                    Program.SafeAppendAllText(csvPath, line);  
  
                    // 2. Automatically generate the Professional, Locked Excel Report  
                    string xlsPath = Path.Combine(ordersDir, voyageName + " Print Report.xls");  
                      
                    if (File.Exists(xlsPath)) {  
                        File.SetAttributes(xlsPath, File.GetAttributes(xlsPath) & ~FileAttributes.ReadOnly);  
                    }  
  
                    StringBuilder html = new StringBuilder();  
                    html.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns=\"http://www.w3.org/TR/REC-html40\">");  
                    html.AppendLine("<head><meta charset='utf-8'>");  
                    html.AppendLine("<!--[if gte mso 9]><xml><x:ExcelWorkbook><x:ExcelWorksheets><x:ExcelWorksheet><x:Name>Print Report</x:Name><x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions></x:ExcelWorksheet></x:ExcelWorksheets></x:ExcelWorkbook></xml><![endif]-->");  
                    html.AppendLine("<style>");  
                    html.AppendLine("table { border-collapse: collapse; font-family: 'Segoe UI', Arial, sans-serif; font-size: 10pt; mso-table-lspace: 0pt; mso-table-rspace: 0pt; }");  
                    html.AppendLine("th { background-color: #1F4E78; color: #FFFFFF; font-weight: bold; border: 1px solid #000000; padding: 6px 12px; text-align: center; vertical-align: middle; white-space: nowrap; }");  
                    html.AppendLine("td { border: 1px solid #000000; padding: 5px 10px; vertical-align: middle; text-align: center; white-space: nowrap; mso-number-format: '\\@'; }");  
                    html.AppendLine(".files { text-align: left; vertical-align: top; white-space: nowrap; }");  
                    html.AppendLine("</style></head><body>");  
                    html.AppendLine("<h2 style=\"font-family: 'Segoe UI', Arial, sans-serif; color: #1F4E78;\">Voyage " + HtmlEncode(voyageName) + " - Laboratory Print Report</h2>");  
                    html.AppendLine("<table border=\"1\" cellpadding=\"5\" cellspacing=\"0\" style=\"border-collapse: collapse; border: 1px solid #000000;\">");  
                    html.AppendLine("<tr>");  
                    html.AppendLine("<th style=\"background-color: #1F4E78; color: #ffffff; border: 1px solid #000000;\">Date &amp; Time</th>");  
                    html.AppendLine("<th style=\"background-color: #1F4E78; color: #ffffff; border: 1px solid #000000;\">Order ID</th>");  
                    html.AppendLine("<th style=\"background-color: #1F4E78; color: #ffffff; border: 1px solid #000000;\">Cabin Number</th>");  
                    html.AppendLine("<th style=\"background-color: #1F4E78; color: #ffffff; border: 1px solid #000000;\">Machine</th>");  
                    html.AppendLine("<th style=\"background-color: #1F4E78; color: #ffffff; border: 1px solid #000000;\">Print Size</th>");  
                    html.AppendLine("<th style=\"background-color: #1F4E78; color: #ffffff; border: 1px solid #000000;\">Quantity</th>");  
                    html.AppendLine("<th style=\"background-color: #1F4E78; color: #ffffff; border: 1px solid #000000;\">File Name(s)</th>");  
                    html.AppendLine("</tr>");  
  
                    var lines = Program.SafeReadAllLines(csvPath).Skip(1);  
                    int rowIndex = 0;  
                    foreach (var l in lines) {  
                        var parts = ParseCsvLine(l);   
                        if (parts.Length >= 6) {  
                            string bgStyle = (rowIndex % 2 == 1) ? " background-color: #F2F5F8;" : "";  
                            html.AppendLine("<tr>");  
                            html.AppendLine("<td style=\"border: 1px solid #000000;" + bgStyle + "\">" + HtmlEncode(parts.Length > 0 ? parts[0] : "") + "</td>");  
                            html.AppendLine("<td style=\"border: 1px solid #000000;" + bgStyle + "\">" + HtmlEncode(parts.Length > 1 ? parts[1] : "") + "</td>");  
                            html.AppendLine("<td style=\"border: 1px solid #000000;" + bgStyle + "\">" + HtmlEncode(parts.Length > 2 ? parts[2] : "") + "</td>");  
                            html.AppendLine("<td style=\"border: 1px solid #000000;" + bgStyle + "\">" + HtmlEncode(parts.Length > 3 ? parts[3] : "") + "</td>");  
                            html.AppendLine("<td style=\"border: 1px solid #000000;" + bgStyle + "\">" + HtmlEncode(parts.Length > 4 ? parts[4] : "") + "</td>");  
                            html.AppendLine("<td style=\"border: 1px solid #000000;" + bgStyle + "\">" + HtmlEncode(parts.Length > 5 ? parts[5] : "") + "</td>");  
                            string rawFiles = parts.Length >= 8 ? parts[7] : (parts.Length == 7 ? parts[6] : "");  
                            string formattedFiles = string.Join("<br>", rawFiles.Split(new[] { " | " }, StringSplitOptions.None).Select(HtmlEncode));  
                            html.AppendLine("<td class=\"files\" style=\"border: 1px solid #000000; text-align: left;" + bgStyle + "\">" + formattedFiles + "</td>");  
                            html.AppendLine("</tr>");  
                            rowIndex++;  
                        }  
                    }  
                    html.AppendLine("</table></body></html>");  
                    Program.SafeWriteAllText(xlsPath, html.ToString());  
                      
                    File.SetAttributes(xlsPath, File.GetAttributes(xlsPath) | FileAttributes.ReadOnly);  
                }  
            }
            catch (Exception ex)
            {
                SetStatus("WARNING: order dispatched, but audit history/report update failed: " + ex.Message);
            }
        }

        private void OpenOrderHistoryDialog()  
        {  
            Form historyForm = new Form() { Text = AppTitle + " - Voyage Order History", Size = new Size(680, 520), StartPosition = FormStartPosition.CenterParent, BackColor = darkBackground, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Icon = appIcon };  
              
            Label lblVoyage = new Label() { Text = "Current Voyage Code:", Location = new Point(20, 20), AutoSize = true, ForeColor = textWhite, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            TextBox txtVoyage = new TextBox() { Text = GetNetworkVoyage(), Location = new Point(160, 17), Width = 150 };  
            Button btnSetVoyage = new Button() { Text = "Save Voyage", Location = new Point(320, 16), Width = 100, Height = 27, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat };  
  
            ListView lv = new ListView() { Location = new Point(20, 55), Width = 625, Height = 360, View = View.Details, FullRowSelect = true, BackColor = Color.FromArgb(50, 50, 50), ForeColor = textWhite };  
            lv.Columns.Add("Date & Time", 140); lv.Columns.Add("Order ID", 90); lv.Columns.Add("Cabin No.", 75); lv.Columns.Add("Machine", 90); lv.Columns.Add("Print Size", 120); lv.Columns.Add("Qty", 50);  
  
            Action loadHistory = () => {  
                lv.Items.Clear();  
                try {  
                    string voyageName = GetNetworkVoyage();  
                    string historyPath = Path.Combine(!string.IsNullOrWhiteSpace(config.OrdersFolder) ? config.OrdersFolder : @"Z:\PixelsOrder\Processed", voyageName + " Print History.csv");  
                    if (File.Exists(historyPath)) {  
                        var lines = Program.SafeReadAllLines(historyPath).Skip(1);  
                        foreach (var line in lines.Reverse()) {  
                            var parts = ParseCsvLine(line);  
                            if (parts.Length >= 6) {   
                                var lvi = new ListViewItem(parts[0].Trim('"'));   
                                lvi.SubItems.Add(parts[1].Trim('"'));   
                                lvi.SubItems.Add(parts[2].Trim('"'));   
                                lvi.SubItems.Add(parts[3].Trim('"'));   
                                lvi.SubItems.Add(parts[4].Trim('"'));   
                                lvi.SubItems.Add(parts[5].Trim('"'));   
                                lv.Items.Add(lvi);   
                            }  
                        }  
                    }  
                } catch { }  
            };  
  
            btnSetVoyage.Click += (s, e) => {  
                string v = txtVoyage.Text.Trim();  
                if(!string.IsNullOrEmpty(v)) {  
                    SetNetworkVoyage(v);  
                    loadHistory();  
                    MessageBox.Show("Voyage set to " + v + ". Network synced successfully.", "Voyage Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);  
                }  
            };  
  
            loadHistory();  
  
            Button btnLoadPhotos = new Button() { Text = "Load / Select Photos", Location = new Point(20, 430), Width = 160, Height = 32, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat };  
            btnLoadPhotos.Click += (s, e) => {  
                if (lv.SelectedItems.Count > 0) {  
                    var item = lv.SelectedItems[0];  
                    string orderId = item.SubItems[1].Text;  
                    string cabin = item.SubItems[2].Text;  
                    string machine = item.SubItems[3].Text;  
                    string size = item.SubItems[4].Text;  
                    string qtyStr = item.SubItems[5].Text;  
  
                    txtCabin.Text = cabin;  
                    if (cmbMachine.Items.Contains(machine)) cmbMachine.SelectedItem = machine;  
                      
                    string cleanSize = size.Replace(" + Auto 4W", "").Replace(" (Canvas Copy)", "").Replace(" (canvas copy)", "");  
                    if (!cmbPrintSizeInput.Items.Contains(cleanSize)) cmbPrintSizeInput.Items.Add(cleanSize);  
                    cmbPrintSizeInput.SelectedItem = cleanSize;  
                      
                    if (int.TryParse(qtyStr, out int q)) numPrintQty.Value = Math.Max(numPrintQty.Minimum, Math.Min(q, numPrintQty.Maximum));  
  
                    string voyageName = GetNetworkVoyage();  
                    string historyPath = Path.Combine(!string.IsNullOrWhiteSpace(config.OrdersFolder) ? config.OrdersFolder : @"Z:\PixelsOrder\Processed", voyageName + " Print History.csv");  
                    List<string> targetFiles = new List<string>();  
                      
                    if (File.Exists(historyPath)) {  
                        var lines = Program.SafeReadAllLines(historyPath).Skip(1);  
                        foreach (var line in lines) {  
                            var parts = ParseCsvLine(line);  
                            if (parts.Length >= 8 && parts[1].Trim('"') == orderId) {  
                                targetFiles = parts[7].Trim('"').Split(new[] { " | " }, StringSplitOptions.None).ToList();  
                                break;  
                            }  
                        }  
                    }  
  
                    if (targetFiles.Count > 0) {  
                        string searchStr = string.Join(",", targetFiles.Select(f => f.Trim()));  
                        txtSearch.Text = searchStr;  
                        historyForm.Close();  
                        SearchFilesAsync(searchStr);  
                    } else {  
                        MessageBox.Show("Could not find the original file names for this order in the database.", "Files Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);  
                    }  
  
                } else {  
                    MessageBox.Show("Please select an order from the list first.", "No Order Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);  
                }  
            };  
  
            Button btnInstantReprint = new Button() { Text = "Instant Reprint Order", Location = new Point(190, 430), Width = 160, Height = 32, BackColor = accentTeal, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            btnInstantReprint.Click += (s, e) => {  
                if (lv.SelectedItems.Count > 0) {  
                    if (MessageBox.Show("Are you sure you want to instantly reprint this entire order directly to the machine?", "Confirm Instant Reprint", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {  
                        var item = lv.SelectedItems[0];  
                        string orderId = item.SubItems[1].Text;  
                        string cabin = item.SubItems[2].Text;  
                        string machine = item.SubItems[3].Text;  
                        string size = item.SubItems[4].Text;  
                        string qtyStr = item.SubItems[5].Text;  
                          
                        int qty = int.TryParse(qtyStr, out int q) ? q : 1;  
                          
                        string voyageName = GetNetworkVoyage();  
                        string historyPath = Path.Combine(!string.IsNullOrWhiteSpace(config.OrdersFolder) ? config.OrdersFolder : @"Z:\PixelsOrder\Processed", voyageName + " Print History.csv");  
                        List<string> targetFiles = new List<string>();  
                        string orderType = "Package";  
                          
                        if (File.Exists(historyPath)) {  
                            var lines = Program.SafeReadAllLines(historyPath).Skip(1);  
                            foreach (var line in lines) {  
                                var parts = ParseCsvLine(line);  
                                if (parts.Length >= 8 && parts[1].Trim('"') == orderId) {  
                                    orderType = parts[6].Trim('"');  
                                    targetFiles = parts[7].Trim('"').Split(new[] { " | " }, StringSplitOptions.None).ToList();  
                                    break;  
                                }  
                            }  
                        }  
                          
                        if (targetFiles.Count == 0) {  
                            MessageBox.Show("Could not find hidden file list for this order in the history CSV.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);  
                            return;  
                        }  
                          
                        List<string> foundPaths = new List<string>();  
                        string printedDir = !string.IsNullOrWhiteSpace(config.PrintedFolder) ? config.PrintedFolder : @"Z:\Printed";  
                          
                        foreach(var f in targetFiles) {  
                            if (string.IsNullOrWhiteSpace(f)) continue;  
                            string possiblePath = Path.Combine(printedDir, f.Trim());  
                            if (File.Exists(possiblePath)) {  
                                foundPaths.Add(possiblePath);  
                            } else {  
                                try {  
                                    var match = Directory.EnumerateFiles(printedDir, f.Trim(), SearchOption.AllDirectories).FirstOrDefault();  
                                    if (match != null) foundPaths.Add(match);  
                                } catch { }  
                            }  
                        }  
                          
                        if(foundPaths.Count == 0) {  
                             MessageBox.Show("Could not physically locate the original files in the Printed folder.", "Files Missing", MessageBoxButtons.OK, MessageBoxIcon.Error);  
                             return;  
                        }  
                          
                        string machineTargetFolder = config.MachineC;  
                        if (machine == "Machine A") machineTargetFolder = config.MachineA;  
                        else if (machine == "Machine B") machineTargetFolder = config.MachineB;  
                        else if (machine == "Machine D") machineTargetFolder = config.MachineD;  
                          
                        Directory.CreateDirectory(machineTargetFolder);  
                          
                        StringBuilder sb = new StringBuilder();  
                        sb.AppendLine("OrderReferenceID=");  
                        sb.AppendLine("OrderFirstName = " + orderType);  
                        sb.AppendLine("OrderLastName = REPRINT");  
                        sb.AppendLine("ExtCabin = " + cabin);  
                        sb.AppendLine("ExtFolio = ");  
                        sb.AppendLine();  
                          
                        string cleanSize = size.Replace(" + Auto 4W", "").Replace(" (Canvas Copy)", "").Replace(" (canvas copy)", "");  
                        bool appendAuto4W = size.Contains("+ Auto 4W");  
  
                        foreach (var path in foundPaths) {  
                            sb.AppendLine("Qty=" + qty);  
                            sb.AppendLine("Size=" + cleanSize);  
                            sb.AppendLine("PrintName=" + cleanSize);  
                            sb.AppendLine("Media=Glossy");  
                            sb.AppendLine("FilePath=" + path);  
                            sb.AppendLine();  
                              
                            if (appendAuto4W) {  
                                sb.AppendLine("Qty=" + qty);  
                                sb.AppendLine("Size=4-Wallets (vert)");  
                                sb.AppendLine("PrintName=4-Wallets (vert)");  
                                sb.AppendLine("Media=Glossy");  
                                sb.AppendLine("FilePath=" + path);  
                                sb.AppendLine();  
                            }  
                        }  
                          
                        string newOrderId = GetNext7CharOrderId();  
                        string tempFilePath = Path.Combine(machineTargetFolder, newOrderId + ".tmp");  
                        string finalFilePath = Path.Combine(machineTargetFolder, newOrderId + ".txt");  
                        Program.SafeWriteAllText(tempFilePath, sb.ToString());  
                        File.Move(tempFilePath, finalFilePath);  
                          
                        LogOrderToAuditHistory(newOrderId, cabin, machine, size, foundPaths.Count * qty, "REPRINT", targetFiles);  
                          
                        MessageBox.Show("Instant reprint successfully dispatched as Order " + newOrderId, "Reprint Sent", MessageBoxButtons.OK, MessageBoxIcon.Information);  
                        loadHistory();  
                    }  
                } else {  
                     MessageBox.Show("Please select an order from the list first.", "No Order Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);  
                }  
            };  
  
            Button btnClearHistory = new Button() { Text = "Clear", Location = new Point(360, 430), Width = 110, Height = 32, BackColor = Color.FromArgb(180, 50, 50), ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            btnClearHistory.Click += (s, e) => {  
                if (MessageBox.Show("Are you sure you want to completely clear the order history for the current voyage? This cannot be undone.", "Confirm Clear", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)  
                {  
                    string voyageName = GetNetworkVoyage();  
                    string csvPath = Path.Combine(!string.IsNullOrWhiteSpace(config.OrdersFolder) ? config.OrdersFolder : @"Z:\PixelsOrder\Processed", voyageName + " Print History.csv");  
                    string xlsPath = Path.Combine(!string.IsNullOrWhiteSpace(config.OrdersFolder) ? config.OrdersFolder : @"Z:\PixelsOrder\Processed", voyageName + " Print Report.xls");  
  
                    try   
                    {  
                        Program.SafeWriteAllText(csvPath, "\"Date & Time\",\"Order ID\",\"Cabin Number\",\"Assigned Printer\",\"Print Size\",\"Quantity\",\"Order Category\",\"File Name(s)\"" + Environment.NewLine);  
                        if (File.Exists(xlsPath)) { File.SetAttributes(xlsPath, File.GetAttributes(xlsPath) & ~FileAttributes.ReadOnly); File.Delete(xlsPath); }  
                        lv.Items.Clear();  
                    }   
                    catch (Exception ex) { MessageBox.Show("Could not clear history: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }  
                }  
            };  
  
            Button btnClose = new Button() { Text = "Close", Location = new Point(480, 430), Width = 110, Height = 32, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat };  
            btnClose.Click += (s, e) => historyForm.Close();  
  
            historyForm.Controls.Add(lblVoyage);  
            historyForm.Controls.Add(txtVoyage);  
            historyForm.Controls.Add(btnSetVoyage);  
            historyForm.Controls.Add(lv);   
            historyForm.Controls.Add(btnLoadPhotos);  
            historyForm.Controls.Add(btnInstantReprint);  
            historyForm.Controls.Add(btnClearHistory);  
            historyForm.Controls.Add(btnClose);   
            historyForm.ShowDialog();  
        }  
  
        protected override void OnFormClosed(FormClosedEventArgs e)  
        {  
            searchCts?.Cancel(); copyCts?.Cancel(); folderWatcher?.Dispose(); printshopWatcher?.Dispose(); flashTimer?.Dispose(); picLogo?.Image?.Dispose(); base.OnFormClosed(e);  
        }  
  
        private class FileItem { public string FileName { get; set; } = ""; public string FullPath { get; set; } = ""; public override string ToString() => FileName; }  
        private class OrderSearchResult { public bool IsOrder { get; set; } public string OrderId { get; set; } = ""; public string Cabin { get; set; } = ""; public string? Size { get; set; } public List<string> Files { get; set; } = new List<string>(); }  
    }  
  
    // ========================================================================  
    // MEDIA SIZE MANAGER FORM  
    // ========================================================================  
    public class MediaSizeManagerForm : Form
    {
        private ListBox lstSizes;
        private TextBox txtNewSize;

        public MediaSizeManagerForm()
        {
            this.Text = "Manage Media Print Sizes";
            this.Size = new Size(460, 430);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(43, 43, 43);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lblInfo = new Label() { Text = "Add or remove available print sizes from the Print Size menu:", Location = new Point(20, 15), AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 9) };

            lstSizes = new ListBox() { Location = new Point(20, 42), Width = 405, Height = 210, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, Font = new Font("Segoe UI", 9.5f) };
            RefreshList();

            Label lblNew = new Label() { Text = "New Media Size Name (e.g. 10x13 or Metallic 8x10):", Location = new Point(20, 262), AutoSize = true, ForeColor = Color.White };
            txtNewSize = new TextBox() { Location = new Point(20, 285), Width = 285, Height = 25, Font = new Font("Segoe UI", 9) };
            
            Button btnAdd = new Button() { Text = "Add Size", Location = new Point(315, 283), Width = 110, Height = 28, BackColor = Color.FromArgb(17, 122, 114), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnAdd.Click += (s, e) =>
            {
                string newSize = txtNewSize.Text.Trim();
                if (string.IsNullOrWhiteSpace(newSize)) return;
                var current = GetCurrentSizes();
                if (!current.Contains(newSize, StringComparer.OrdinalIgnoreCase))
                {
                    current.Add(newSize);
                    SaveSizes(current);
                    txtNewSize.Clear();
                    RefreshList();
                }
            };

            Button btnRemove = new Button() { Text = "Remove Selected", Location = new Point(20, 330), Width = 150, Height = 34, BackColor = Color.FromArgb(180, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
            btnRemove.Click += (s, e) =>
            {
                if (lstSizes.SelectedItem is string sel)
                {
                    var current = GetCurrentSizes();
                    current.RemoveAll(x => x.Equals(sel, StringComparison.OrdinalIgnoreCase));
                    SaveSizes(current);
                    RefreshList();
                }
            };

            Button btnReset = new Button() { Text = "Reset Defaults", Location = new Point(180, 330), Width = 130, Height = 34, BackColor = Color.FromArgb(54, 54, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
            btnReset.Click += (s, e) =>
            {
                if (MessageBox.Show("Reset media sizes to factory defaults?", "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    SaveSizes(new List<string>(MainForm.DefaultMediaSizes));
                    RefreshList();
                }
            };

            Button btnClose = new Button() { Text = "Done", Location = new Point(320, 330), Width = 105, Height = 34, BackColor = Color.FromArgb(28, 115, 189), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(lblInfo);
            this.Controls.Add(lstSizes);
            this.Controls.Add(lblNew);
            this.Controls.Add(txtNewSize);
            this.Controls.Add(btnAdd);
            this.Controls.Add(btnRemove);
            this.Controls.Add(btnReset);
            this.Controls.Add(btnClose);
        }

        private List<string> GetCurrentSizes()
        {
            var list = new List<string>();
            if (File.Exists("MediaSizes.txt"))
            {
                foreach (var line in Program.SafeReadAllLines("MediaSizes.txt"))
                {
                    string s = line.Trim();
                    if (!string.IsNullOrEmpty(s) && !list.Contains(s, StringComparer.OrdinalIgnoreCase))
                        list.Add(s);
                }
            }
            if (list.Count == 0) list.AddRange(MainForm.DefaultMediaSizes);
            return list;
        }

        private void SaveSizes(List<string> sizes)
        {
            Program.SafeWriteAllText("MediaSizes.txt", string.Join(Environment.NewLine, sizes));
        }

        private void RefreshList()
        {
            lstSizes.Items.Clear();
            foreach (var size in GetCurrentSizes())
            {
                lstSizes.Items.Add(size);
            }
        }
    }

    // ========================================================================  
    // CUSTOM TEMPLATE MANAGER FORM  
    // ========================================================================  
    public class TemplateManagerForm : Form  
    {  
        private ListBox lstTemplates;  
        private TextBox txtName;  
        private TextBox txtPath;  
        private CheckBox chkDual;  
  
        public TemplateManagerForm()  
        {  
            this.Text = "Custom Template Manager";  
            this.Size = new Size(500, 420);  
            this.StartPosition = FormStartPosition.CenterParent;  
            this.BackColor = Color.FromArgb(43, 43, 43);  
            this.FormBorderStyle = FormBorderStyle.FixedDialog;  
            this.MaximizeBox = false;  
  
            Label lblInfo = new Label() { Text = "Add custom border PNGs to inject them into the Print Size menu.", Location = new Point(20, 15), AutoSize = true, ForeColor = Color.White };  
  
            lstTemplates = new ListBox() { Location = new Point(20, 40), Width = 445, Height = 130, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };  
            RefreshList();  
  
            Label lblName = new Label() { Text = "Template Name (e.g. 8x10 Fall Border):", Location = new Point(20, 185), AutoSize = true, ForeColor = Color.White };  
            txtName = new TextBox() { Location = new Point(20, 205), Width = 445, Height = 25 };  
  
            Label lblPath = new Label() { Text = "PNG Border File Path:", Location = new Point(20, 240), AutoSize = true, ForeColor = Color.White };  
            txtPath = new TextBox() { Location = new Point(20, 260), Width = 360, Height = 25 };  
            Button btnBrowse = new Button() { Text = "Browse", Location = new Point(390, 259), Width = 75, Height = 27, BackColor = Color.FromArgb(54, 54, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };  
            btnBrowse.Click += (s, e) => { using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "PNG Image|*.png" }) { if (ofd.ShowDialog() == DialogResult.OK) txtPath.Text = ofd.FileName; } };  
  
            chkDual = new CheckBox() { Text = "This is a 2-Photo Split Border (like Mini Porthole)", Location = new Point(20, 295), Width = 350, ForeColor = Color.White };  
  
            Button btnAdd = new Button() { Text = "Add / Update Template", Location = new Point(20, 330), Width = 170, Height = 35, BackColor = Color.FromArgb(17, 122, 114), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            btnAdd.Click += BtnAdd_Click;  
  
            Button btnRemove = new Button() { Text = "Remove Selected", Location = new Point(200, 330), Width = 140, Height = 35, BackColor = Color.FromArgb(180, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };  
            btnRemove.Click += BtnRemove_Click;  
  
            Button btnClose = new Button() { Text = "Close", Location = new Point(375, 330), Width = 90, Height = 35, BackColor = Color.FromArgb(54, 54, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };  
            btnClose.Click += (s, e) => this.Close();  
  
            this.Controls.Add(lblInfo); this.Controls.Add(lstTemplates);  
            this.Controls.Add(lblName); this.Controls.Add(txtName);  
            this.Controls.Add(lblPath); this.Controls.Add(txtPath); this.Controls.Add(btnBrowse);  
            this.Controls.Add(chkDual); this.Controls.Add(btnAdd); this.Controls.Add(btnRemove); this.Controls.Add(btnClose);  
        }  
  
        private void RefreshList()  
        {  
            lstTemplates.Items.Clear();  
            if (File.Exists("CustomTemplates.txt"))  
            {  
                foreach (var line in Program.SafeReadAllLines("CustomTemplates.txt"))  
                {  
                    if (line.Contains("|")) lstTemplates.Items.Add(line.Split('|')[0].Trim());  
                }  
            }  
        }  
  
        private void BtnAdd_Click(object? sender, EventArgs e)  
        {  
            string name = txtName.Text.Trim();  
            string path = txtPath.Text.Trim();  
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) { MessageBox.Show("Name and Path are required."); return; }  
  
            var lines = File.Exists("CustomTemplates.txt") ? Program.SafeReadAllLines("CustomTemplates.txt").ToList() : new List<string>();  
            lines.RemoveAll(l => l.StartsWith(name + "|", StringComparison.OrdinalIgnoreCase));  
            lines.Add($"{name}|{path}|{chkDual.Checked}");  
  
            Program.SafeWriteAllText("CustomTemplates.txt", string.Join(Environment.NewLine, lines));  
            txtName.Clear(); txtPath.Clear(); chkDual.Checked = false;  
            RefreshList();  
        }  
  
        private void BtnRemove_Click(object? sender, EventArgs e)  
        {  
            if (lstTemplates.SelectedItem == null) return;  
            string name = lstTemplates.SelectedItem.ToString() ?? "";  
              
            var lines = Program.SafeReadAllLines("CustomTemplates.txt").ToList();  
            lines.RemoveAll(l => l.StartsWith(name + "|", StringComparison.OrdinalIgnoreCase));  
            Program.SafeWriteAllText("CustomTemplates.txt", string.Join(Environment.NewLine, lines));  
            RefreshList();  
        }  
    }  
  
    // ========================================================================  
    // BORDER ADJUSTMENT TOOL (Optimized with Dynamic Fast Rendering)  
    // ========================================================================  
    public class BorderAdjustForm : Form  
    {  
        public Image? FlattenedImage { get; private set; }  
        private Image originalPhoto;  
        private Image borderLayer;  
        private MemoryStream photoStream;  
        private MemoryStream borderStream;  
          
        // Fast Render Engine Variables  
        private System.Windows.Forms.Timer renderTimer;  
        private bool isFastRender = false;  
          
        private class PhotoState {  
            public float Zoom = 1.0f;  
            public PointF Pan = new PointF(0, 0);  
            public RectangleF ClipBounds;  
        }  
  
        private List<PhotoState> photos = new List<PhotoState>();  
        private int activeIndex = 0;  
        private bool isDualPhoto = false;  
  
        private Point lastMousePos;  
        private bool isDragging = false;  
        private PictureBox canvas;  
        private TrackBar tbZoom;  
  
        public BorderAdjustForm(string photoPath, string borderPath, bool dualPhoto)  
        {  
            this.Text = "Adjust Photo Border - " + Path.GetFileName(photoPath);  
            this.Size = new Size(900, 750);  
            this.StartPosition = FormStartPosition.CenterParent;  
            this.BackColor = Color.FromArgb(43, 43, 43);  
            this.isDualPhoto = dualPhoto;  
  
            byte[] photoBytes = File.ReadAllBytes(photoPath);  
            byte[] borderBytes = File.ReadAllBytes(borderPath);  
            photoStream = new MemoryStream(photoBytes);  
            borderStream = new MemoryStream(borderBytes);  
              
            originalPhoto = Image.FromStream(photoStream);  
            borderLayer = Image.FromStream(borderStream);  
  
            if (isDualPhoto)  
            {  
                if (borderLayer.Width > borderLayer.Height)   
                {  
                    photos.Add(new PhotoState { ClipBounds = new RectangleF(0, 0, borderLayer.Width / 2f, borderLayer.Height) });  
                    photos.Add(new PhotoState { ClipBounds = new RectangleF(borderLayer.Width / 2f, 0, borderLayer.Width / 2f, borderLayer.Height) });  
                }  
                else   
                {  
                    photos.Add(new PhotoState { ClipBounds = new RectangleF(0, 0, borderLayer.Width, borderLayer.Height / 2f) });  
                    photos.Add(new PhotoState { ClipBounds = new RectangleF(0, borderLayer.Height / 2f, borderLayer.Width, borderLayer.Height / 2f) });  
                }  
            }  
            else  
            {  
                photos.Add(new PhotoState { ClipBounds = new RectangleF(0, 0, borderLayer.Width, borderLayer.Height) });  
            }  
  
            foreach (var p in photos)   
            {  
                float rX = p.ClipBounds.Width / originalPhoto.Width;  
                float rY = p.ClipBounds.Height / originalPhoto.Height;  
                p.Zoom = Math.Max(rX, rY);   
            }  
  
            Label lblInst = new Label() { Text = isDualPhoto ? "Click a side to select. Scroll Wheel to Zoom. Drag to Move." : "Scroll Mouse Wheel to Zoom. Click and Drag to Move.", ForeColor = Color.White, Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };  
              
            Button btnApply = new Button() { Text = "Apply && Print", Location = new Point(620, 10), Width = 140, Height = 35, BackColor = Color.FromArgb(17, 122, 114), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };  
            btnApply.Click += BtnApply_Click;  
  
            Button btnCancel = new Button() { Text = "Cancel", Location = new Point(770, 10), Width = 90, Height = 35, BackColor = Color.FromArgb(180, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };  
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };  
  
            // Setup Fast Render Engine Timer  
            renderTimer = new System.Windows.Forms.Timer { Interval = 150 };  
            renderTimer.Tick += (s, e) => { renderTimer.Stop(); isFastRender = false; canvas.Invalidate(); };  
  
            tbZoom = new TrackBar() { Location = new Point(360, 15), Width = 240, Minimum = 10, Maximum = 300, Value = (int)(photos[activeIndex].Zoom * 100), TickStyle = TickStyle.None };  
            tbZoom.ValueChanged += (s, e) =>   
            {   
                photos[activeIndex].Zoom = tbZoom.Value / 100f;   
                isFastRender = true;   
                renderTimer.Stop();   
                renderTimer.Start();   
                canvas.Invalidate();   
            };  
  
            canvas = new PictureBox() { Location = new Point(20, 55), Width = 840, Height = 640, BackColor = Color.FromArgb(20, 20, 20), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.SizeAll };  
            canvas.Paint += Canvas_Paint;  
              
            canvas.MouseDown += (s, e) =>   
            {   
                isDragging = true;   
                isFastRender = true;  
                renderTimer.Stop();  
                lastMousePos = e.Location;   
                  
                float uiScale = Math.Min((float)canvas.Width / borderLayer.Width, (float)canvas.Height / borderLayer.Height);  
                int drawW = (int)(borderLayer.Width * uiScale);  
                int drawH = (int)(borderLayer.Height * uiScale);  
                int offsetX = (canvas.Width - drawW) / 2;  
                int offsetY = (canvas.Height - drawH) / 2;  
  
                float clickX = (e.X - offsetX) / uiScale;  
                float clickY = (e.Y - offsetY) / uiScale;  
  
                for (int i = 0; i < photos.Count; i++)   
                {  
                    if (photos[i].ClipBounds.Contains(clickX, clickY))   
                    {  
                        activeIndex = i;  
                        tbZoom.Value = Math.Min(Math.Max((int)(photos[i].Zoom * 100), tbZoom.Minimum), tbZoom.Maximum);  
                        break;  
                    }  
                }  
                canvas.Invalidate();  
            };  
              
            canvas.MouseUp += (s, e) =>   
            {   
                isDragging = false;   
                renderTimer.Start();   
            };  
              
            canvas.MouseMove += (s, e) =>  
            {  
                if (isDragging)  
                {  
                    photos[activeIndex].Pan.X += (e.X - lastMousePos.X);  
                    photos[activeIndex].Pan.Y += (e.Y - lastMousePos.Y);  
                    lastMousePos = e.Location;  
                    canvas.Invalidate();  
                }  
            };  
              
            canvas.MouseWheel += (s, e) =>   
            {  
                int newZoom = (int)(photos[activeIndex].Zoom * 100) + (e.Delta > 0 ? 10 : -10);  
                if (newZoom >= tbZoom.Minimum && newZoom <= tbZoom.Maximum)  
                {  
                    tbZoom.Value = newZoom;   
                }  
                isFastRender = true;  
                renderTimer.Stop();  
                renderTimer.Start();  
            };  
  
            this.Controls.Add(lblInst);  
            this.Controls.Add(tbZoom);  
            this.Controls.Add(btnApply);  
            this.Controls.Add(btnCancel);  
            this.Controls.Add(canvas);  
        }  
  
        private void Canvas_Paint(object? sender, PaintEventArgs e)  
        {  
            e.Graphics.Clear(Color.Black);  
  
            // Dynamic Fast Rendering Engine  
            if (isFastRender || isDragging)  
            {  
                e.Graphics.InterpolationMode = InterpolationMode.Bilinear;  
                e.Graphics.SmoothingMode = SmoothingMode.HighSpeed;  
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;  
                e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;  
            }  
            else  
            {  
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;  
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;  
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;  
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;  
            }  
  
            float uiScale = Math.Min((float)canvas.Width / borderLayer.Width, (float)canvas.Height / borderLayer.Height);  
            int drawW = (int)(borderLayer.Width * uiScale);  
            int drawH = (int)(borderLayer.Height * uiScale);  
            int offsetX = (canvas.Width - drawW) / 2;  
            int offsetY = (canvas.Height - drawH) / 2;  
  
            e.Graphics.TranslateTransform(offsetX, offsetY);  
            e.Graphics.ScaleTransform(uiScale, uiScale);  
  
            for (int i = 0; i < photos.Count; i++)  
            {  
                var p = photos[i];  
                e.Graphics.SetClip(p.ClipBounds);  
                  
                float photoW = originalPhoto.Width * p.Zoom;  
                float photoH = originalPhoto.Height * p.Zoom;  
                float photoX = p.ClipBounds.X + (p.ClipBounds.Width - photoW) / 2 + (p.Pan.X / uiScale);  
                float photoY = p.ClipBounds.Y + (p.ClipBounds.Height - photoH) / 2 + (p.Pan.Y / uiScale);  
                  
                e.Graphics.DrawImage(originalPhoto, photoX, photoY, photoW, photoH);  
            }  
  
            e.Graphics.ResetClip();  
            e.Graphics.DrawImage(borderLayer, 0, 0, borderLayer.Width, borderLayer.Height);  
        }  
  
        private void BtnApply_Click(object? sender, EventArgs e)  
        {  
            Bitmap finalBmp = new Bitmap(borderLayer.Width, borderLayer.Height);  
            using (Graphics g = Graphics.FromImage(finalBmp))  
            {  
                g.Clear(Color.White);  
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;  
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;  
                g.SmoothingMode = SmoothingMode.HighQuality;  
  
                float uiScale = Math.Min((float)canvas.Width / borderLayer.Width, (float)canvas.Height / borderLayer.Height);  
  
                for (int i = 0; i < photos.Count; i++)  
                {  
                    var p = photos[i];  
                    g.SetClip(p.ClipBounds);  
                      
                    float photoW = originalPhoto.Width * p.Zoom;  
                    float photoH = originalPhoto.Height * p.Zoom;  
                    float photoX = p.ClipBounds.X + (p.ClipBounds.Width - photoW) / 2 + (p.Pan.X / uiScale);  
                    float photoY = p.ClipBounds.Y + (p.ClipBounds.Height - photoH) / 2 + (p.Pan.Y / uiScale);  
                      
                    g.DrawImage(originalPhoto, photoX, photoY, photoW, photoH);  
                }  
  
                g.ResetClip();  
                g.DrawImage(borderLayer, 0, 0, borderLayer.Width, borderLayer.Height);  
            }  
              
            FlattenedImage = finalBmp;  
            this.DialogResult = DialogResult.OK;  
            this.Close();  
        }  
  
        protected override void OnFormClosed(FormClosedEventArgs e)  
        {  
            renderTimer?.Dispose();  
            originalPhoto?.Dispose();  
            borderLayer?.Dispose();  
            photoStream?.Dispose();  
            borderStream?.Dispose();  
            base.OnFormClosed(e);  
        }  
    }  

    // ========================================================================  
    // ADOBE-INSPIRED TWO-STAGE SPLASH SCREEN ANIMATOR  
    // ========================================================================  
    public class SplashForm : Form
    {
        private Image? stage1Image;
        private Image? stage2Image;
        private System.Windows.Forms.Timer animTimer;
        private int currentStage = 1;
        private double currentOpacity = 0.0;
        private int frameCounter = 0;
        private bool isFadingIn = true;

        public SplashForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(520, 330);
            this.BackColor = Color.FromArgb(18, 19, 22);
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.Opacity = 0.0;

            try
            {
                string path1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "splash_stage1.jpg");
                string path2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "splash_stage2.jpg");

                if (File.Exists(path1)) stage1Image = Image.FromFile(path1);
                if (File.Exists(path2)) stage2Image = Image.FromFile(path2);
            }
            catch { }

            animTimer = new System.Windows.Forms.Timer { Interval = 15 };
            animTimer.Tick += AnimTimer_Tick;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            animTimer.Start();
        }

        private void AnimTimer_Tick(object? sender, EventArgs e)
        {
            if (isFadingIn)
            {
                currentOpacity += 0.08;
                if (currentOpacity >= 1.0)
                {
                    currentOpacity = 1.0;
                    frameCounter++;
                    if (frameCounter > 50)
                    {
                        if (currentStage == 1 && stage2Image != null)
                        {
                            currentStage = 2;
                            frameCounter = 0;
                        }
                        else if (frameCounter > 65)
                        {
                            isFadingIn = false;
                            frameCounter = 0;
                        }
                    }
                }
            }
            else
            {
                currentOpacity -= 0.1;
                if (currentOpacity <= 0.0)
                {
                    currentOpacity = 0.0;
                    animTimer.Stop();
                    this.Close();
                }
            }

            this.Opacity = currentOpacity;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(18, 19, 22));
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Image? currentImg = (currentStage == 1) ? stage1Image : stage2Image;
            if (currentImg != null)
            {
                e.Graphics.DrawImage(currentImg, 0, 0, this.Width, this.Height);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            animTimer?.Dispose();
            stage1Image?.Dispose();
            stage2Image?.Dispose();
            base.OnFormClosed(e);
        }
    }
    
    // ========================================================================  
    // ADOBE-INSPIRED TWO-STAGE SPLASH SCREEN ANIMATOR (BULLETPROOF FALLBACK)  
    // ========================================================================  
    public class SplashForm : Form
    {
        private Image? stage1Image;
        private Image? stage2Image;
        private System.Windows.Forms.Timer animTimer;
        private int currentStage = 1;
        private double currentOpacity = 0.0;
        private int frameCounter = 0;
        private bool isFadingIn = true;

        private readonly Color[] logoStripeColors = new Color[]  
        {  
            Color.FromArgb(128, 130, 133), Color.FromArgb(0, 188, 212),  
            Color.FromArgb(0, 114, 206), Color.FromArgb(0, 47, 108),  
            Color.FromArgb(67, 176, 42), Color.FromArgb(243, 146, 0),  
            Color.FromArgb(227, 27, 35)  
        };  

        public SplashForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(520, 330);
            this.BackColor = Color.FromArgb(18, 19, 22);
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.Opacity = 0.0;

            try
            {
                string path1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "splash_stage1.jpg");
                string path2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "splash_stage2.jpg");

                if (File.Exists(path1)) stage1Image = Image.FromFile(path1);
                if (File.Exists(path2)) stage2Image = Image.FromFile(path2);
            }
            catch { }

            animTimer = new System.Windows.Forms.Timer { Interval = 15 };
            animTimer.Tick += AnimTimer_Tick;

            this.Click += (s, e) => CloseSplash();
            this.KeyDown += (s, e) => CloseSplash();
        }

        private void CloseSplash()
        {
            animTimer?.Stop();
            this.Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            animTimer.Start();
        }

        private void AnimTimer_Tick(object? sender, EventArgs e)
        {
            if (isFadingIn)
            {
                currentOpacity += 0.05; 
                if (currentOpacity >= 1.0)
                {
                    currentOpacity = 1.0;
                    frameCounter++;
                    
                    if (frameCounter > 80)
                    {
                        if (currentStage == 1)
                        {
                            currentStage = 2;
                            frameCounter = 0; 
                        }
                        else if (frameCounter > 80)
                        {
                            isFadingIn = false;
                            frameCounter = 0;
                        }
                    }
                }
            }
            else
            {
                currentOpacity -= 0.05; 
                if (currentOpacity <= 0.0)
                {
                    currentOpacity = 0.0;
                    CloseSplash();
                }
            }

            this.Opacity = currentOpacity;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(18, 19, 22));
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Image? currentImg = (currentStage == 1) ? stage1Image : stage2Image;
            if (currentImg != null)
            {
                e.Graphics.DrawImage(currentImg, 0, 0, this.Width, this.Height);
            }
            else
            {
                // -- FALLBACK GDI+ RENDERING IF JPG FILES ARE NOT FOUND --
                if (currentStage == 1)
                {
                    using (Font fTitle = new Font("Segoe UI", 24, FontStyle.Bold))
                    using (Font fSub = new Font("Segoe UI", 10, FontStyle.Bold))
                    using (Font fCredit = new Font("Segoe UI", 9))
                    using (Font fCreditSub = new Font("Segoe UI", 8))
                    {
                        e.Graphics.DrawString("pixels", fTitle, Brushes.White, new PointF(190, 70));
                        int barX = 170, barY = 115, segWidth = 26, segHeight = 8;
                        for (int i = 0; i < logoStripeColors.Length; i++)
                        {
                            using (Brush b = new SolidBrush(logoStripeColors[i]))
                                e.Graphics.FillRectangle(b, barX + (i * segWidth), barY, segWidth, segHeight);
                        }
                        e.Graphics.DrawString("LAB TECHNICIAN", fSub, Brushes.White, new PointF(185, 130));
                        e.Graphics.DrawString("Created by Gil", fCredit, Brushes.LightGray, new PointF(210, 185));
                        e.Graphics.DrawString("Inspired by Hassan / MTP", fCreditSub, Brushes.Gray, new PointF(180, 205));
                        e.Graphics.DrawString("LOADING WORKFLOW MODULES...", fCreditSub, Brushes.Gray, new PointF(30, 290));
                    }
                }
                else
                {
                    using (Font fTitle = new Font("Segoe UI", 28, FontStyle.Bold))
                    using (Font fSub = new Font("Segoe UI", 11, FontStyle.Bold))
                    using (Font fCredit = new Font("Segoe UI", 9))
                    using (Font fCreditSub = new Font("Segoe UI", 8))
                    {
                        e.Graphics.DrawString("Lt", fTitle, Brushes.White, new PointF(230, 75));
                        e.Graphics.DrawString("Lab Tools", fSub, Brushes.White, new PointF(215, 125));
                        e.Graphics.DrawString("LABTECH PRINTING SYSTEM", new Font("Segoe UI", 7.5f), Brushes.Gray, new PointF(180, 145));
                        e.Graphics.DrawString("Created by Gil", fCredit, Brushes.LightGray, new PointF(210, 185));
                        e.Graphics.DrawString("Inspired by Hassan / MTP", fCreditSub, Brushes.Gray, new PointF(180, 205));
                        e.Graphics.DrawString("INITIALIZING INTERFACE...", fCreditSub, Brushes.Gray, new PointF(30, 290));
                    }
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            animTimer?.Dispose();
            stage1Image?.Dispose();
            stage2Image?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
