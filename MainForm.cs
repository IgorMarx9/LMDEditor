using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace LMDTool
{
    public partial class MainForm : Form
    {
        enum TaskMode { Export, Import, Verify }

        // =========================
        // THEME
        // =========================

        Color DarkBlue = Color.FromArgb(18, 28, 44);
        Color DarkBluePanel = Color.FromArgb(22, 34, 56);
        Color ButtonBlue = Color.FromArgb(30, 50, 80);
        Color Fore = Color.WhiteSmoke;

        // =========================
        // CORE
        // =========================

        string gameRoot = "";
        string extension = "";
        bool isGMD = false;

        Panel menuPanel = null!;
        Panel mainPanel = null!;

        Button btn3G = null!;
        Button btn4G = null!;
        Button btnBack = null!;

        Button btnExport = null!;
        Button btnImport = null!;
        Button btnVerify = null!;

        TextBox logBox = null!;

        public MainForm()
        {
            InitializeForm();
            BuildMenu();
            BuildMainUI();
            ShowMenu();
        }

        void InitializeForm()
        {
            Text = "Monster Hunter String Tool";
            Width = 900;
            Height = 600;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = DarkBlue;
        }

        // =========================================================
        // MENU
        // =========================================================

        void BuildMenu()
        {
            menuPanel = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = DarkBluePanel
            };

            Label title = new Label()
            {
                Text = "Select Game",
                Dock = DockStyle.Top,
                Height = 80,
                Font = new Font("Segoe UI", 26, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Fore,
                BackColor = DarkBluePanel
            };

            btn3G = new Button()
            {
                Text = "MH 3G / 3U",
                Width = 260,
                Height = 220,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ImageAlign = ContentAlignment.TopCenter,
                TextAlign = ContentAlignment.BottomCenter,
                Left = 170,
                Top = 150
            };

            btn4G = new Button()
            {
                Text = "MH 4G / 4U",
                Width = 260,
                Height = 220,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ImageAlign = ContentAlignment.TopCenter,
                TextAlign = ContentAlignment.BottomCenter,
                Left = 450,
                Top = 150
            };

            StyleButton(btn3G);
            StyleButton(btn4G);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            LoadButtonImage(btn3G, Path.Combine(baseDir, "assets", "LAGGY.ico"));
            LoadButtonImage(btn4G, Path.Combine(baseDir, "assets", "SERGIO.ico"));

            btn3G.Click += (s, e) => Select3G();
            btn4G.Click += (s, e) => Select4G();

            menuPanel.Controls.Add(title);
            menuPanel.Controls.Add(btn3G);
            menuPanel.Controls.Add(btn4G);

            Controls.Add(menuPanel);
        }

        void ShowMenu()
        {
            menuPanel.Visible = true;
            mainPanel.Visible = false;
            Text = "Monster Hunter Text Tool";
        }

        // =========================================================
        // MAIN UI
        // =========================================================

        void BuildMainUI()
        {
            mainPanel = new Panel()
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = DarkBluePanel
            };

            btnBack = new Button()
            {
                Text = "← Back to menu",
                Width = 150,
                Height = 32,
                Left = 10,
                Top = 10
            };

            btnExport = new Button()
            {
                Text = "Export → TXT",
                Width = 180,
                Height = 44,
                Left = 30,
                Top = 70
            };

            btnImport = new Button()
            {
                Text = "Import → BIN",
                Width = 180,
                Height = 44,
                Left = 30,
                Top = 125
            };

            btnVerify = new Button()
            {
                Text = "Verify TXT",
                Width = 180,
                Height = 44,
                Left = 30,
                Top = 180
            };

            logBox = new TextBox()
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Left = 230,
                Top = 60,
                Width = 630,
                Height = 480,
                Font = new Font("Consolas", 10),
                ReadOnly = true,
                BackColor = Color.FromArgb(12, 20, 34),
                ForeColor = Fore,
                BorderStyle = BorderStyle.FixedSingle
            };

            StyleButton(btnBack);
            StyleButton(btnExport);
            StyleButton(btnImport);
            StyleButton(btnVerify);

            btnBack.Click += (s, e) => ShowMenu();
            btnExport.Click += (s, e) => RunTask(TaskMode.Export);
            btnImport.Click += (s, e) => RunTask(TaskMode.Import);
            btnVerify.Click += (s, e) => RunTask(TaskMode.Verify);

            mainPanel.Controls.Add(btnBack);
            mainPanel.Controls.Add(btnExport);
            mainPanel.Controls.Add(btnImport);
            mainPanel.Controls.Add(btnVerify);
            mainPanel.Controls.Add(logBox);

            Controls.Add(mainPanel);
        }

        void ShowMainUI(string title)
        {
            Text = title;
            menuPanel.Visible = false;
            mainPanel.Visible = true;
            logBox.Clear();
            Log("Ready.");
        }

        // =========================================================
        // GAME SELECT
        // =========================================================

        void Select3G()
        {
            gameRoot = "MH3G";
            extension = "*.gmd";
            isGMD = true;

            PrepareFolders();
            ShowMainUI("GMD Tool - Monster Hunter 3G / 3U");
        }

        void Select4G()
        {
            gameRoot = "MH4G";
            extension = "*.lmd";
            isGMD = false;

            PrepareFolders();
            ShowMainUI("LMD Tool - Monster Hunter 4G / 4U");
        }

        void PrepareFolders()
        {
            Directory.CreateDirectory(gameRoot);
            Directory.CreateDirectory(Path.Combine(gameRoot, "original"));
            Directory.CreateDirectory(Path.Combine(gameRoot, "txt"));
            Directory.CreateDirectory(Path.Combine(gameRoot, "output"));
            Directory.CreateDirectory(Path.Combine(gameRoot, "backup"));
            Directory.CreateDirectory(Path.Combine(gameRoot, "logs"));
        }

        // =========================================================
        // CORE
        // =========================================================

        void RunTask(TaskMode mode)
        {
            try
            {
                string originalDir = Path.Combine(gameRoot, "original");
                string[] files = Directory.GetFiles(originalDir, extension);

                if (files.Length == 0)
                {
                    Log("No files found in /original.");
                    return;
                }

                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    Log("Processing: " + name);

                    string txt = Path.Combine(gameRoot, "txt", Path.ChangeExtension(name, ".txt"));
                    string outBin = Path.Combine(gameRoot, "output", name);
                    string backup = Path.Combine(gameRoot, "backup", name);

                    if (mode != TaskMode.Export && !File.Exists(txt))
                    {
                        Log("❌ Missing TXT.");
                        continue;
                    }

                    if (mode != TaskMode.Export && !File.Exists(backup))
                        File.Copy(file, backup, true);

                    if (mode == TaskMode.Export)
                    {
                        if (isGMD) GMDParser.ExportToTxt(file, txt);
                        else LMDParser.ExportToTxt(file, txt);
                    }
                    else if (mode == TaskMode.Verify)
                    {
                        if (isGMD) GMDParser.Verify(file, txt);
                        else LMDParser.Verify(file, txt);
                    }
                    else
                    {
                        if (isGMD) GMDParser.ImportFromTxt(file, txt, outBin);
                        else LMDParser.ImportFromTxt(file, txt, outBin);
                    }

                    Log("✔ Done");
                }

                Log("Finished.");
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(gameRoot, "logs", "errors.log"),
                    DateTime.Now + " - " + ex + Environment.NewLine);

                Log("❌ ERROR: " + ex.Message);
            }
        }

        // =========================================================
        // UTILS
        // =========================================================

        void StyleButton(Button btn)
        {
            btn.BackColor = ButtonBlue;
            btn.ForeColor = Fore;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
        }

        void LoadButtonImage(Button btn, string file)
        {
            try
            {
                if (File.Exists(file))
                    btn.Image = new Icon(file, 96, 96).ToBitmap();
            }
            catch { }
        }

        void Log(string msg)
        {
            logBox.AppendText(msg + Environment.NewLine);
        }
    }
}
