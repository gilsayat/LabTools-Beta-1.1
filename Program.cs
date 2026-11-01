using System;
using System.IO;
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
using System.Text.Json;

// ========================================================================
// labtools - Laboratory Workflow Automation System
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
            Application.Run(new MainForm());
        }
    }

    public class AppConfig
    {
        public string PrintedFolder { get; set; } = @"Z:\Printed";
        public string OrdersFolder { get; set; } = @"Z:\PixelsOrder\Processed";
        public string DownloadsFolder { get; set; } = @"Z:\PixelsOrder\Downloads";
        public string PrintshopFolder { get; set; } = @"Z:\PhotoLab\Printshop";
        public string CCFolder { get; set; } = @"Z:\CC";
        public string DestinationForWhiteImages { get; set; } = @"Z:\PhotoLab\Misc";
        public string MachineA { get; set; } = @"Z:\HotFolders\MachineA";
        public string MachineB { get; set; } = @"Z:\HotFolders\MachineB";
        public string MachineC { get; set; } = @"Z:\HotFolders\MachineC";
        public string MachineD { get; set; } = @"Z:\HotFolders\MachineD";
        public bool IsDarkMode { get; set; } = true;

        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "labtools_config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                    if (loaded != null) return loaded;
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
                File.WriteAllText(ConfigFilePath, json);
            }
            catch { }
        }
    }

    public class MainForm : Form
    {
#pragma warning disable CS8618
        public const string Publisher = "Gil Sayat";
        public const string AppTitle = "labtools";

        // Palette
        private Color darkBackground = Color.FromArgb(43, 43, 43);
        private Color panelBackground = Color.FromArgb(54, 54, 54);
        private readonly Color accentBlue = Color.FromArgb(28, 115, 189);
        private readonly Color accentTeal = Color.FromArgb(17, 122, 114);
        private Color textWhite = Color.White;
        private bool isDarkMode = true;

        private readonly Color[] logoStripeColors = new Color[]
        {
            Color.FromArgb(128, 130, 133), Color.FromArgb(0, 188, 212),
            Color.FromArgb(0, 114, 206), Color.FromArgb(0, 47, 108),
            Color.FromArgb(67, 176, 42), Color.FromArgb(243, 146, 0),
            Color.FromArgb(227, 27, 35)
        };

        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" };

        private AppConfig config = null!;
        private Panel topBar = null!;
        private Panel rightPanel = null!;
        private Panel alertBox = null!;
        private Button btnThemeSwitch = null!;
        private Button btnHistory = null!;
        private Button btnSettings = null!;
        private Button btnClearAll = null!;
        private Button btnExcelList = null!;
        private Button btnSelectAll = null!;
        private Button btnUnselectAll = null!;
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
        private ComboBox cmbPrintSizeInput = null!;
        private CheckBox chkAuto4W = null!;
        private ComboBox cmbOrderType = null!; 
        private Label lblStatusLeft = null!;
        private Label lblStatusRight = null!;
        private Label lblLastOrder = null!;        
        private Label lblFilesFound = null!;
        private Label lblCredit = null!;
        private ProgressBar progressBar = null!;
        private ComboBox cmbAspectCrop = null!;

        private FileSystemWatcher? folderWatcher;
        private FileSystemWatcher? printshopWatcher;
        private System.Windows.Forms.Timer? flashTimer;
        private int flashCounter = 0;
        private bool isSyncingCabin = false;
        private int nextCanvasNumber = 1;
        private int currentOrderSequence = 6000000;
        private CancellationTokenSource? searchCts;
        private CancellationTokenSource? copyCts;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);
        private static Icon? appIcon = null;

        public MainForm()
        {
            config = AppConfig.Load();
            isDarkMode = config.IsDarkMode;
            InitializeCustomComponent();
            ApplyTheme();
            SetupFolderWatcher();
            SetupPrintshopWatcher();
            UpdateNextCanvasNumber();
            CheckMachineHealthAsync();
        }

        private void GenerateAndSetAppIcon()
        {
            if (appIcon == null)
            {
                using (Bitmap bmp = new Bitmap(128, 128))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.FromArgb(18, 19, 22)); // Match dark background
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                        using (Font fontPixels = new Font("Segoe UI", 28, FontStyle.Bold))
                        {
                            SizeF textSize = g.MeasureString("pixels", fontPixels);
                            g.DrawString("pixels", fontPixels, Brushes.White, new PointF((128 - textSize.Width) / 2, 25));
                        }

                        int segWidth = 16, segHeight = 8;
                        int totalWidth = logoStripeColors.Length * segWidth;
                        int barX = (128 - totalWidth) / 2;
                        int barY = 75;

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

                // Generates an .ico file in the directory that you can use in GitHub for the .exe icon
                try
                {
                    if (!File.Exists("labtools_icon.ico") && appIcon != null)
                    {
                        using (var fs = new FileStream("labtools_icon.ico", FileMode.Create))
                        {
                            appIcon.Save(fs);
                        }
                    }
                }
                catch { }
            }
            this.Icon = appIcon;
        }

        private void InitializeCustomComponent()
        {
            this.Text = "labtools";
            this.Size = new Size(930, 850);
            this.MinimumSize = new Size(860, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = darkBackground;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;

            // Generate and Set Application Logo/Icon
            GenerateAndSetAppIcon();

            topBar = new Panel() { Location = new Point(0, 0), Width = 930, Height = 46, BackColor = Color.FromArgb(22, 23, 27), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            // Compact icon-only theme switch (no redundant "Labtools" label on the top bar)
            btnThemeSwitch = new Button()
            {
                Text = isDarkMode ? "🌙" : "☀️",
                Location = new Point(15, 9),
                Width = 34,
                Height = 28,
                BackColor = panelBackground,
                ForeColor = textWhite,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Emoji", 10f),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnThemeSwitch.FlatAppearance.BorderSize = 0;
            btnThemeSwitch.Click += (s, e) => { ToggleTheme(); };

            btnHistory = new Button() { Text = "Order History / Audit", Location = new Point(610, 8), Width = 150, Height = 30, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnHistory.Click += (s, e) => { OpenOrderHistoryDialog(); };
            btnSettings = new Button() { Text = "Settings / Folders", Location = new Point(770, 8), Width = 140, Height = 30, BackColor = accentTeal, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnSettings.Click += (s, e) => { OpenSettingsDialog(); };
            
            topBar.Controls.Add(btnThemeSwitch);
            topBar.Controls.Add(btnHistory);
            topBar.Controls.Add(btnSettings);
            this.Controls.Add(topBar);

            txtSearch = new TextBox() { Text = "", Location = new Point(20, 58), Width = 290, Height = 30, Font = new Font("Segoe UI", 10), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SearchFilesAsync(txtSearch.Text); e.Handled = true; e.SuppressKeyPress = true; } };
            
            btnFind = new Button() { Text = "Find", Location = new Point(320, 56), Width = 85, Height = 32, BackColor = accentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnFind.Click += (s, e) => { SearchFilesAsync(txtSearch.Text); };

            btnCancelSearch = new Button() { Text = "Cancel", Location = new Point(410, 56), Width = 75, Height = 32, BackColor = Color.FromArgb(180, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnCancelSearch.Click += (s, e) => { searchCts?.Cancel(); btnCancelSearch.Enabled = false; SetStatus("Search cancelled by operator."); };

            btnClearAll = new Button() { Text = "Clear All", Location = new Point(490, 56), Width = 90, Height = 32, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f) };
            btnClearAll.Click += (s, e) => { txtSearch.Clear(); lstFiles.Items.Clear(); ResetLogoPreview(); lblFilesFound.Text = "0 Files Found"; SetStatus("Cleared all search results."); };

            Button btnOtherSource = new Button() { Text = "Select From Other Source (JPG)", Location = new Point(20, 98), Width = 230, Height = 35, BackColor = accentTeal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnOtherSource.Click += (s, e) => { SelectFilesFromOtherSource(); };

            btnExcelList = new Button() { Text = "Import From CSV List / Clipboard", Location = new Point(260, 98), Width = 235, Height = 35, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnExcelList.Click += (s, e) => { ImportExcelOrClipboardList(); };

            lstFiles = new CheckedListBox() { Location = new Point(20, 142), Width = 340, Height = 145, BackColor = Color.FromArgb(70, 70, 70), ForeColor = textWhite };
            lstFiles.SelectedIndexChanged += (s, e) => { PreviewSelectedImageSafe(); };
            
            btnSelectAll = new Button() { Text = "Select All", Location = new Point(370, 142), Width = 210, Height = 32, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat };
            btnSelectAll.Click += (s, e) => { lstFiles.BeginUpdate(); for (int i = 0; i < lstFiles.Items.Count; i++) lstFiles.SetItemChecked(i, true); lstFiles.EndUpdate(); };

            btnUnselectAll = new Button() { Text = "Unselect All", Location = new Point(370, 180), Width = 210, Height = 32, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat };
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

            Button btnCopyUsb = new Button() { Text = "Copy To USB", Location = new Point(290, 345), Width = 135, Height = 52, BackColor = accentTeal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnCopyUsb.Click += (s, e) => { CopyCheckedFilesToUsb(); };

            Button btnCopyDownloads = new Button() { Text = "Copy To Downloads", Location = new Point(435, 345), Width = 135, Height = 52, BackColor = accentTeal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnCopyDownloads.Click += (s, e) => { CopyToDownloadsFolder(); };

            lblCanvasHeader = new Label() { Text = "Next Canvas No. 001", Location = new Point(20, 417), ForeColor = Color.FromArgb(130, 220, 210), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            txtCanvasCabin = new TextBox() { Text = "", Location = new Point(245, 414), Width = 115, Height = 25 }; 
            txtSeller = new TextBox() { Text = "FREE", Location = new Point(245, 444), Width = 115, Height = 25 };
            cmbCanvasSize = new ComboBox() { Location = new Point(20, 444), Width = 215, Height = 25, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCanvasSize.Items.AddRange(new string[] { "16x20", "20x30", "11x14", "8x10" });
            cmbCanvasSize.SelectedIndex = 0;

            Button btnPostCanvas = new Button() { Text = "Post Canvas Order", Location = new Point(370, 414), Width = 200, Height = 55, BackColor = accentTeal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnPostCanvas.Click += (s, e) => { PostCanvasOrder(); };

            Label lblPrintCabinLabel = new Label() { Text = "Cabin", Location = new Point(20, 490), ForeColor = textWhite, AutoSize = true };
            txtPrintCabinNum = new TextBox() { Text = "", Location = new Point(130, 487), Width = 100, Height = 25, TextAlign = HorizontalAlignment.Center };
            cmbPrintSizeInput = new ComboBox() { Location = new Point(245, 487), Width = 325, Height = 25, DropDownStyle = ComboBoxStyle.DropDown };
            cmbPrintSizeInput.Items.AddRange(new string[] { "8x10", "4x6", "5x7", "4x8", "4-Wallets (vert)", "Wallet Combo (4W)", "8x10 and 4W on (10in)", "5x7 and 4-Wallet Combo (8x10)" });
            cmbPrintSizeInput.Text = "4-Wallets (vert)";

            txtCabin.TextChanged += (s, e) => { if (isSyncingCabin) return; isSyncingCabin = true; txtCanvasCabin.Text = txtCabin.Text; txtPrintCabinNum.Text = txtCabin.Text; isSyncingCabin = false; };
            txtCanvasCabin.TextChanged += (s, e) => { if (isSyncingCabin) return; isSyncingCabin = true; txtCabin.Text = txtCanvasCabin.Text; txtPrintCabinNum.Text = txtCanvasCabin.Text; isSyncingCabin = false; };
            txtPrintCabinNum.TextChanged += (s, e) => { if (isSyncingCabin) return; isSyncingCabin = true; txtCabin.Text = txtPrintCabinNum.Text; txtCanvasCabin.Text = txtPrintCabinNum.Text; isSyncingCabin = false; };

            chkAuto4W = new CheckBox() { Text = "Auto 4W", Location = new Point(30, 527), Width = 120, Height = 25, ForeColor = textWhite };
            
            cmbOrderType = new ComboBox() { Location = new Point(20, 557), Width = 270, Height = 25, DropDownStyle = ComboBoxStyle.DropDown };
            cmbOrderType.Items.AddRange(new string[] { "Package", "Manual Order", "Express Order", "Online Order" });
            cmbOrderType.Text = "Package";

            Button btnPrint = new Button() { Text = "Print", Location = new Point(300, 527), Width = 270, Height = 55, BackColor = accentTeal, ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnPrint.Click += (s, e) => { ExecutePrintJobAsync(); };

            lblStatusLeft = new Label() { Text = "Ready", Location = new Point(20, 765), Width = 560, Height = 30, ForeColor = textWhite, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11, FontStyle.Bold), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };

            rightPanel = new Panel() { Location = new Point(600, 55), Width = 310, Height = 745, BackColor = panelBackground, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            picLogo = new PictureBox() { Location = new Point(15, 15), Width = 280, Height = 210, BackColor = Color.FromArgb(18, 19, 22), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            ResetLogoPreview();

            Label lblCrop = new Label() { Text = "Aspect Ratio Crop Guide:", Location = new Point(15, 235), ForeColor = textWhite, AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            cmbAspectCrop = new ComboBox() { Location = new Point(15, 255), Width = 280, Height = 25, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAspectCrop.Items.AddRange(new string[] { "Full Frame (No Crop)", "8x10 Ratio (4:5)", "4x6 Ratio (2:3)", "5x7 Ratio (5:7)", "16x20 Canvas (+1.5in Wrap)" });
            cmbAspectCrop.SelectedIndex = 0;
            cmbAspectCrop.SelectedIndexChanged += (s, e) => { PreviewSelectedImageSafe(); };

            alertBox = new Panel() { Location = new Point(15, 310), Width = 280, Height = 95, BackColor = Color.FromArgb(30, 31, 35), BorderStyle = BorderStyle.FixedSingle };
            Label lblOrderHeader = new Label() { Text = "DIGITAL ORDER MONITOR", Location = new Point(5, 8), Width = 268, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            lblLastOrder = new Label() { Text = "Waiting for orders...", Location = new Point(5, 35), Width = 268, Height = 45, ForeColor = textWhite, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            alertBox.Controls.Add(lblOrderHeader);
            alertBox.Controls.Add(lblLastOrder);

            lblStatusRight = new Label() { Text = "Ready", Location = new Point(15, 695), Width = 280, Height = 30, ForeColor = textWhite, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11, FontStyle.Bold), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };

            lblCredit = new Label() { Text = "Created by Gil Inspired by Hassan", Location = new Point(15, 725), Width = 280, Height = 16, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 7f, FontStyle.Regular), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };

            rightPanel.Controls.Add(picLogo);
            rightPanel.Controls.Add(lblCrop);
            rightPanel.Controls.Add(cmbAspectCrop);
            rightPanel.Controls.Add(alertBox);
            rightPanel.Controls.Add(lblStatusRight);
            rightPanel.Controls.Add(lblCredit);

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
            this.Controls.Add(cmbPrintSizeInput);
            this.Controls.Add(chkAuto4W);
            this.Controls.Add(cmbOrderType);
            this.Controls.Add(btnPrint);
            this.Controls.Add(lblStatusLeft);
            this.Controls.Add(rightPanel);
        }

        private void ToggleTheme()
        {
            isDarkMode = !isDarkMode;
            config.IsDarkMode = isDarkMode;
            config.Save();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (isDarkMode)
            {
                darkBackground = Color.FromArgb(43, 43, 43);
                panelBackground = Color.FromArgb(54, 54, 54);
                textWhite = Color.White;
                topBar.BackColor = Color.FromArgb(22, 23, 27);
                lstFiles.BackColor = Color.FromArgb(70, 70, 70);
                lstFiles.ForeColor = Color.White;
                alertBox.BackColor = Color.FromArgb(30, 31, 35);
                btnThemeSwitch.Text = "🌙";
            }
            else
            {
                darkBackground = Color.FromArgb(242, 244, 247);
                panelBackground = Color.FromArgb(224, 228, 234);
                textWhite = Color.FromArgb(28, 30, 33);
                topBar.BackColor = Color.FromArgb(208, 213, 221);
                lstFiles.BackColor = Color.White;
                lstFiles.ForeColor = Color.FromArgb(28, 30, 33);
                alertBox.BackColor = Color.FromArgb(235, 238, 242);
                btnThemeSwitch.Text = "☀️";
            }

            this.BackColor = darkBackground;
            rightPanel.BackColor = panelBackground;

            btnThemeSwitch.BackColor = panelBackground;
            btnThemeSwitch.ForeColor = textWhite;
            btnHistory.BackColor = panelBackground;
            btnHistory.ForeColor = textWhite;
            btnClearAll.BackColor = panelBackground;
            btnClearAll.ForeColor = textWhite;
            btnExcelList.BackColor = panelBackground;
            btnExcelList.ForeColor = textWhite;
            btnSelectAll.BackColor = panelBackground;
            btnSelectAll.ForeColor = textWhite;
            btnUnselectAll.BackColor = panelBackground;
            btnUnselectAll.ForeColor = textWhite;

            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is Label lbl && lbl != lblMachineStatus && lbl != lblCanvasHeader)
                    lbl.ForeColor = textWhite;
                else if (ctrl is CheckBox chk)
                    chk.ForeColor = textWhite;
            }

            foreach (Control ctrl in rightPanel.Controls)
            {
                if (ctrl is Label lbl && lbl != lblCredit)
                    lbl.ForeColor = textWhite;
            }

            lblCanvasHeader.ForeColor = isDarkMode ? Color.FromArgb(130, 220, 210) : accentTeal;
            lblLastOrder.ForeColor = textWhite;
        }

        // ========================================================================
        // 1. SELECT FROM OTHER SOURCE
        // ========================================================================
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
                }
            }
        }

        // ========================================================================
        // 2. ULTRA-FAST ASYNC SEARCH ENGINE
        // ========================================================================
        private static readonly Regex PhotoFileNameRegex = new Regex(@"^P\d{9}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private async void SearchFilesAsync(string query)
        {
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
                SetStatus("Ready");
                return;
            }

            // Typing 3 characters or less returns no results
            if (query.Length <= 3)
            {
                lblFilesFound.Text = "0 Files Found";
                SetStatus("Search requires a valid Cabin (4-5 chars), Order ID (7 digits), or File Name (P + 9 digits).");
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
                    string ordersDir = config.OrdersFolder ?? @"Z:\PixelsOrder\Processed";
                    string? orderFilePath = null;

                    // 1. Order ID Search: Strictly 7 digits
                    if (query.Length == 7 && int.TryParse(query, out _))
                    {
                        if (Directory.Exists(ordersDir))
                        {
                            string direct1 = Path.Combine(ordersDir, query.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? query : query + ".txt");
                            string direct2 = Path.Combine(ordersDir, query);

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
                                        var match = Directory.EnumerateFiles(currDir, "*" + query + "*.txt").FirstOrDefault();
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
                            string printedDir = config.PrintedFolder ?? @"Z:\Printed";
                            var targetPhotos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            string[] lines = orderText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                if (line.StartsWith("FilePath=", StringComparison.OrdinalIgnoreCase))
                                {
                                    string fName = Path.GetFileName(line.Substring(9).Trim());
                                    if (!string.IsNullOrEmpty(fName)) targetPhotos.Add(fName);
                                }
                            }

                            if (targetPhotos.Count == 0)
                            {
                                var photoRegex = new Regex(@"\b(p\d+\.jpe?g|[A-Za-z0-9_\-\.]+\.(?:jpe?g|png|tif|bmp))\b", RegexOptions.IgnoreCase);
                                foreach (Match m in photoRegex.Matches(orderText))
                                {
                                    string fName = Path.GetFileName(m.Groups[1].Value.Trim());
                                    if (!string.IsNullOrEmpty(fName) && fName.IndexOf("Copyright Release", StringComparison.OrdinalIgnoreCase) < 0)
                                        targetPhotos.Add(fName);
                                }
                            }

                            var cabinMatch = Regex.Match(orderText, @"(?:ExtCabin|Cabin)\s*=\s*([0-9A-Za-z]+)", RegexOptions.IgnoreCase);
                            string extractedCabin = cabinMatch.Success ? cabinMatch.Groups[1].Value.Trim() : string.Empty;

                            if (Directory.Exists(printedDir) && targetPhotos.Count > 0)
                            {
                                foreach (string file in FastEnumerateImageFiles(printedDir, token))
                                {
                                    string fileName = Path.GetFileName(file);
                                    if (targetPhotos.Contains(fileName))
                                    {
                                        results.Add(file);
                                        targetPhotos.Remove(fileName);
                                        if (targetPhotos.Count == 0) break;
                                    }
                                }
                            }
                            return new OrderSearchResult { IsOrder = true, OrderId = Path.GetFileNameWithoutExtension(orderFilePath), Cabin = extractedCabin, Files = results };
                        }

                        return new OrderSearchResult { IsOrder = true, OrderId = query, Cabin = "", Files = results };
                    }

                    // 2. Cabin Folder Search: Strictly 4 to 5 characters (single token, no spaces/commas)
                    // Searches strictly for the folder matching that cabin number and returns photos inside it
                    if (query.Length >= 4 && query.Length <= 5 && !query.Contains(" ") && !query.Contains(","))
                    {
                        string downloadsDir = config.DownloadsFolder;
                        if (Directory.Exists(downloadsDir))
                        {
                            string targetSubFolder = Path.Combine(downloadsDir, query);
                            if (!Directory.Exists(targetSubFolder))
                            {
                                var pendingDirs = new Stack<string>();
                                pendingDirs.Push(downloadsDir);
                                while (pendingDirs.Count > 0)
                                {
                                    if (token.IsCancellationRequested) break;
                                    string currDir = pendingDirs.Pop();
                                    try
                                    {
                                        var dirs = Directory.EnumerateDirectories(currDir);
                                        foreach (var d in dirs)
                                        {
                                            if (Path.GetFileName(d).Equals(query, StringComparison.OrdinalIgnoreCase))
                                            {
                                                targetSubFolder = d;
                                                pendingDirs.Clear();
                                                break;
                                            }
                                            pendingDirs.Push(d);
                                        }
                                    }
                                    catch { }
                                }
                            }

                            if (Directory.Exists(targetSubFolder))
                            {
                                foreach (string f in FastEnumerateImageFiles(targetSubFolder, token))
                                    results.Add(f);
                            }
                        }
                        return new OrderSearchResult { IsOrder = false, Cabin = query, Files = results };
                    }

                    // 3. File / Photo Name Search: Starting with 'P' + 9 digits (e.g. P157921732)
                    // Supports separation via comma (with or without space) or space alone
                    string defaultPrinted = config.PrintedFolder ?? @"Z:\Printed";
                    if (Directory.Exists(defaultPrinted))
                    {
                        string[] rawTokens = query.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        var validPhotoNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (string raw in rawTokens)
                        {
                            string cleanToken = Path.GetFileNameWithoutExtension(raw.Trim());
                            if (PhotoFileNameRegex.IsMatch(cleanToken))
                            {
                                validPhotoNames.Add(cleanToken);
                            }
                        }

                        if (validPhotoNames.Count > 0)
                        {
                            foreach (string mf in FastEnumerateImageFiles(defaultPrinted, token))
                            {
                                string baseName = Path.GetFileNameWithoutExtension(mf);
                                if (validPhotoNames.Contains(baseName))
                                {
                                    results.Add(mf);
                                }
                            }
                        }
                    }
                    return new OrderSearchResult { IsOrder = false, Cabin = "", Files = results };
                }, token);

                lstFiles.BeginUpdate();
                int count = 0;
                foreach (string filePath in searchResult.Files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase))
                {
                    lstFiles.Items.Add(new FileItem { FileName = Path.GetFileName(filePath), FullPath = filePath }, true);
                    count++;
                }
                lstFiles.EndUpdate();

                if (!string.IsNullOrEmpty(searchResult.Cabin)) txtCabin.Text = searchResult.Cabin;
                lblFilesFound.Text = count + " Files Found";

                if (searchResult.IsOrder) SetStatus("Found Order " + searchResult.OrderId + ".txt with " + count + " photo(s) in " + (config.PrintedFolder ?? @"Z:\Printed") + " (ready to print).");
                else SetStatus("Ready - Found " + count + " matching files.");

                if (lstFiles.Items.Count > 0)
                {
                    lstFiles.SelectedIndex = 0;
                    PreviewSelectedImageSafe();
                }
            }
            catch (OperationCanceledException) { SetStatus("Search cancelled by operator."); }
            catch (Exception ex) { MessageBox.Show("Search Error: " + ex.Message, "Search Failed", MessageBoxButtons.OK, MessageBoxIcon.Error); SetStatus("Search failed."); }
            finally { btnFind.Enabled = true; btnCancelSearch.Enabled = false; progressBar.Visible = false; }
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
                        if (ImageExtensions.Contains(Path.GetExtension(file))) yield return file;
                    }
                }

                IEnumerable<string>? dirs = null;
                try { dirs = Directory.EnumerateDirectories(current); } catch { }
                if (dirs != null) foreach (string dir in dirs) pending.Push(dir);
            }
        }

        // ========================================================================
        // 3. IMPORT FROM CSV OR EXCEL CLIPBOARD COLUMN PASTE
        // ========================================================================
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
                        DialogResult dr = MessageBox.Show("Do you want to import file names directly from your Clipboard (copied from Excel)?\n\nClick 'Yes' to use Clipboard, or 'No' to browse for a CSV/Excel file.", "Import Source", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dr == DialogResult.Yes) rawInput = clipText;
                    }
                }

                if (string.IsNullOrEmpty(rawInput))
                {
                    using (OpenFileDialog ofd = new OpenFileDialog() { Title = "Select CSV or List File", Filter = "CSV/Text Files|*.csv;*.txt;*.xlsx;*.xls|All Files|*.*" })
                    {
                        if (ofd.ShowDialog() == DialogResult.OK) rawInput = File.ReadAllText(ofd.FileName);
                        else return;
                    }
                }

                lstFiles.BeginUpdate();
                lstFiles.Items.Clear();
                lstFiles.EndUpdate();
                ResetLogoPreview();

                string printedDir = config.PrintedFolder ?? @"Z:\Printed";
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
        // 4. ATOMIC HOTFOLDER JOB TICKET DISPATCHER (6,000,001 - 9,999,999)
        // ========================================================================
        private async void ExecutePrintJobAsync()
        {
            try
            {
                string machineTargetFolder = config.MachineC; 
                string selectedMachine = cmbMachine.SelectedItem != null ? cmbMachine.SelectedItem.ToString()! : "Machine C";

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

                string orderType = cmbOrderType.Text.Trim();
                if (string.IsNullOrEmpty(orderType)) orderType = "Package";

                string cabinNum = txtPrintCabinNum.Text.Trim();
                if (string.IsNullOrWhiteSpace(cabinNum)) { MessageBox.Show("Enter a cabin number before printing.", "Missing Cabin Number", MessageBoxButtons.OK, MessageBoxIcon.Warning); txtPrintCabinNum.Focus(); return; }
                string mediaType = "Glossy";

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("OrderReferenceID=");
                sb.AppendLine("OrderFirstName = " + orderType);
                sb.AppendLine("OrderLastName = ");
                sb.AppendLine("ExtCabin = " + cabinNum);
                sb.AppendLine("ExtFolio = ");
                sb.AppendLine();

                foreach (var item in checkedItems)
                {
                    sb.AppendLine("Qty=1");
                    sb.AppendLine("Size=" + printSize);
                    sb.AppendLine("PrintName=" + printSize);
                    sb.AppendLine("Media=" + mediaType);
                    sb.AppendLine("FilePath=" + item.FullPath);
                    sb.AppendLine();
                }

                string orderId = GetNext7CharOrderId();
                string tempFilePath = Path.Combine(machineTargetFolder, orderId + ".tmp");
                string finalFilePath = Path.Combine(machineTargetFolder, orderId + ".txt");

                await File.WriteAllTextAsync(tempFilePath, sb.ToString(), Encoding.UTF8);
                File.Move(tempFilePath, finalFilePath);

                LogOrderToAuditHistory(orderId, cabinNum, selectedMachine, printSize, checkedItems.Count, orderType);

                string statusMsg = "Dispatched " + orderId + ".txt to " + selectedMachine + " (" + checkedItems.Count + " files).";
                string dialogMsg = "Created order job " + orderId + ".txt containing " + checkedItems.Count + " file(s) for " + selectedMachine + "." + Environment.NewLine + Environment.NewLine + "Job Ticket: " + finalFilePath;
                MessageBox.Show(dialogMsg, "Print Job Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus(statusMsg);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Print Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetNext7CharOrderId()
        {
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                string candidate = Path.GetFileNameWithoutExtension(txtSearch.Text.Trim());
                if (candidate.Length == 7 && int.TryParse(candidate, out _))
                {
                    return candidate;
                }
            }

            int nextNum = Interlocked.Increment(ref currentOrderSequence);
            if (nextNum > 9999999)
            {
                nextNum = 1000001; 
                currentOrderSequence = 1000001;
            }
            return nextNum.ToString("D7");
        }

        // ========================================================================
        // 5. SAFE MEMORY PREVIEW WITH ASPECT RATIO CROP GUIDES
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

                        Image? oldImg = picLogo.Image;
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
                using (Pen wrapPen = new Pen(Color.Cyan, 1.5f) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(wrapPen, cropX + wrapMarginX, cropY + wrapMarginY, cropW - wrapMarginX * 2, cropH - wrapMarginY * 2);
            }
        }

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
            Image? oldImg = picLogo.Image;
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
            string selected = cmbMachine.SelectedItem != null ? cmbMachine.SelectedItem.ToString()! : "Machine C";
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

        private void PostCanvasOrder()
        {
            try
            {
                string targetDir = config.PrintshopFolder;
                Directory.CreateDirectory(targetDir);

                var checkedItems = lstFiles.CheckedItems.Cast<object>().OfType<FileItem>().ToList();
                if (checkedItems.Count == 0) { MessageBox.Show("Please check at least one file for canvas printing.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                string cabin = txtCanvasCabin.Text.Trim();
                if (string.IsNullOrWhiteSpace(cabin)) { MessageBox.Show("Please enter a cabin number for canvas order.", "Missing Cabin", MessageBoxButtons.OK, MessageBoxIcon.Warning); txtCanvasCabin.Focus(); return; }

                string size = cmbCanvasSize.SelectedItem != null ? cmbCanvasSize.SelectedItem.ToString()! : "16x20";
                string seller = txtSeller.Text.Trim();
                if (string.IsNullOrEmpty(seller)) seller = "FREE";

                int postedCount = 0;
                foreach (var item in checkedItems)
                {
                    string numPrefix = nextCanvasNumber.ToString("D3");
                    string baseName = Path.GetFileNameWithoutExtension(item.FileName);
                    string newFileName = numPrefix + "_" + cabin + "_" + size + "_" + baseName + "_" + seller + ".jpg";
                    string destinationPath = Path.Combine(targetDir, newFileName);

                    File.Copy(item.FullPath, destinationPath, true);
                    nextCanvasNumber++;
                    postedCount++;
                }

                UpdateNextCanvasNumber();
                SetStatus("Posted " + postedCount + " canvas order(s) to Printshop.");
                MessageBox.Show("Successfully generated " + postedCount + " Canvas Orders in Printshop Folder.", "Canvas Order Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        if (token.IsCancellationRequested) break;
                        var item = checkedItems[i];
                        string destFile = Path.Combine(destinationDir, item.FileName);

                        if (!File.Exists(destFile) || !AreFilesIdentical(item.FullPath, destFile))
                            File.Copy(item.FullPath, destFile, true);
                        
                        copied++;
                        BeginInvoke(new Action(() => { progressBar.Value = Math.Min(progressBar.Maximum, i + 1); }));
                    }
                }, token);

                SetStatus("Copied " + copied + "/" + checkedItems.Count + " files to " + description + ".");
            }
            catch (Exception ex) { MessageBox.Show("Copy failed: " + ex.Message, "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error); SetStatus("Copy failed: " + ex.Message); }
            finally { progressBar.Visible = false; }
        }

        private static bool AreFilesIdentical(string file1, string file2)
        {
            try
            {
                var fi1 = new FileInfo(file1); var fi2 = new FileInfo(file2);
                if (fi1.Length != fi2.Length) return false;
                using (var fs1 = File.OpenRead(file1)) using (var fs2 = File.OpenRead(file2))
                {
                    byte[] b1 = new byte[4096]; byte[] b2 = new byte[4096];
                    int r1 = fs1.Read(b1, 0, b1.Length); int r2 = fs2.Read(b2, 0, b2.Length);
                    if (r1 != r2) return false;
                    for (int i = 0; i < r1; i++) if (b1[i] != b2[i]) return false;
                }
                return true;
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
                Text = "labtools - Settings & HotFolder Paths",
                Size = new Size(690, 620),
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
            AddSettingRow(settingsForm, "Printshop Folder:", config.PrintshopFolder, ref y, (val) => config.PrintshopFolder = val);
            AddSettingRow(settingsForm, "CC Folder:", config.CCFolder, ref y, (val) => config.CCFolder = val);
            AddSettingRow(settingsForm, "White Images:", config.DestinationForWhiteImages, ref y, (val) => config.DestinationForWhiteImages = val);
            AddSettingRow(settingsForm, "Machine A:", config.MachineA, ref y, (val) => config.MachineA = val);
            AddSettingRow(settingsForm, "Machine B:", config.MachineB, ref y, (val) => config.MachineB = val);
            AddSettingRow(settingsForm, "Machine C:", config.MachineC, ref y, (val) => config.MachineC = val);
            AddSettingRow(settingsForm, "Machine D:", config.MachineD, ref y, (val) => config.MachineD = val);

            Button btnSave = new Button() { Text = "Save Settings", Location = new Point(240, y + 15), Width = 190, Height = 36, BackColor = accentTeal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            btnSave.Click += (s, e) =>
            {
                config.Save(); SetupPrintshopWatcher(); UpdateNextCanvasNumber(); CheckMachineHealthAsync();
                MessageBox.Show("Settings and HotFolder paths saved successfully!", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                settingsForm.Close();
            };
            settingsForm.Controls.Add(btnSave); settingsForm.ShowDialog();
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
        // 7. ORDER AUDIT HISTORY
        // ========================================================================
        private void LogOrderToAuditHistory(string orderId, string cabin, string machine, string size, int count, string type)
        {
            try
            {
                string ordersDir = config.OrdersFolder;
                if (!string.IsNullOrWhiteSpace(ordersDir))
                {
                    Directory.CreateDirectory(ordersDir);
                    string historyPath = Path.Combine(ordersDir, "OrderAuditHistory.csv");
                    if (!File.Exists(historyPath)) File.WriteAllText(historyPath, "Timestamp,OrderID,Cabin,Machine,Size,Qty,OrderType" + Environment.NewLine);
                    string line = "\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\",\"" + orderId + "\",\"" + cabin + "\",\"" + machine + "\",\"" + size + "\"," + count + ",\"" + type + "\"" + Environment.NewLine;
                    File.AppendAllText(historyPath, line);
                }
            }
            catch { }
        }

        private void OpenOrderHistoryDialog()
        {
            Form historyForm = new Form() { Text = "labtools - Order Audit History", Size = new Size(680, 480), StartPosition = FormStartPosition.CenterParent, BackColor = darkBackground, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Icon = appIcon };
            ListView lv = new ListView() { Location = new Point(20, 20), Width = 625, Height = 360, View = View.Details, FullRowSelect = true, BackColor = isDarkMode ? Color.FromArgb(50, 50, 50) : Color.White, ForeColor = textWhite };
            lv.Columns.Add("Time", 140); lv.Columns.Add("Order ID", 90); lv.Columns.Add("Cabin", 75); lv.Columns.Add("Machine", 90); lv.Columns.Add("Size", 120); lv.Columns.Add("Qty", 50);

            try
            {
                string historyPath = Path.Combine(config.OrdersFolder ?? @"Z:\PixelsOrder\Processed", "OrderAuditHistory.csv");
                if (File.Exists(historyPath))
                {
                    var lines = File.ReadAllLines(historyPath).Skip(1);
                    foreach (var line in lines.Reverse())
                    {
                        var parts = line.Split(',');
                        if (parts.Length >= 6) { var lvi = new ListViewItem(parts[0].Trim('"')); lvi.SubItems.Add(parts[1].Trim('"')); lvi.SubItems.Add(parts[2].Trim('"')); lvi.SubItems.Add(parts[3].Trim('"')); lvi.SubItems.Add(parts[4].Trim('"')); lvi.SubItems.Add(parts[5].Trim('"')); lv.Items.Add(lvi); }
                    }
                }
            }
            catch { }

            Button btnClose = new Button() { Text = "Close", Location = new Point(275, 395), Width = 110, Height = 32, BackColor = panelBackground, ForeColor = textWhite, FlatStyle = FlatStyle.Flat };
            btnClose.Click += (s, e) => historyForm.Close();

            historyForm.Controls.Add(lv); historyForm.Controls.Add(btnClose); historyForm.ShowDialog();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            searchCts?.Cancel(); copyCts?.Cancel(); folderWatcher?.Dispose(); printshopWatcher?.Dispose(); flashTimer?.Dispose(); picLogo?.Image?.Dispose(); base.OnFormClosed(e);
        }

        private class FileItem { public string FileName { get; set; } = ""; public string FullPath { get; set; } = ""; public override string ToString() => FileName; }
        private class OrderSearchResult { public bool IsOrder { get; set; } public string OrderId { get; set; } = ""; public string Cabin { get; set; } = ""; public List<string> Files { get; set; } = new List<string>(); }
    }
}
