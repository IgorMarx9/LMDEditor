using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LMDTool
{
    public partial class MainForm : Form
    {
        enum TaskMode
        {
            Export,
            Import,
            Verify
        }

        enum GameKind
        {
            None,
            MH3G,
            MH4G,
            MHXX
        }

        // ============================================================
        // PALETTE
        // ============================================================

        readonly Color BgPage = Color.FromArgb(13, 22, 34);
        readonly Color BgSidebar = Color.FromArgb(16, 26, 40);
        readonly Color BgCard = Color.FromArgb(22, 34, 47);
        readonly Color BgLog = Color.FromArgb(16, 26, 40);
        readonly Color BgTitleBar = Color.FromArgb(21, 32, 46);

        readonly Color Amber = Color.FromArgb(212, 130, 74);
        readonly Color AmberDark = Color.FromArgb(43, 22, 6);

        readonly Color TextPrimary = Color.FromArgb(240, 230, 210);
        readonly Color TextSecondary = Color.FromArgb(159, 176, 196);
        readonly Color TextMuted = Color.FromArgb(108, 124, 144);

        readonly Color StatusOk = Color.FromArgb(126, 196, 126);
        readonly Color StatusFail = Color.FromArgb(217, 112, 106);
        readonly Color StatusWarn = Color.FromArgb(212, 178, 90);
        readonly Color StatusInfo = Color.FromArgb(212, 130, 74);

        readonly Color ButtonSecondaryBg = Color.FromArgb(25, 40, 58);

        // ============================================================
        // STATE
        // ============================================================

        GameKind currentGame = GameKind.None;

        string gameRoot = "";
        string extension = "";

        Panel menuPanel = null!;
        Panel mainPanel = null!;

        Panel sidebarPanel = null!;
        Button btnBack = null!;
        Panel gameBadge = null!;
        Label lblGameBadgeName = null!;
        Label lblGameBadgeParser = null!;

        Button btnExport = null!;
        Button btnImport = null!;
        Button btnVerify = null!;
        Button btnQuests = null!;
        Button btnOpenRoot = null!;

        // Main content: two mutually-exclusive views inside contentPanel.
        Panel statsRow = null!;
        Panel statTotalCard = null!;
        Panel statSuccessCard = null!;
        Panel statFailedCard = null!;
        Panel statSkippedCard = null!;
        Panel logHost = null!;
        Panel questsViewPanel = null!;
        bool showingQuestsView = false;

        // Quests view controls
        Panel questsFolderRow = null!;
        readonly CheckBox[,] questChecks = new CheckBox[4, 3];
        readonly NumericUpDown[,] questRanges = new NumericUpDown[4, 2];
        Panel questsFilterPanel = null!;
        Panel questsActionsRow = null!;
        Label lblQuestsFolderValue = null!;
        Button btnQuestsBrowse = null!;

        Button btnQuestsExport = null!, btnQuestsImport = null!;
        Panel questsLogHost = null!;
        RichTextBox questsLogBox = null!;

        string questsSourceFolder = "";

        // Stat cards: Total / Success / Failed / Skipped
        Label lblStatTotalValue = null!;
        Label lblStatSuccessValue = null!;
        Label lblStatFailedValue = null!;
        Label lblStatSkippedValue = null!;
        Label lblStatTotalCaption = null!;
        Label lblStatSuccessCaption = null!;
        Label lblStatFailedCaption = null!;
        Label lblStatSkippedCaption = null!;
        Label lblStatTotalUnit = null!;
        Label lblStatSuccessUnit = null!;
        Label lblStatFailedUnit = null!;
        Label lblStatSkippedUnit = null!;

        RichTextBox logBox = null!;

        Button btnLanguage = null!;
        ContextMenuStrip languageMenu = null!;

        // Game selection cards (menu screen)
        Panel cardMH3G = null!;
        Panel cardMH4G = null!;
        Panel cardMHXX = null!;
        Label lblCard3GName = null!, lblCard3GFormat = null!;
        Label lblCard4GName = null!, lblCard4GFormat = null!;
        Label lblCardXXName = null!, lblCardXXFormat = null!;

        // Icon-loading warnings collected while building the menu screen
        // (no logBox exists yet at that point), flushed the next time the
        // main operation screen is shown.
        List<string> pendingWarnings = new List<string>();

        public MainForm()
        {
            InitializeForm();
            BuildMenu();
            BuildMainUI();
            ShowMenu();

            Localization.LanguageChanged += OnLanguageChanged;
            FormClosed += (s, e) => Localization.LanguageChanged -= OnLanguageChanged;
        }

        void InitializeForm()
        {
            Text = Localization.T("appTitle");

            // A janela agora pode ser redimensionada/maximizada pelo usuário.
            // ClientSize evita que bordas/título do Windows reduzam a área útil.
            ClientSize = new Size(1100, 720);
            MinimumSize = new Size(900, 620);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            SizeGripStyle = SizeGripStyle.Show;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
            BackColor = BgPage;
        }

        // ============================================================
        // LANGUAGE PICKER (shared by both screens)
        // ============================================================

        Button BuildLanguageButton()
        {
            Button btn = new Button()
            {
                Text = CurrentLanguageDisplayName() + "  ▾",
                Width = 90,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = BgTitleBar,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
            };

            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(40, 52, 66);
            ConfigureButtonHover(
                btn,
                BgTitleBar,
                Color.FromArgb(30, 44, 62),
                Color.FromArgb(17, 26, 38)
            );

            languageMenu = new ContextMenuStrip();

            foreach (var option in Localization.LanguageOptions)
            {
                var item = new ToolStripMenuItem(option.DisplayName);
                item.Click += (s, e) => Localization.SetLanguage(option.Lang);
                languageMenu.Items.Add(item);
            }

            btn.Click += (s, e) => languageMenu.Show(btn, new Point(0, btn.Height));

            return btn;
        }

        string CurrentLanguageDisplayName()
        {
            foreach (var option in Localization.LanguageOptions)
            {
                if (option.Lang == Localization.Current)
                    return option.DisplayName;
            }

            return "?";
        }

        void OnLanguageChanged()
        {
            // Rebuild both screens from scratch - simplest way to guarantee
            // every label picks up the new language with no stale text.
            bool wasShowingMenu = menuPanel.Visible;

            Controls.Clear();

            BuildMenu();
            BuildMainUI();

            if (wasShowingMenu || currentGame == GameKind.None)
            {
                ShowMenu();
            }
            else
            {
                ShowMainUI();
            }
        }

        // ============================================================
        // GAME SELECTION SCREEN
        // ============================================================

        void BuildMenu()
        {
            menuPanel = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = BgPage
            };

            Panel titleBar = BuildTitleBar();

            Panel cardsHost = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = BgPage
            };

            cardMH3G = BuildGameCard(
                out lblCard3GName, out lblCard3GFormat,
                "LAGGY.ico",
                Color.FromArgb(26, 44, 62),
                Color.FromArgb(111, 168, 201)
            );

            cardMH4G = BuildGameCard(
                out lblCard4GName, out lblCard4GFormat,
                "SERGIO.ico",
                Color.FromArgb(26, 44, 62),
                Color.FromArgb(217, 122, 74)
            );

            cardMHXX = BuildGameCard(
                out lblCardXXName, out lblCardXXFormat,
                "VAL.ico",
                Color.FromArgb(26, 44, 62),
                Amber
            );

            RefreshGameCardLabels();

            cardMH3G.Click += (s, e) => Select3G();
            cardMH4G.Click += (s, e) => Select4G();
            cardMHXX.Click += (s, e) => SelectXX();

            // Center the three cards as a group inside cardsHost.
            cardsHost.Resize += (s, e) => LayoutGameCards(cardsHost);

            cardsHost.Controls.Add(cardMH3G);
            cardsHost.Controls.Add(cardMH4G);
            cardsHost.Controls.Add(cardMHXX);

            menuPanel.Controls.Add(cardsHost);
            menuPanel.Controls.Add(titleBar);

            Controls.Add(menuPanel);

            LayoutGameCards(cardsHost);
        }

        void LayoutGameCards(Control host)
        {
            int cardWidth = cardMH3G.Width;
            int cardHeight = cardMH3G.Height;
            int gap = 18;
            int sidePadding = 24;

            int availableWidth = Math.Max(1, host.ClientSize.Width - sidePadding * 2);
            int columns = availableWidth >= cardWidth * 3 + gap * 2
                ? 3
                : availableWidth >= cardWidth * 2 + gap
                    ? 2
                    : 1;

            Panel[] cards = { cardMH3G, cardMH4G, cardMHXX };
            int rows = (int)Math.Ceiling(cards.Length / (double)columns);

            int totalWidth = columns * cardWidth + (columns - 1) * gap;
            int totalHeight = rows * cardHeight + (rows - 1) * gap;

            int startX = Math.Max(sidePadding, (host.ClientSize.Width - totalWidth) / 2);
            int startY = Math.Max(20, (host.ClientSize.Height - totalHeight) / 2);

            for (int i = 0; i < cards.Length; i++)
            {
                int col = i % columns;
                int row = i / columns;

                cards[i].Left = startX + col * (cardWidth + gap);
                cards[i].Top = startY + row * (cardHeight + gap);
            }
        }

        Panel BuildGameCard(
            out Label nameLabel,
            out Label formatLabel,
            string iconResourceName,
            Color iconCircleColor,
            Color accentColor
        )
        {
            Panel card = new Panel()
            {
                Width = 220,
                Height = 200,
                BackColor = BgCard,
                Cursor = Cursors.Hand,
            };

            Panel iconCircle = new Panel()
            {
                Width = 56,
                Height = 56,
                BackColor = iconCircleColor,
                Left = (card.Width - 56) / 2,
                Top = 22,
            };

            MakeRoundedPanel(iconCircle, 28);

            PictureBox iconBox = new PictureBox()
            {
                Width = 36,
                Height = 36,
                Left = (iconCircle.Width - 36) / 2,
                Top = (iconCircle.Height - 36) / 2,
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = LoadIconImage(iconResourceName, new Size(36, 36)),
            };

            nameLabel = new Label()
            {
                AutoSize = false,
                Width = card.Width,
                Height = 20,
                Top = 96,
                Left = 0,
                ForeColor = TextPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            formatLabel = new Label()
            {
                AutoSize = false,
                Width = card.Width,
                Height = 16,
                Top = 118,
                Left = 0,
                ForeColor = TextMuted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            Panel accentBar = new Panel()
            {
                Width = card.Width - 32,
                Height = 3,
                Left = 16,
                Top = card.Height - 24,
                BackColor = accentColor,
            };

            card.Controls.Add(iconCircle);
            iconCircle.Controls.Add(iconBox);
            card.Controls.Add(nameLabel);
            card.Controls.Add(formatLabel);
            card.Controls.Add(accentBar);

            // Clicking any child of the card should trigger the same
            // selection as clicking the card itself.
            EventHandler bubble = (s, e) =>
            {
                if (card == cardMH3G) Select3G();
                else if (card == cardMH4G) Select4G();
                else if (card == cardMHXX) SelectXX();
            };

            iconCircle.Click += bubble;
            iconBox.Click += bubble;
            nameLabel.Click += bubble;
            formatLabel.Click += bubble;
            accentBar.Click += bubble;

            return card;
        }

        void RefreshGameCardLabels()
        {
            lblCard3GName.Text = Localization.T("game3GName");
            lblCard3GFormat.Text = Localization.T("game3GFormat");

            lblCard4GName.Text = Localization.T("game4GName");
            lblCard4GFormat.Text = Localization.T("game4GFormat");

            lblCardXXName.Text = Localization.T("gameXXName");
            lblCardXXFormat.Text = Localization.T("gameXXFormat");
        }

        // ============================================================
        // MAIN OPERATION SCREEN
        // ============================================================

        void BuildMainUI()
        {
            mainPanel = new Panel()
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = BgPage
            };

            Panel titleBar = BuildTitleBar();

            sidebarPanel = new Panel()
            {
                Dock = DockStyle.Left,
                Width = 260,
                BackColor = BgSidebar,
                Padding = new Padding(14, 18, 14, 14),
            };

            btnBack = new Button()
            {
                Text = Localization.T("backToMenu"),
                Width = 232,
                Height = 32,
                Left = 14,
                Top = 18,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            btnBack.FlatAppearance.BorderSize = 1;
            btnBack.FlatAppearance.BorderColor = Color.FromArgb(40, 52, 66);
            btnBack.AutoEllipsis = true;
            ConfigureButtonHover(
                btnBack,
                Color.Transparent,
                Color.FromArgb(24, 37, 54),
                Color.FromArgb(18, 28, 42)
            );

            gameBadge = new Panel()
            {
                Width = 232,
                Height = 56,
                Left = 14,
                Top = 68,
                BackColor = Color.FromArgb(22, 34, 47),
            };

            Panel badgeAccent = new Panel()
            {
                Width = 3,
                Height = gameBadge.Height,
                Left = 0,
                Top = 0,
                BackColor = Amber,
            };

            lblGameBadgeName = new Label()
            {
                AutoSize = false,
                Left = 14,
                Top = 9,
                Width = gameBadge.Width - 28,
                Height = 20,
                ForeColor = TextPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                AutoEllipsis = true,
            };

            lblGameBadgeParser = new Label()
            {
                AutoSize = false,
                Left = 14,
                Top = 31,
                Width = gameBadge.Width - 28,
                Height = 17,
                ForeColor = TextSecondary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8),
                AutoEllipsis = true,
            };

            gameBadge.Controls.Add(badgeAccent);
            gameBadge.Controls.Add(lblGameBadgeName);
            gameBadge.Controls.Add(lblGameBadgeParser);

            Label actionsHeader = new Label()
            {
                Text = Localization.T("actionsHeader").ToUpperInvariant(),
                AutoSize = false,
                Left = 18,
                Top = 142,
                Width = 224,
                Height = 16,
                ForeColor = TextMuted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            };

            btnExport = BuildSidebarButton(Localization.T("export"), 168, primary: true);
            btnImport = BuildSidebarButton(Localization.T("importGmd"), 218, primary: false);
            btnVerify = BuildSidebarButton(Localization.T("verify"), 268, primary: false);
            btnQuests = BuildSidebarButton(Localization.T("quests"), 318, primary: false);

            Panel sep = new Panel()
            {
                Width = 232,
                Height = 1,
                Left = 14,
                Top = 374,
                BackColor = Color.FromArgb(40, 52, 66),
            };

            btnOpenRoot = BuildSidebarButton(Localization.T("openGameFolder"), 390, primary: false, dashed: true);

            btnBack.Click += (s, e) => ShowMenu();
            btnExport.Click += (s, e) => ShowStandardView(() => RunTask(TaskMode.Export));
            btnImport.Click += (s, e) => ShowStandardView(() => RunTask(TaskMode.Import));
            btnVerify.Click += (s, e) => ShowStandardView(() => RunTask(TaskMode.Verify));
            btnQuests.Click += (s, e) => ShowQuestsView();
            btnOpenRoot.Click += (s, e) => OpenCurrentGameFolder();

            sidebarPanel.Controls.Add(btnBack);
            sidebarPanel.Controls.Add(gameBadge);
            sidebarPanel.Controls.Add(actionsHeader);
            sidebarPanel.Controls.Add(btnExport);
            sidebarPanel.Controls.Add(btnImport);
            sidebarPanel.Controls.Add(btnVerify);
            sidebarPanel.Controls.Add(btnQuests);
            sidebarPanel.Controls.Add(sep);
            sidebarPanel.Controls.Add(btnOpenRoot);

            // ---- Right side: stat cards + log (standard view) ----

            Panel contentPanel = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = BgPage,
                Padding = new Padding(20, 18, 20, 18),
            };

            statsRow = new Panel()
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = BgPage,
            };

            statTotalCard = BuildStatCard(0, 150,
                out lblStatTotalCaption, out lblStatTotalValue, out lblStatTotalUnit, TextSecondary);

            statSuccessCard = BuildStatCard(0, 150,
                out lblStatSuccessCaption, out lblStatSuccessValue, out lblStatSuccessUnit, StatusOk);

            statFailedCard = BuildStatCard(0, 150,
                out lblStatFailedCaption, out lblStatFailedValue, out lblStatFailedUnit, StatusFail);

            statSkippedCard = BuildStatCard(0, 150,
                out lblStatSkippedCaption, out lblStatSkippedValue, out lblStatSkippedUnit, StatusWarn);

            statsRow.Controls.Add(statTotalCard);
            statsRow.Controls.Add(statSuccessCard);
            statsRow.Controls.Add(statFailedCard);
            statsRow.Controls.Add(statSkippedCard);
            statsRow.Resize += (s, e) => LayoutStatCards();

            ResetStatCards();
            LayoutStatCards();

            logBox = new RichTextBox()
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.5f),
                ReadOnly = true,
                BackColor = BgLog,
                ForeColor = TextSecondary,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 12, 0, 0),
            };

            logHost = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = BgLog,
                Padding = new Padding(14, 10, 14, 10),
            };
            logHost.Controls.Add(logBox);

            // ---- Right side: quests view (filters + folder + actions) ----

            questsViewPanel = BuildQuestsView();
            questsViewPanel.Visible = false;

            contentPanel.Controls.Add(questsViewPanel);
            contentPanel.Controls.Add(logHost);
            contentPanel.Controls.Add(statsRow);

            mainPanel.Controls.Add(contentPanel);
            mainPanel.Controls.Add(sidebarPanel);
            mainPanel.Controls.Add(titleBar);

            Controls.Add(mainPanel);
        }

        void ShowStandardView(Action action)
        {
            showingQuestsView = false;
            questsViewPanel.Visible = false;
            statsRow.Visible = true;
            logHost.Visible = true;

            action();
        }

        void ShowQuestsView()
        {
            showingQuestsView = true;
            statsRow.Visible = false;
            logHost.Visible = false;
            questsViewPanel.Visible = true;
        }

        Button BuildSidebarButton(string text, int top, bool primary, bool dashed = false)
        {
            Button btn = new Button()
            {
                Text = text,
                Width = 232,
                Height = 42,
                Left = 14,
                Top = top,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
                Cursor = Cursors.Hand,
                AutoEllipsis = true,
            };

            if (primary)
            {
                btn.BackColor = Amber;
                btn.ForeColor = AmberDark;
                btn.FlatAppearance.BorderSize = 0;
                ConfigureButtonHover(
                    btn,
                    Amber,
                    Color.FromArgb(226, 148, 88),
                    Color.FromArgb(190, 110, 60)
                );
            }
            else
            {
                btn.BackColor = ButtonSecondaryBg;
                btn.ForeColor = TextPrimary;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = Color.FromArgb(40, 52, 66);

                if (dashed)
                {
                    btn.ForeColor = TextSecondary;
                    btn.BackColor = Color.Transparent;
                    ConfigureButtonHover(
                        btn,
                        Color.Transparent,
                        Color.FromArgb(24, 37, 54),
                        Color.FromArgb(18, 28, 42)
                    );
                }
                else
                {
                    ConfigureButtonHover(
                        btn,
                        ButtonSecondaryBg,
                        Color.FromArgb(34, 52, 74),
                        Color.FromArgb(18, 30, 44)
                    );
                }
            }

            return btn;
        }

        Panel BuildStatCard(int left, int width, out Label caption, out Label value, out Label unit, Color valueColor)
        {
            Panel card = new Panel()
            {
                Width = width,
                Height = 68,
                Left = left,
                Top = 0,
                BackColor = BgCard,
            };

            caption = new Label()
            {
                AutoSize = false,
                Left = 14,
                Top = 10,
                Width = width - 28,
                Height = 16,
                ForeColor = TextMuted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
            };

            value = new Label()
            {
                AutoSize = false,
                Left = 14,
                Top = 30,
                Width = 62,
                Height = 28,
                ForeColor = valueColor,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
            };

            unit = new Label()
            {
                AutoSize = false,
                Left = 62,
                Top = 42,
                Width = width - 76,
                Height = 16,
                ForeColor = TextMuted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
            };

            card.Controls.Add(caption);
            card.Controls.Add(value);
            card.Controls.Add(unit);

            return card;
        }

        void LayoutStatCards()
        {
            if (statsRow == null || statTotalCard == null)
                return;

            int gap = 14;
            Panel[] cards = { statTotalCard, statSuccessCard, statFailedCard, statSkippedCard };
            int availableWidth = Math.Max(1, statsRow.ClientSize.Width);
            int cardWidth = Math.Max(124, (availableWidth - gap * (cards.Length - 1)) / cards.Length);

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].Bounds = new Rectangle(i * (cardWidth + gap), 0, cardWidth, 68);

                foreach (Control child in cards[i].Controls)
                {
                    if (child is Label label)
                    {
                        if (label.Left >= 62)
                            label.Width = Math.Max(20, cards[i].Width - label.Left - 12);
                        else
                            label.Width = Math.Max(20, cards[i].Width - label.Left - 14);
                    }
                }
            }
        }

        void ResetStatCards()
        {
            lblStatTotalCaption.Text = Localization.T("statTotal").ToUpperInvariant();
            lblStatSuccessCaption.Text = Localization.T("statSuccess").ToUpperInvariant();
            lblStatFailedCaption.Text = Localization.T("statFailed").ToUpperInvariant();
            lblStatSkippedCaption.Text = Localization.T("statSkipped").ToUpperInvariant();

            lblStatTotalUnit.Text = Localization.T("statFilesUnit");
            lblStatSuccessUnit.Text = Localization.T("statFilesUnit");
            lblStatFailedUnit.Text = Localization.T("statFilesUnit");
            lblStatSkippedUnit.Text = Localization.T("statFilesUnit");

            lblStatTotalValue.Text = "0";
            lblStatSuccessValue.Text = "0";
            lblStatFailedValue.Text = "0";
            lblStatSkippedValue.Text = "0";
        }

        // ============================================================
        // QUESTS VIEW
        // ============================================================

        Panel BuildQuestsView()
        {
            Panel root = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = BgPage,
            };

            // ---- Folder picker row ----

            questsFolderRow = new Panel()
            {
                Dock = DockStyle.Top,
                Height = 66,
                BackColor = BgCard,
                Margin = new Padding(0, 0, 0, 14),
            };

            Label folderCaption = new Label()
            {
                Text = Localization.T("questsFolderLabel"),
                AutoSize = false,
                Left = 18,
                Top = 9,
                Width = 400,
                Height = 16,
                ForeColor = TextMuted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                AutoEllipsis = true,
            };

            lblQuestsFolderValue = new Label()
            {
                Text = Localization.T("questsFolderNotSet"),
                AutoSize = false,
                Left = 18,
                Top = 32,
                Width = 360,
                Height = 22,
                ForeColor = TextPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9.5f),
                AutoEllipsis = true,
            };

            btnQuestsBrowse = new Button()
            {
                Text = Localization.T("questsBrowse"),
                Width = 130,
                Height = 32,
                Left = 0,
                Top = 17,
                FlatStyle = FlatStyle.Flat,
                BackColor = ButtonSecondaryBg,
                ForeColor = TextPrimary,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9),
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnQuestsBrowse.FlatAppearance.BorderSize = 1;
            btnQuestsBrowse.FlatAppearance.BorderColor = Color.FromArgb(40, 52, 66);
            ConfigureButtonHover(
                btnQuestsBrowse,
                ButtonSecondaryBg,
                Color.FromArgb(34, 52, 74),
                Color.FromArgb(18, 30, 44)
            );
            btnQuestsBrowse.Click += (s, e) => BrowseForQuestsFolder();

            questsFolderRow.Controls.Add(folderCaption);
            questsFolderRow.Controls.Add(lblQuestsFolderValue);
            questsFolderRow.Controls.Add(btnQuestsBrowse);
            questsFolderRow.Resize += (s, e) => LayoutQuestsFolderRow(folderCaption);

            // ---- Filter panel: category checkboxes + star range ----

            questsFilterPanel = new Panel()
            {
                Dock = DockStyle.Top,
                Height = 176,
                BackColor = BgCard,
                Margin = new Padding(0, 0, 0, 14),
            };

            Label filterCaption = new Label()
            {
                Text = Localization.T("questsFilterLabel"),
                AutoSize = false,
                Left = 18,
                Top = 10,
                Width = 400,
                Height = 16,
                ForeColor = TextMuted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                AutoEllipsis = true,
            };

            Label starsCaption = new Label(), starsDash = new Label();
            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4,
                RowCount = 1, Padding = new Padding(12, 8, 12, 8), BackColor = BgCard };
            string[] titles = { Localization.T("questColumnVillage"), Localization.T("questColumnHub"),
                Localization.T("questColumnPub"), Localization.T("questColumnSpecial") };
            for (int c = 0; c < 4; c++)
            {
                columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
                var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                    WrapContents = false, AutoScroll = true };
                panel.Controls.Add(new Label { Text = titles[c], AutoSize = true, ForeColor = TextSecondary,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(3, 3, 3, 10) });
                string[] labels = { Localization.T("questFilterQuests"), Localization.T("questFilterProwlers"), Localization.T("questFilterSpecial") };
                for (int r = 0; r < 3; r++)
                {
                    var box = BuildFilterCheckbox(labels[r], 0, 0);
                    box.AutoSize = true; questChecks[c, r] = box;
                    if ((c < 3 && r < 2) || (c == 3 && r == 0)) panel.Controls.Add(box);
                    else box.Checked = false;
                }
                panel.Controls.Add(new Label { Text = Localization.T(c == 2 ? "questsGRankLabel" : c == 3 ? "questsSpecialLevelLabel" : "questsStarsLabel"), AutoSize = true, ForeColor = TextSecondary });
                var range = new FlowLayoutPanel { Width = 170, Height = 36, WrapContents = false };
                for (int r = 0; r < 2; r++)
                {
                    var number = BuildStarsNumericUpDown(0, 0);
                    number.Width = 55; number.Maximum = c == 3 ? 18 : c == 2 ? 4 : 10;
                    number.Value = r == 0 ? 1 : number.Maximum;
                    questRanges[c, r] = number; range.Controls.Add(number);
                    if (r == 0) range.Controls.Add(new Label { Text = Localization.T("questsRangeTo"), Width = 22, ForeColor = TextSecondary });
                }
                panel.Controls.Add(range); columns.Controls.Add(panel, c, 0);
            }
            questsFilterPanel.Height = 225;
            questsFilterPanel.Controls.Add(columns);

            // ---- Action buttons row ----

            questsActionsRow = new Panel()
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = BgPage,
                Margin = new Padding(0, 0, 0, 14),
            };

            btnQuestsExport = new Button()
            {
                Text = Localization.T("questsExport"),
                Width = 220,
                Height = 42,
                Left = 0,
                Top = 2,
                FlatStyle = FlatStyle.Flat,
                BackColor = Amber,
                ForeColor = AmberDark,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                AutoEllipsis = true,
            };
            btnQuestsExport.FlatAppearance.BorderSize = 0;
            ConfigureButtonHover(
                btnQuestsExport,
                Amber,
                Color.FromArgb(226, 148, 88),
                Color.FromArgb(190, 110, 60)
            );
            btnQuestsExport.Click += (s, e) => RunQuestsExport();

            btnQuestsImport = new Button()
            {
                Text = Localization.T("questsImport"),
                Width = 220,
                Height = 42,
                Left = 234,
                Top = 2,
                FlatStyle = FlatStyle.Flat,
                BackColor = ButtonSecondaryBg,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9.5f),
                Cursor = Cursors.Hand,
                AutoEllipsis = true,
            };
            btnQuestsImport.FlatAppearance.BorderSize = 1;
            btnQuestsImport.FlatAppearance.BorderColor = Color.FromArgb(40, 52, 66);
            ConfigureButtonHover(
                btnQuestsImport,
                ButtonSecondaryBg,
                Color.FromArgb(34, 52, 74),
                Color.FromArgb(18, 30, 44)
            );
            btnQuestsImport.Click += (s, e) => RunQuestsImport();

            questsActionsRow.Controls.Add(btnQuestsExport);
            questsActionsRow.Controls.Add(btnQuestsImport);
            questsActionsRow.Resize += (s, e) => LayoutQuestsActionsRow();

            // ---- Log: the quests view has its own log box, since a
            //      WinForms control can only belong to one parent at a
            //      time (it can't share logHost with the standard view).
            //      AppendLogLine writes to whichever log is active. ----

            questsLogHost = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = BgLog,
                Padding = new Padding(16, 12, 16, 12),
            };

            questsLogBox = new RichTextBox()
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.5f),
                ReadOnly = true,
                BackColor = BgLog,
                ForeColor = TextSecondary,
                BorderStyle = BorderStyle.None,
            };
            questsLogHost.Controls.Add(questsLogBox);

            root.Controls.Add(questsLogHost);
            root.Controls.Add(questsActionsRow);
            root.Controls.Add(questsFilterPanel);
            root.Controls.Add(questsFolderRow);

            LoadRememberedQuestsFolder();
            LayoutQuestsFolderRow(folderCaption);
            LayoutQuestsFilters(filterCaption, starsCaption, starsDash);
            LayoutQuestsActionsRow();

            return root;
        }

        CheckBox BuildFilterCheckbox(string text, int left, int top)
        {
            return new CheckBox()
            {
                Text = text,
                AutoSize = false,
                Left = left,
                Top = top,
                Width = 190,
                Height = 24,
                Checked = true,
                ForeColor = TextPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9),
                AutoEllipsis = true,
            };
        }

        NumericUpDown BuildStarsNumericUpDown(int left, int top)
        {
            return new NumericUpDown()
            {
                Left = left,
                Top = top,
                Width = 62,
                Height = 24,
                Minimum = 1,
                Maximum = 10,
                Value = 1,
                BackColor = ButtonSecondaryBg,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
            };
        }

        void LayoutQuestsFolderRow(Label folderCaption)
        {
            if (questsFolderRow == null || lblQuestsFolderValue == null || btnQuestsBrowse == null)
                return;

            int pad = 18;
            btnQuestsBrowse.Left = Math.Max(pad, questsFolderRow.ClientSize.Width - btnQuestsBrowse.Width - pad);
            btnQuestsBrowse.Top = 17;

            folderCaption.Left = pad;
            folderCaption.Width = Math.Max(80, questsFolderRow.ClientSize.Width - pad * 2);

            lblQuestsFolderValue.Left = pad;
            lblQuestsFolderValue.Width = Math.Max(80, btnQuestsBrowse.Left - lblQuestsFolderValue.Left - 14);
        }

        void LayoutQuestsFilters(Label filterCaption, Label starsCaption, Label starsDash)
        {
            // The four equal-width columns are laid out by TableLayoutPanel.
        }

        void LayoutQuestsActionsRow()
        {
            if (questsActionsRow == null || btnQuestsExport == null || btnQuestsImport == null)
                return;

            int gap = 14;
            int availableWidth = Math.Max(1, questsActionsRow.ClientSize.Width);
            int buttonWidth = Math.Min(280, Math.Max(190, (availableWidth - gap) / 2));

            btnQuestsExport.SetBounds(0, 2, buttonWidth, 42);
            btnQuestsImport.SetBounds(buttonWidth + gap, 2, buttonWidth, 42);
        }

        void LoadRememberedQuestsFolder()
        {
            string? remembered = AppConfig.Get("QuestsFolder");

            if (!string.IsNullOrWhiteSpace(remembered) && Directory.Exists(remembered))
            {
                questsSourceFolder = remembered;
                lblQuestsFolderValue.Text = remembered;
            }
        }

        void BrowseForQuestsFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = Localization.T("questsBrowseDialogTitle");
                dialog.UseDescriptionForTitle = true;

                if (!string.IsNullOrWhiteSpace(questsSourceFolder) && Directory.Exists(questsSourceFolder))
                    dialog.SelectedPath = questsSourceFolder;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    questsSourceFolder = dialog.SelectedPath;
                    lblQuestsFolderValue.Text = questsSourceFolder;
                    AppConfig.Set("QuestsFolder", questsSourceFolder);
                }
            }
        }

        QuestFilter BuildQuestFilterFromUI()
        {
            var result = new QuestFilter();
            QuestColumn[] columns = { result.Village, result.Hub, result.Pub, result.Special };
            for (int c = 0; c < 4; c++)
            {
                columns[c].Regular = questChecks[c, 0].Checked;
                columns[c].Prowler = questChecks[c, 1].Checked;
                columns[c].Special = questChecks[c, 2].Checked;
                columns[c].Min = (int)questRanges[c, 0].Value;
                columns[c].Max = (int)questRanges[c, 1].Value;
            }
            return result;
        }

        void RunQuestsExport()
        {
            if (string.IsNullOrWhiteSpace(questsSourceFolder) || !Directory.Exists(questsSourceFolder))
            {
                AppendLogLine(Localization.T("questsNoFolderSelected"), StatusFail);
                return;
            }

            QuestFilter filter = BuildQuestFilterFromUI();

            if (!filter.Valid)
            {
                AppendLogLine(Localization.T("questsInvalidStarRange"), StatusFail);
                return;
            }

            string destRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QuestsText");

            AppendLogLine(Localization.T("logTaskLabel") + Localization.T("taskNameExport") + " (" + Localization.T("quests") + ")", null);
            AppendLogLine("", null);

            var result = QuestBatch.ExportFolder(
                questsSourceFolder,
                destRoot,
                filter,
                (fileName, status, detail) => LogQuestEntry(fileName, status, detail)
            );

            AppendLogLine("", null);
            AppendLogLine(Localization.T("logFinished"), null);
            AppendLogLine(
                Localization.T("statTotal") + ": " + result.Total
                + "  " + Localization.T("statSuccess") + ": " + result.Success
                + "  " + Localization.T("statFailed") + ": " + result.Failed
                + "  " + Localization.T("statSkipped") + ": " + result.Skipped,
                null
            );
        }

        void RunQuestsImport()
        {
            if (string.IsNullOrWhiteSpace(questsSourceFolder) || !Directory.Exists(questsSourceFolder))
            {
                AppendLogLine(Localization.T("questsNoFolderSelected"), StatusFail);
                return;
            }

            QuestFilter filter = BuildQuestFilterFromUI();

            if (!filter.Valid)
            {
                AppendLogLine(Localization.T("questsInvalidStarRange"), StatusFail);
                return;
            }

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string outputRoot = Path.Combine(exeDir, "QuestsOutput");

            AppendLogLine(Localization.T("logTaskLabel") + Localization.T("taskNameImport") + " (" + Localization.T("quests") + ")", null);
            AppendLogLine("", null);

            var result = QuestBatch.ImportFolder(
                questsSourceFolder,
                Path.Combine(exeDir, "QuestsText"),
                outputRoot,
                filter,
                (fileName, status, detail) => LogQuestEntry(fileName, status, detail)
            );

            AppendLogLine("", null);
            AppendLogLine(Localization.T("logFinished"), null);
            AppendLogLine(
                Localization.T("statTotal") + ": " + result.Total
                + "  " + Localization.T("statSuccess") + ": " + result.Success
                + "  " + Localization.T("statFailed") + ": " + result.Failed
                + "  " + Localization.T("statSkipped") + ": " + result.Skipped,
                null
            );
        }

        void LogQuestEntry(string fileName, QuestLogStatus status, string detail)
        {
            Color color = status switch
            {
                QuestLogStatus.Success => StatusOk,
                QuestLogStatus.Warning => StatusWarn,
                QuestLogStatus.Failure => StatusFail,
                _ => TextSecondary,
            };

            if (string.IsNullOrEmpty(fileName))
            {
                AppendLogLine(detail, color);
            }
            else
            {
                AppendLogLine(fileName, color, detail);
            }
        }

        // ============================================================
        // SHARED TITLE BAR (with language picker)
        // ============================================================

        Panel BuildTitleBar()
        {
            Panel bar = new Panel()
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = BgTitleBar,
            };

            Panel logoDot = new Panel()
            {
                Width = 14,
                Height = 14,
                Left = 16,
                Top = 12,
                BackColor = Amber,
            };

            Label appName = new Label()
            {
                Text = Localization.T("appTitle"),
                AutoSize = true,
                Left = 38,
                Top = 10,
                ForeColor = TextPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            };

            btnLanguage = BuildLanguageButton();
            btnLanguage.Top = 6;

            bar.Resize += (s, e) =>
            {
                btnLanguage.Left = bar.Width - btnLanguage.Width - 16;
            };

            bar.Controls.Add(logoDot);
            bar.Controls.Add(appName);
            bar.Controls.Add(btnLanguage);

            btnLanguage.Left = bar.Width - btnLanguage.Width - 16;

            return bar;
        }

        // ============================================================
        // SCREEN SWITCHING
        // ============================================================

        void ShowMenu()
        {
            menuPanel.Visible = true;
            mainPanel.Visible = false;

            Text = Localization.T("appTitle");

            currentGame = GameKind.None;
            gameRoot = "";
            extension = "";
        }

        void ShowMainUI()
        {
            Text = GetCurrentWindowTitle();

            menuPanel.Visible = false;
            mainPanel.Visible = true;

            lblGameBadgeName.Text = GetCurrentGameDescription();
            lblGameBadgeParser.Text = Localization.T("logParser") + GetCurrentParserName();

            btnImport.Text = currentGame == GameKind.MH4G
                ? Localization.T("importLmd")
                : Localization.T("importGmd");

            // Quest ARC management is MHXX-specific, including bundled archives.
            btnQuests.Visible = currentGame == GameKind.MHXX;

            ShowStandardView(() =>
            {
                ResetStatCards();

                logBox.Clear();

                AppendLogLine(Localization.T("logReady"), null);

                if (pendingWarnings.Count > 0)
                {
                    foreach (string warning in pendingWarnings)
                        AppendLogLine(warning, StatusWarn);

                    pendingWarnings.Clear();
                }

                AppendLogLine("", null);
            });
        }

        // ============================================================
        // GAME SELECTION
        // ============================================================

        void Select3G()
        {
            currentGame = GameKind.MH3G;
            gameRoot = "MH3G";
            extension = "*.gmd";

            PrepareFolders();
            ShowMainUI();
        }

        void Select4G()
        {
            currentGame = GameKind.MH4G;
            gameRoot = "MH4G";
            extension = "*.lmd";

            PrepareFolders();
            ShowMainUI();
        }

        void SelectXX()
        {
            currentGame = GameKind.MHXX;
            gameRoot = "MHXX";
            extension = "*.gmd";

            PrepareFolders();
            ShowMainUI();
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

        // ============================================================
        // TASK EXECUTION (unchanged behavior from the original tool)
        // ============================================================

        void RunTask(TaskMode mode)
        {
            if (currentGame == GameKind.None)
            {
                AppendLogLine(Localization.T("logNoGameSelected"), StatusFail);
                return;
            }

            string originalDir = Path.Combine(gameRoot, "original");
            string txtDir = Path.Combine(gameRoot, "txt");
            string outputDir = Path.Combine(gameRoot, "output");
            string backupDir = Path.Combine(gameRoot, "backup");
            string logDir = Path.Combine(gameRoot, "logs");

            Directory.CreateDirectory(originalDir);
            Directory.CreateDirectory(txtDir);
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(backupDir);
            Directory.CreateDirectory(logDir);

            string[] files = Directory
                .GetFiles(originalDir, "*", SearchOption.AllDirectories)
                .Where(file => Path.GetExtension(file).Equals(extension.TrimStart('*'), StringComparison.OrdinalIgnoreCase) ||
                    (currentGame == GameKind.MH3G && Path.GetExtension(file).Equals(".arc", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(Path.GetFileName)
                .ToArray();

            SetStatValue(lblStatTotalValue, files.Length);

            if (files.Length == 0)
            {
                AppendLogLine(Localization.T("logNoFilesFound") + extension, StatusWarn);
                return;
            }

            AppendLogLine(Localization.T("logTaskLabel") + GetTaskName(mode), null);
            AppendLogLine(Localization.T("logParser") + GetCurrentParserName(), null);
            AppendLogLine(Localization.T("logFilesFound") + files.Length, null);
            AppendLogLine("", null);

            int ok = 0;
            int skipped = 0;
            int failed = 0;

            foreach (string file in files)
            {
                string name = Path.GetRelativePath(originalDir, file);

                string txt = Path.Combine(txtDir, Path.GetExtension(name).Equals(".arc", StringComparison.OrdinalIgnoreCase)
                    ? name + ".texts" : Path.ChangeExtension(name, ".txt"));
                string outFile = Path.Combine(outputDir, name);
                string backup = Path.Combine(backupDir, name);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(txt)!);
                    Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    if (mode == TaskMode.Export)
                    {
                        RunExport(file, txt);

                        ok++;
                        SetStatValue(lblStatSuccessValue, ok);
                        AppendLogLine(name, StatusOk, Localization.T("logTaskExport"));
                    }
                    else
                    {
                        if (!File.Exists(txt) && !Directory.Exists(txt))
                        {
                            skipped++;
                            SetStatValue(lblStatSkippedValue, skipped);
                            AppendLogLine(name, StatusWarn, Localization.T("logSkippedSuffix"));
                            continue;
                        }

                        if (mode == TaskMode.Verify)
                        {
                            RunVerify(file, txt);

                            ok++;
                            SetStatValue(lblStatSuccessValue, ok);
                            AppendLogLine(name, StatusOk, Localization.T("logTaskVerify"));
                        }
                        else if (mode == TaskMode.Import)
                        {
                            /*
                                Verify before rebuilding.

                                For MHXX:
                                - MHXXGMDParser keeps the original names/keys;
                                - only the text pool is rebuilt;
                                - if TXT is incomplete or mismatched, no output is generated.
                            */
                            RunVerify(file, txt);

                            if (!File.Exists(backup))
                                File.Copy(file, backup, false);

                            RunImport(file, txt, outFile);

                            ok++;
                            SetStatValue(lblStatSuccessValue, ok);
                            AppendLogLine(name, StatusOk, Localization.T("logTaskImport"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    SetStatValue(lblStatFailedValue, failed);

                    string logPath = Path.Combine(logDir, "errors.log");

                    File.AppendAllText(
                        logPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        + " - "
                        + name
                        + " -> "
                        + ex
                        + Environment.NewLine
                    );

                    AppendLogLine(name, StatusFail, ex.Message);
                }
            }

            AppendLogLine("", null);
            AppendLogLine(Localization.T("logFinished"), null);
        }

        string GetTaskName(TaskMode mode)
        {
            switch (mode)
            {
                case TaskMode.Export: return Localization.T("taskNameExport");
                case TaskMode.Import: return Localization.T("taskNameImport");
                case TaskMode.Verify: return Localization.T("taskNameVerify");
                default: return mode.ToString();
            }
        }

        void RunExport(string sourceFile, string txtFile)
        {
            if (currentGame == GameKind.MH3G && Path.GetExtension(sourceFile).Equals(".arc", StringComparison.OrdinalIgnoreCase))
            { MH3UArcWorkflow.Run(sourceFile, txtFile, null); return; }
            switch (currentGame)
            {
                case GameKind.MH3G:
                    GMDParser.ExportToTxt(sourceFile, txtFile);
                    break;

                case GameKind.MH4G:
                    LMDParser.ExportToTxt(sourceFile, txtFile);
                    break;

                case GameKind.MHXX:
                    /*
                        IMPORTANT:
                        MHXX must NEVER use the generic GMDParser.
                    */
                    MHXXGMDParser.ExportToTxt(sourceFile, txtFile);
                    break;

                default:
                    throw new Exception("No parser selected.");
            }
        }

        void RunVerify(string sourceFile, string txtFile)
        {
            if (currentGame == GameKind.MH3G && Path.GetExtension(sourceFile).Equals(".arc", StringComparison.OrdinalIgnoreCase))
            { MH3UArcWorkflow.Run(sourceFile, txtFile, null, true); return; }
            switch (currentGame)
            {
                case GameKind.MH3G:
                    GMDParser.Verify(sourceFile, txtFile);
                    break;

                case GameKind.MH4G:
                    LMDParser.Verify(sourceFile, txtFile);
                    break;

                case GameKind.MHXX:
                    /*
                        IMPORTANT:
                        MHXX must NEVER use the generic GMDParser.
                    */
                    MHXXGMDParser.Verify(sourceFile, txtFile);
                    break;

                default:
                    throw new Exception("No parser selected.");
            }
        }

        void RunImport(string sourceFile, string txtFile, string outputFile)
        {
            if (currentGame == GameKind.MH3G && Path.GetExtension(sourceFile).Equals(".arc", StringComparison.OrdinalIgnoreCase))
            { MH3UArcWorkflow.Run(sourceFile, txtFile, outputFile); return; }
            switch (currentGame)
            {
                case GameKind.MH3G:
                    GMDParser.ImportFromTxt(sourceFile, txtFile, outputFile);
                    break;

                case GameKind.MH4G:
                    LMDParser.ImportFromTxt(sourceFile, txtFile, outputFile);
                    break;

                case GameKind.MHXX:
                    /*
                        IMPORTANT:
                        MHXX must NEVER use the generic GMDParser.
                    */
                    MHXXGMDParser.ImportFromTxt(sourceFile, txtFile, outputFile);
                    break;

                default:
                    throw new Exception("No parser selected.");
            }
        }

        string GetCurrentWindowTitle()
        {
            switch (currentGame)
            {
                case GameKind.MH3G:
                    return Localization.T("windowTitle3G");

                case GameKind.MH4G:
                    return Localization.T("windowTitle4G");

                case GameKind.MHXX:
                    return Localization.T("windowTitleXX");

                default:
                    return Localization.T("appTitle");
            }
        }

        string GetCurrentGameDescription()
        {
            switch (currentGame)
            {
                case GameKind.MH3G:
                    return Localization.T("gameDesc3G");

                case GameKind.MH4G:
                    return Localization.T("gameDesc4G");

                case GameKind.MHXX:
                    return Localization.T("gameDescXX");

                default:
                    return Localization.T("gameDescNone");
            }
        }

        string GetCurrentParserName()
        {
            switch (currentGame)
            {
                case GameKind.MH3G:
                    return "GMDParser";

                case GameKind.MH4G:
                    return "LMDParser";

                case GameKind.MHXX:
                    return "MHXXGMDParser";

                default:
                    return "None";
            }
        }

        void OpenCurrentGameFolder()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(gameRoot))
                    return;

                PrepareFolders();

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = Path.GetFullPath(gameRoot),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppendLogLine(Localization.T("logCouldNotOpenFolder") + ex.Message, StatusFail);
            }
        }

        // ============================================================
        // STAT CARD HELPERS
        // ============================================================

        void SetStatValue(Label label, int value)
        {
            label.Text = value.ToString();
        }

        // ============================================================
        // COLORED LOG
        // ============================================================

        /*
            Appends one line to the log. When 'status' is null, the line is
            written as plain secondary-colored text (headers, blank lines,
            summary text). When 'status' is provided, the line is rendered
            as:

                <icon> fileName                              suffix

            with the icon and suffix colored according to status
            (success/failure/warning), mirroring the original log's
            ✔ / ❌ / ⚠ markers but with color instead of relying purely on
            the emoji glyph.
        */
        void AppendLogLine(string text, Color? status, string? suffix = null)
        {
            RichTextBox? target = showingQuestsView ? questsLogBox : logBox;

            if (target == null)
                return;

            target.SelectionStart = target.TextLength;
            target.SelectionLength = 0;

            if (status == null)
            {
                target.SelectionColor = TextSecondary;
                target.AppendText(text + Environment.NewLine);
                return;
            }

            string icon = status == StatusOk ? "✔ "
                : status == StatusFail ? "✕ "
                : status == StatusWarn ? "⚠ "
                : "• ";

            target.SelectionColor = status.Value;
            target.AppendText(icon);

            target.SelectionColor = TextPrimary;
            target.AppendText(text);

            if (!string.IsNullOrEmpty(suffix))
            {
                int padding = Math.Max(1, 50 - text.Length - icon.Length);
                target.SelectionColor = TextMuted;
                target.AppendText(new string(' ', padding) + suffix);
            }

            target.AppendText(Environment.NewLine);
            target.SelectionStart = target.TextLength;
            target.ScrollToCaret();
        }

        // ============================================================
        // VISUAL HELPERS
        // ============================================================

        Color BlendColor(Color from, Color to, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));

            int r = (int)(from.R + (to.R - from.R) * amount);
            int g = (int)(from.G + (to.G - from.G) * amount);
            int b = (int)(from.B + (to.B - from.B) * amount);

            return Color.FromArgb(r, g, b);
        }

        void ConfigureButtonHover(Button btn, Color normalBack, Color hoverBack, Color pressedBack)
        {
            Color normalBorder = btn.FlatAppearance.BorderColor;
            Color hoverBorder = BlendColor(hoverBack, TextSecondary, 0.28);

            btn.FlatAppearance.MouseOverBackColor = hoverBack;
            btn.FlatAppearance.MouseDownBackColor = pressedBack;

            btn.MouseEnter += (s, e) =>
            {
                btn.BackColor = hoverBack;

                if (btn.FlatAppearance.BorderSize > 0)
                    btn.FlatAppearance.BorderColor = hoverBorder;
            };

            btn.MouseLeave += (s, e) =>
            {
                btn.BackColor = normalBack;

                if (btn.FlatAppearance.BorderSize > 0)
                    btn.FlatAppearance.BorderColor = normalBorder;
            };

            btn.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    btn.BackColor = pressedBack;
            };

            btn.MouseUp += (s, e) =>
            {
                Point p = btn.PointToClient(Cursor.Position);
                bool stillHovering = btn.ClientRectangle.Contains(p);
                btn.BackColor = stillHovering ? hoverBack : normalBack;

                if (btn.FlatAppearance.BorderSize > 0)
                    btn.FlatAppearance.BorderColor = stillHovering ? hoverBorder : normalBorder;
            };
        }

        void MakeRoundedPanel(Panel panel, int radius)
        {
            panel.Paint += (s, e) =>
            {
                using (var path = RoundedRectPath(panel.ClientRectangle, radius))
                using (var brush = new SolidBrush(panel.BackColor))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                }
            };

            panel.Resize += (s, e) => panel.Invalidate();
        }

        System.Drawing.Drawing2D.GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        Image? LoadIconImage(string resourceFileName, Size size)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                string? resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("." + resourceFileName, StringComparison.OrdinalIgnoreCase));

                if (resourceName == null)
                {
                    pendingWarnings.Add(Localization.T("logIconNotFound") + resourceFileName);
                    return null;
                }

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var icon = new Icon(stream!, size))
                {
                    return icon.ToBitmap();
                }
            }
            catch (Exception ex)
            {
                pendingWarnings.Add(Localization.T("logIconLoadFailed") + resourceFileName + "': " + ex.Message);
                return null;
            }
        }
    }
}