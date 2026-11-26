using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Imazen.WebP;

internal sealed class LauncherConfiguration
{
    public string Name { get; set; }
    public string ShortcutPath { get; set; }
    public string Target { get; set; }
    public string Arguments { get; set; }
    public string WorkingDirectory { get; set; }
    public string IconLocation { get; set; }
    public string BackgroundPath { get; set; }
}

internal static class LauncherPaths
{
    public static string ProfilesDirectory
    {
        get { return Path.Combine(Application.StartupPath, "Profiles"); }
    }

    public static string IconsDirectory
    {
        get { return Path.Combine(Application.StartupPath, "Icons"); }
    }

    public static string LibrariesDirectory
    {
        get { return Path.Combine(Application.StartupPath, "Libraries"); }
    }

    public static string FontsDirectory
    {
        get { return Path.Combine(Application.StartupPath, "Fonts"); }
    }

    public static string BackgroundsDirectory
    {
        get { return Path.Combine(Application.StartupPath, "Backgrounds"); }
    }

    public static string ImagesDirectory
    {
        get { return Path.Combine(Application.StartupPath, "Images"); }
    }
}

internal static class Program
{
    private static readonly Regex ShortcutName = new Regex(
        @"^SC4 \((.+)\)\.lnk$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [STAThread]
    private static int Main(string[] args)
    {
        ConfigureLibraryLoading();

        bool verifyOnly = Array.Exists(args, delegate(string argument)
        {
            return string.Equals(argument, "--verify", StringComparison.OrdinalIgnoreCase);
        });

        List<LauncherConfiguration> configurations;
        try
        {
            configurations = LoadConfigurations();
            ProfileBackgroundStore.Load(configurations);
        }
        catch (Exception exception)
        {
            if (verifyOnly)
            {
                return 1;
            }

            MessageBox.Show(
                exception.Message,
                "SimCity 4 Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (verifyOnly)
        {
            return VerifyConfigurations(configurations);
        }

        Application.Run(new LauncherForm(configurations, true));
        return 0;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string path);

    private static void ConfigureLibraryLoading()
    {
        string libraryDirectory = LauncherPaths.LibrariesDirectory;
        if (Directory.Exists(libraryDirectory))
        {
            SetDllDirectory(libraryDirectory);
        }

        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs eventArgs)
        {
            string assemblyName = new AssemblyName(eventArgs.Name).Name + ".dll";
            string assemblyPath = Path.Combine(libraryDirectory, assemblyName);
            return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
        };
    }

    private static List<LauncherConfiguration> LoadConfigurations()
    {
        string directory = LauncherPaths.ProfilesDirectory;
        Directory.CreateDirectory(directory);
        string[] files = Directory.GetFiles(directory, "*.lnk");
        List<LauncherConfiguration> configurations = new List<LauncherConfiguration>();
        if (files.Length == 0)
        {
            return configurations;
        }

        Type shellType = Type.GetTypeFromProgID("WScript.Shell");

        if (shellType == null)
        {
            throw new InvalidOperationException("No se pudo acceder al servicio de accesos directos de Windows.");
        }

        object shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            foreach (string file in files)
            {
                Match match = ShortcutName.Match(Path.GetFileName(file));
                if (!match.Success)
                {
                    continue;
                }

                object shortcut = null;
                try
                {
                    shortcut = shellType.InvokeMember(
                        "CreateShortcut",
                        BindingFlags.InvokeMethod,
                        null,
                        shell,
                        new object[] { file });

                    configurations.Add(new LauncherConfiguration
                    {
                        Name = match.Groups[1].Value,
                        ShortcutPath = file,
                        Target = ReadShortcutProperty(shortcut, "TargetPath"),
                        Arguments = ReadShortcutProperty(shortcut, "Arguments"),
                        WorkingDirectory = ReadShortcutProperty(shortcut, "WorkingDirectory"),
                        IconLocation = ReadShortcutProperty(shortcut, "IconLocation")
                    });
                }
                finally
                {
                    ReleaseComObject(shortcut);
                }
            }
        }
        finally
        {
            ReleaseComObject(shell);
        }

        configurations.Sort(delegate(LauncherConfiguration first, LauncherConfiguration second)
        {
            return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
        });

        return configurations;
    }

    private static string ReadShortcutProperty(object shortcut, string propertyName)
    {
        object value = shortcut.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            null,
            shortcut,
            null);
        return value == null ? string.Empty : value.ToString();
    }

    internal static void ReleaseComObject(object instance)
    {
        if (instance != null && Marshal.IsComObject(instance))
        {
            Marshal.ReleaseComObject(instance);
        }
    }

    private static int VerifyConfigurations(List<LauncherConfiguration> configurations)
    {
        try
        {
            using (LauncherForm emptyProfileForm = new LauncherForm(
                new List<LauncherConfiguration>(),
                false))
            {
                if (!emptyProfileForm.EmptyProfileStateIsValid)
                {
                    return 12;
                }
            }
        }
        catch
        {
            return 12;
        }

        if (configurations.Count == 0)
        {
            return 0;
        }

        foreach (LauncherConfiguration configuration in configurations)
        {
            if (string.IsNullOrEmpty(configuration.Target) || !File.Exists(configuration.Target))
            {
                return 3;
            }

            if (!string.IsNullOrEmpty(configuration.WorkingDirectory) &&
                !Directory.Exists(configuration.WorkingDirectory))
            {
                return 4;
            }
        }

        if (!LauncherForm.ProfileBackgroundsAreValid(configurations))
        {
            return 7;
        }

        try
        {
            bool profileEditorLayoutValid;
            using (ProfileEditorForm editor = new ProfileEditorForm(string.Empty, string.Empty, string.Empty))
            {
                profileEditorLayoutValid = editor.ButtonLayoutIsValid;
            }

            using (LauncherForm form = new LauncherForm(configurations, false))
            {
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-2000, -2000);
                form.Show();
                Application.DoEvents();

                bool selectedFirstConfiguration = form.SelectedConfigurationName == configurations[0].Name;
                bool profileAppearanceValid = form.ProfileAppearanceIsValid;
                bool titleBarButtonsValid = form.TitleBarButtonsAreValid;
                bool profileListAppearanceValid = form.ProfileListAppearanceIsValid;
                bool profileSelectionRedrawIsScoped = form.ProfileSelectionRedrawIsScoped;
                form.Close();
                Application.DoEvents();

                return selectedFirstConfiguration
                    ? (profileAppearanceValid
                        ? (titleBarButtonsValid
                            ? (profileListAppearanceValid
                                ? (profileSelectionRedrawIsScoped
                                    ? (profileEditorLayoutValid ? 0 : 10)
                                    : 11)
                                : 9)
                            : 8)
                        : 6)
                    : 5;
            }
        }
        catch
        {
            return 5;
        }
    }
}

internal static class LastProfileStore
{
    private static string GetFilePath()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localApplicationData))
        {
            return null;
        }

        return Path.Combine(localApplicationData, "SimCity 4 Launcher", "last-profile.txt");
    }

    public static string Load()
    {
        try
        {
            string path = GetFilePath();
            return string.IsNullOrEmpty(path) || !File.Exists(path)
                ? null
                : File.ReadAllText(path).Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Save(string profileName)
    {
        if (string.IsNullOrEmpty(profileName))
        {
            return;
        }

        try
        {
            string path = GetFilePath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, profileName);
        }
        catch (Exception)
        {
        }
    }
}

internal static class ProfileBackgroundStore
{
    private static string GetFilePath()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localApplicationData))
        {
            return null;
        }

        return Path.Combine(localApplicationData, "SimCity 4 Launcher", "profile-backgrounds.txt");
    }

    public static void Load(List<LauncherConfiguration> configurations)
    {
        try
        {
            string path = GetFilePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            bool pathsUpdated = false;
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                try
                {
                    string[] values = line.Split('\t');
                    if (values.Length != 2)
                    {
                        continue;
                    }

                    string profileName = Encoding.UTF8.GetString(Convert.FromBase64String(values[0]));
                    string backgroundPath = Encoding.UTF8.GetString(Convert.FromBase64String(values[1]));
                    if (!File.Exists(backgroundPath))
                    {
                        string backgroundFileName = Path.GetFileName(backgroundPath);
                        if (!string.IsNullOrEmpty(backgroundFileName))
                        {
                            string relocatedPath = Path.Combine(
                                LauncherPaths.BackgroundsDirectory,
                                backgroundFileName);
                            if (File.Exists(relocatedPath))
                            {
                                backgroundPath = relocatedPath;
                                pathsUpdated = true;
                            }
                        }
                    }

                    LauncherConfiguration profile = configurations.Find(delegate(LauncherConfiguration configuration)
                    {
                        return string.Equals(configuration.Name, profileName, StringComparison.OrdinalIgnoreCase);
                    });
                    if (profile != null)
                    {
                        profile.BackgroundPath = backgroundPath;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (pathsUpdated)
            {
                Save(configurations);
            }
        }
        catch (Exception)
        {
        }
    }

    public static void Save(List<LauncherConfiguration> configurations)
    {
        string path = GetFilePath();
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("No se pudo acceder a la carpeta de datos del usuario.");
        }

        List<string> lines = new List<string>();
        foreach (LauncherConfiguration profile in configurations)
        {
            if (string.IsNullOrWhiteSpace(profile.BackgroundPath))
            {
                continue;
            }

            string encodedName = Convert.ToBase64String(Encoding.UTF8.GetBytes(profile.Name));
            string encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(profile.BackgroundPath));
            lines.Add(encodedName + "\t" + encodedPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllLines(path, lines.ToArray(), Encoding.UTF8);
    }
}

internal static class LauncherFonts
{
    private static readonly PrivateFontCollection FontCollection = new PrivateFontCollection();
    private static FontFamily family;

    public static FontFamily DisplayFamily
    {
        get
        {
            if (family != null)
            {
                return family;
            }

            string fontPath = Path.Combine(LauncherPaths.FontsDirectory, "Orbitron-variable.ttf");
            try
            {
                if (File.Exists(fontPath))
                {
                    FontCollection.AddFontFile(fontPath);
                }

                foreach (FontFamily fontFamily in FontCollection.Families)
                {
                    if (string.Equals(fontFamily.Name, "Orbitron", StringComparison.OrdinalIgnoreCase))
                    {
                        family = fontFamily;
                        return family;
                    }
                }
            }
            catch
            {
            }

            family = new FontFamily("Trebuchet MS");
            return family;
        }
    }
}

internal static class BootstrapIcons
{
    private static readonly Dictionary<string, Image> icons = new Dictionary<string, Image>();

    public static Image Load(string name, Color color, int size)
    {
        string key = name + "|" + color.ToArgb() + "|" + size;
        Image icon;
        if (icons.TryGetValue(key, out icon))
        {
            return icon;
        }

        string path = Path.Combine(LauncherPaths.IconsDirectory, "Bootstrap Icon " + name + ".png");
        if (!File.Exists(path) || size <= 0)
        {
            return null;
        }

        Bitmap tintedIcon = null;
        try
        {
            using (Image source = Image.FromFile(path))
            {
                tintedIcon = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(tintedIcon))
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    graphics.Clear(Color.Transparent);
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                    {
                        new float[] { 0F, 0F, 0F, 0F, 0F },
                        new float[] { 0F, 0F, 0F, 0F, 0F },
                        new float[] { 0F, 0F, 0F, 0F, 0F },
                        new float[] { 0F, 0F, 0F, 1F, 0F },
                        new float[] { color.R / 255F, color.G / 255F, color.B / 255F, 0F, 1F }
                    });
                    attributes.SetColorMatrix(colorMatrix);
                    graphics.DrawImage(
                        source,
                        new Rectangle(0, 0, size, size),
                        0,
                        0,
                        source.Width,
                        source.Height,
                        GraphicsUnit.Pixel,
                        attributes);
                }
            }

            icons.Add(key, tintedIcon);
            return tintedIcon;
        }
        catch
        {
            if (tintedIcon != null)
            {
                tintedIcon.Dispose();
            }

            return null;
        }
    }

    public static void SetButtonIcon(Button button, string name, Color color, int size)
    {
        Image image = Load(name, color, size);
        if (image == null)
        {
            return;
        }

        button.Image = image;
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Padding = new Padding(6, 0, 6, 0);
    }
}

internal sealed class ProfileBackgroundPanel : Panel
{
    private string profileName = string.Empty;
    private Rectangle profileNameBounds;
    private Font profileNameFont;

    public ProfileBackgroundPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public string ProfileName
    {
        get { return profileName; }
        set
        {
            profileName = value ?? string.Empty;
            Invalidate(profileNameBounds);
        }
    }

    public Rectangle ProfileNameBounds
    {
        get { return profileNameBounds; }
        set
        {
            profileNameBounds = value;
            Invalidate();
        }
    }

    public Font ProfileNameFont
    {
        set
        {
            if (profileNameFont != null)
            {
                profileNameFont.Dispose();
            }

            profileNameFont = value;
            Invalidate(profileNameBounds);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        Image image = BackgroundImage;
        if (image == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        float scale = Math.Max(
            (float)ClientSize.Width / image.Width,
            (float)ClientSize.Height / image.Height);
        int width = (int)Math.Ceiling(image.Width * scale);
        int height = (int)Math.Ceiling(image.Height * scale);
        Rectangle destination = new Rectangle(
            (ClientSize.Width - width) / 2,
            (ClientSize.Height - height) / 2,
            width,
            height);

        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(image, destination, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (profileName.Length > 0 && profileNameFont != null)
        {
            TextRenderer.DrawText(
                e.Graphics,
                profileName,
                profileNameFont,
                profileNameBounds,
                Color.FromArgb(235, 241, 243),
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && profileNameFont != null)
        {
            profileNameFont.Dispose();
            profileNameFont = null;
        }

        base.Dispose(disposing);
    }
}

internal sealed class LauncherForm : Form
{
    private const int WmNcHitTest = 0x84;
    private const int WmNcLeftButtonDown = 0xA1;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int ProfileActionHeight = 64;
    private const int ProfileActionButtonWidth = 260;
    private const int ProfileActionsIndicatorWidth = 38;
    private const int AddProfileButtonHeight = 42;
    private const int SelectedProfileIndicatorWidth = 3;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    private static readonly Color BackgroundColor = Color.FromArgb(16, 24, 31);
    private static readonly Color HeaderColor = Color.FromArgb(20, 31, 40);
    private static readonly Color SidebarColor = Color.FromArgb(23, 34, 43);
    private static readonly Color PanelColor = Color.FromArgb(25, 38, 48);
    private static readonly Color SelectedColor = Color.FromArgb(37, 56, 68);
    private static readonly Color AccentColor = Color.FromArgb(157, 88, 128);
    private static readonly Color AddProfileHoverColor = Color.FromArgb(177, 109, 144);
    private static readonly Color AddProfilePressedColor = Color.FromArgb(127, 64, 100);
    private static readonly Color MainTextColor = Color.FromArgb(235, 241, 243);
    private static readonly Color MutedTextColor = Color.FromArgb(154, 171, 180);

    private readonly List<LauncherConfiguration> configurations;
    private readonly ListBox configurationList;
    private readonly VScrollBar profileScrollBar;
    private readonly Font selectedProfileFont = new Font("Trebuchet MS", 12.5F, FontStyle.Bold);
    private readonly Font profileActionsGlyphFont = new Font("Segoe UI Symbol", 16F, FontStyle.Bold);
    private readonly ProfileBackgroundPanel detailsPanel;
    private readonly Button launchButton;
    private readonly Button addProfileButton;
    private readonly ContextMenuStrip profileActionsMenu;
    private readonly ToolStripMenuItem editProfileMenuItem;
    private readonly ToolStripMenuItem deleteProfileMenuItem;
    private readonly ProfileActionsColorTable profileActionsColorTable;
    private readonly ToolStripProfessionalRenderer profileActionsRenderer;
    private int hoveredProfileIndex = -1;
    private int selectedProfileIndex = -1;
    private int[] lastProfileSelectionInvalidatedIndices = new int[0];
    private Button maximizeWindowButton;
    private readonly List<Button> titleBarButtons = new List<Button>();
    private readonly bool rememberLastProfile;

    public LauncherForm(List<LauncherConfiguration> configurations, bool rememberLastProfile)
    {
        this.configurations = configurations;
        this.rememberLastProfile = rememberLastProfile;
        Text = "SimCity 4 Launcher";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 550);
        Size = new Size(960, 650);
        BackColor = BackgroundColor;
        ForeColor = MainTextColor;
        Font = new Font("Trebuchet MS", 12F);

        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.BackColor = BackgroundColor;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Controls.Add(layout);

        Panel titleBar = CreateCustomTitleBar();
        Panel body = new Panel();
        body.Dock = DockStyle.Fill;
        body.BackColor = BackgroundColor;

        layout.Controls.Add(titleBar, 0, 0);
        layout.Controls.Add(body, 0, 1);

        Panel sidebar = CreateSidebar();
        ProfileBackgroundPanel details = CreateDetails();
        body.Controls.Add(details);
        body.Controls.Add(sidebar);
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = 292;
        details.Dock = DockStyle.Fill;

        configurationList = FindConfigurationList(sidebar);
        profileScrollBar = FindProfileScrollBar(sidebar);
        detailsPanel = details;
        launchButton = FindButton(details, "LaunchButton");
        addProfileButton = FindButton(sidebar, "AddProfileButton");
        profileActionsMenu = new ContextMenuStrip();
        profileActionsColorTable = new ProfileActionsColorTable(PanelColor, SelectedColor, SelectedColor);
        profileActionsRenderer = new ToolStripProfessionalRenderer(profileActionsColorTable);
        profileActionsMenu.Renderer = profileActionsRenderer;
        profileActionsMenu.ShowImageMargin = true;
        profileActionsMenu.ShowCheckMargin = false;
        profileActionsMenu.ImageScalingSize = new Size(16, 16);
        profileActionsMenu.Padding = new Padding(2);
        profileActionsMenu.BackColor = PanelColor;
        profileActionsMenu.ForeColor = MainTextColor;
        profileActionsMenu.Font = new Font("Trebuchet MS", 11.5F);
        editProfileMenuItem = new ToolStripMenuItem("Editar perfil");
        deleteProfileMenuItem = new ToolStripMenuItem("Eliminar perfil");
        editProfileMenuItem.Padding = new Padding(8, 4, 12, 4);
        deleteProfileMenuItem.Padding = new Padding(8, 4, 12, 4);
        editProfileMenuItem.ImageAlign = ContentAlignment.MiddleCenter;
        deleteProfileMenuItem.ImageAlign = ContentAlignment.MiddleCenter;
        editProfileMenuItem.Image = BootstrapIcons.Load("pencil-fill", MainTextColor, 16);
        deleteProfileMenuItem.Image = BootstrapIcons.Load("trash3-fill", MainTextColor, 16);
        editProfileMenuItem.Click += OnEditProfileClicked;
        deleteProfileMenuItem.Click += OnDeleteProfileClicked;
        profileActionsMenu.Items.Add(editProfileMenuItem);
        profileActionsMenu.Items.Add(deleteProfileMenuItem);
        launchButton.Enabled = configurations.Count > 0;
        editProfileMenuItem.Enabled = configurations.Count > 0;
        deleteProfileMenuItem.Enabled = configurations.Count > 1;

        foreach (LauncherConfiguration configuration in configurations)
        {
            configurationList.Items.Add(configuration.Name);
        }

        configurationList.SelectedIndexChanged += OnSelectionChanged;
        configurationList.DrawItem += OnDrawConfiguration;
        configurationList.MouseDoubleClick += OnProfileListMouseDoubleClick;
        configurationList.MouseMove += OnProfileListMouseMove;
        configurationList.MouseLeave += OnProfileListMouseLeave;
        configurationList.MouseUp += OnProfileListMouseUp;
        configurationList.MouseWheel += OnProfileListScrolled;
        configurationList.KeyUp += OnProfileListScrolled;
        configurationList.Resize += delegate { UpdateProfileScrollBar(); };
        profileScrollBar.ValueChanged += OnProfileScrollChanged;
        launchButton.Click += OnLaunchClicked;
        addProfileButton.Click += OnAddProfileClicked;
        details.Resize += delegate
        {
            LayoutProfileDetails();
        };
        LayoutProfileDetails();
        FormClosed += delegate
        {
            selectedProfileFont.Dispose();
            profileActionsGlyphFont.Dispose();
            profileActionsMenu.Dispose();
            SetProfileBackground(null);
        };
        Resize += OnWindowResized;
        AcceptButton = launchButton;

        LoadWindowIcon();
        if (configurationList.Items.Count > 0)
        {
            string lastProfile = rememberLastProfile ? LastProfileStore.Load() : null;
            int selectedIndex = configurations.FindIndex(delegate(LauncherConfiguration configuration)
            {
                return string.Equals(
                    configuration.Name,
                    lastProfile,
                    StringComparison.OrdinalIgnoreCase);
            });
            configurationList.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }
        UpdateProfileScrollBar();
        if (rememberLastProfile)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    public string SelectedConfigurationName
    {
        get { return configurationList.SelectedItem as string; }
    }

    public bool EmptyProfileStateIsValid
    {
        get
        {
            return configurations.Count == 0 &&
                configurationList.Items.Count == 0 &&
                configurationList.SelectedIndex == -1 &&
                addProfileButton.Enabled &&
                !launchButton.Enabled &&
                !editProfileMenuItem.Enabled &&
                !deleteProfileMenuItem.Enabled;
        }
    }

    public bool TitleBarButtonsAreValid
    {
        get
        {
            if (titleBarButtons.Count != 3)
            {
                return false;
            }

            foreach (Button button in titleBarButtons)
            {
                if (button.Width != button.Height ||
                    button.ImageAlign != ContentAlignment.MiddleCenter ||
                    button.TextAlign != ContentAlignment.MiddleCenter ||
                    button.Padding != Padding.Empty ||
                    button.Top != (button.Parent.ClientSize.Height - button.Height) / 2)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public bool ProfileAppearanceIsValid
    {
        get
        {
            return detailsPanel.ProfileName == SelectedConfigurationName &&
                detailsPanel.ProfileNameBounds.Top == launchButton.Top &&
                detailsPanel.ProfileNameBounds.Height == launchButton.Height &&
                detailsPanel.ProfileNameBounds.Height == ProfileActionHeight &&
                launchButton.Width == Math.Min(
                    ProfileActionButtonWidth,
                    Math.Max(1, detailsPanel.ProfileNameBounds.Width - 48)) &&
                launchButton.Width < detailsPanel.ProfileNameBounds.Width &&
                launchButton.Image != null &&
                addProfileButton.Image != null &&
                addProfileButton.Text == "Nuevo perfil" &&
                addProfileButton.BackColor == AccentColor &&
                addProfileButton.ForeColor == MainTextColor &&
                addProfileButton.Font.Bold &&
                addProfileButton.Height == AddProfileButtonHeight &&
                addProfileButton.FlatAppearance.MouseOverBackColor == AddProfileHoverColor &&
                addProfileButton.FlatAppearance.MouseDownBackColor == AddProfilePressedColor &&
                maximizeWindowButton.Image != null &&
                editProfileMenuItem.Image != null &&
                deleteProfileMenuItem.Image != null &&
                BootstrapIcons.Load("three-dots-vertical", MutedTextColor, 16) != null;
        }
    }

    public bool ProfileListAppearanceIsValid
    {
        get
        {
            int selectedIndex = configurationList.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= configurationList.Items.Count)
            {
                return false;
            }

            Rectangle itemBounds = configurationList.GetItemRectangle(selectedIndex);
            Rectangle indicatorBounds = GetSelectedProfileIndicatorBounds(itemBounds);
            Rectangle actionsBounds = GetProfileActionsIndicatorBounds(itemBounds);
            Point actionsCenter = new Point(
                actionsBounds.Left + actionsBounds.Width / 2,
                actionsBounds.Top + actionsBounds.Height / 2);
            Point profileTextPoint = new Point(actionsBounds.Left - 1, actionsCenter.Y);
            return ReferenceEquals(profileActionsMenu.Renderer, profileActionsRenderer) &&
                profileActionsMenu.ShowImageMargin &&
                profileActionsMenu.ImageScalingSize == new Size(16, 16) &&
                configurationList.Right <= profileScrollBar.Left &&
                editProfileMenuItem.ImageAlign == ContentAlignment.MiddleCenter &&
                deleteProfileMenuItem.ImageAlign == ContentAlignment.MiddleCenter &&
                profileActionsMenu.BackColor == PanelColor &&
                profileActionsColorTable.ToolStripDropDownBackground == PanelColor &&
                profileActionsColorTable.ImageMarginGradientBegin == PanelColor &&
                profileActionsColorTable.ImageMarginGradientMiddle == PanelColor &&
                profileActionsColorTable.ImageMarginGradientEnd == PanelColor &&
                actionsBounds.Right == itemBounds.Right &&
                actionsBounds.Width == ProfileActionsIndicatorWidth &&
                actionsBounds.Height == itemBounds.Height &&
                BootstrapIcons.Load("three-dots-vertical", MutedTextColor, 16) != null &&
                !ShouldDrawProfileActionsIndicator(selectedIndex, -1) &&
                ShouldDrawProfileActionsIndicator(selectedIndex, selectedIndex) &&
                ShouldOpenProfileActionsMenu(MouseButtons.Left, itemBounds, actionsCenter, true) &&
                !ShouldOpenProfileActionsMenu(MouseButtons.Left, itemBounds, actionsCenter, false) &&
                !ShouldOpenProfileActionsMenu(MouseButtons.Left, itemBounds, profileTextPoint, true) &&
                !ShouldOpenProfileActionsMenu(MouseButtons.Right, itemBounds, actionsCenter, true) &&
                indicatorBounds.X == itemBounds.X &&
                indicatorBounds.Y == itemBounds.Y &&
                indicatorBounds.Width == SelectedProfileIndicatorWidth &&
                indicatorBounds.Height == itemBounds.Height;
        }
    }

    public bool ProfileSelectionRedrawIsScoped
    {
        get
        {
            if (configurationList.Items.Count < 2)
            {
                return false;
            }

            int previousIndex = configurationList.SelectedIndex;
            int nextIndex = (previousIndex + 1) % configurationList.Items.Count;
            configurationList.SelectedIndex = nextIndex;
            int[] invalidatedIndices = lastProfileSelectionInvalidatedIndices;
            bool redrawIsScoped = invalidatedIndices.Length == 2 &&
                invalidatedIndices[0] == previousIndex &&
                invalidatedIndices[1] == nextIndex;
            configurationList.SelectedIndex = previousIndex;
            return redrawIsScoped;
        }
    }

    public static bool ProfileBackgroundsAreValid(List<LauncherConfiguration> configurations)
    {
        foreach (LauncherConfiguration configuration in configurations)
        {
            if (string.IsNullOrWhiteSpace(configuration.BackgroundPath))
            {
                continue;
            }

            if (!File.Exists(configuration.BackgroundPath))
            {
                return false;
            }

            try
            {
                using (Image image = LoadProfileBackground(configuration.BackgroundPath))
                {
                    if (image == null || image.Width <= 0 || image.Height <= 0)
                    {
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        return true;
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg != WmNcHitTest || WindowState != FormWindowState.Normal)
        {
            return;
        }

        Point cursor = PointToClient(Cursor.Position);
        int resizeBorder = 8;
        bool left = cursor.X < resizeBorder;
        bool right = cursor.X >= ClientSize.Width - resizeBorder;
        bool top = cursor.Y < resizeBorder;
        bool bottom = cursor.Y >= ClientSize.Height - resizeBorder;

        if (top && left)
        {
            message.Result = (IntPtr)HtTopLeft;
        }
        else if (top && right)
        {
            message.Result = (IntPtr)HtTopRight;
        }
        else if (bottom && left)
        {
            message.Result = (IntPtr)HtBottomLeft;
        }
        else if (bottom && right)
        {
            message.Result = (IntPtr)HtBottomRight;
        }
        else if (left)
        {
            message.Result = (IntPtr)HtLeft;
        }
        else if (right)
        {
            message.Result = (IntPtr)HtRight;
        }
        else if (top)
        {
            message.Result = (IntPtr)HtTop;
        }
        else if (bottom)
        {
            message.Result = (IntPtr)HtBottom;
        }
    }

    private Panel CreateCustomTitleBar()
    {
        Panel titleBar = new Panel();
        titleBar.Dock = DockStyle.Fill;
        titleBar.BackColor = PanelColor;

        Label title = new Label();
        title.Text = "SIMCITY 4 LAUNCHER";
        title.Font = new Font(LauncherFonts.DisplayFamily, 11F, FontStyle.Bold);
        title.ForeColor = MutedTextColor;
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.Location = new Point(18, 0);
        title.Size = new Size(300, 38);
        titleBar.Controls.Add(title);
        AttachTitleBarDrag(titleBar);
        AttachTitleBarDrag(title);

        Button minimizeButton = CreateWindowButton("dash-lg", "—", "Minimizar", HeaderColor);
        Button maximizeButton = CreateWindowButton("square", "□", "Maximizar", HeaderColor);
        Button closeButton = CreateWindowButton("x-lg", "×", "Cerrar", Color.FromArgb(190, 55, 55));
        maximizeWindowButton = maximizeButton;
        titleBarButtons.Add(minimizeButton);
        titleBarButtons.Add(maximizeButton);
        titleBarButtons.Add(closeButton);
        titleBar.Controls.Add(minimizeButton);
        titleBar.Controls.Add(maximizeButton);
        titleBar.Controls.Add(closeButton);

        minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };
        maximizeButton.Click += delegate
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        };
        closeButton.Click += delegate { Close(); };
        titleBar.Resize += delegate
        {
            int buttonTop = (titleBar.ClientSize.Height - closeButton.Height) / 2;
            closeButton.Location = new Point(titleBar.ClientSize.Width - closeButton.Width, buttonTop);
            maximizeButton.Location = new Point(closeButton.Left - maximizeButton.Width, buttonTop);
            minimizeButton.Location = new Point(maximizeButton.Left - minimizeButton.Width, buttonTop);
        };

        return titleBar;
    }

    private static Button CreateWindowButton(string iconName, string fallbackText, string accessibleName, Color hoverColor)
    {
        Button button = new Button();
        button.AccessibleName = accessibleName;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = hoverColor;
        button.BackColor = PanelColor;
        button.ForeColor = MainTextColor;
        button.Font = new Font("Segoe UI Symbol", 11F);
        button.Image = BootstrapIcons.Load(iconName, MainTextColor, 16);
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Padding = Padding.Empty;
        button.Text = button.Image == null ? fallbackText : string.Empty;
        button.Size = new Size(38, 38);
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        return button;
    }

    private void AttachTitleBarDrag(Control control)
    {
        control.MouseDown += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WmNcLeftButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
            }
        };
    }

    private void OnWindowResized(object sender, EventArgs e)
    {
        bool maximized = WindowState == FormWindowState.Maximized;
        string iconName = maximized ? "fullscreen-exit" : "square";
        maximizeWindowButton.Image = BootstrapIcons.Load(iconName, MainTextColor, 16);
        maximizeWindowButton.Text = maximizeWindowButton.Image == null
            ? (maximized ? "❐" : "□")
            : string.Empty;
        maximizeWindowButton.AccessibleName = WindowState == FormWindowState.Maximized
            ? "Restaurar"
            : "Maximizar";
    }

    private Panel CreateSidebar()
    {
        Panel sidebar = new Panel();
        sidebar.BackColor = SidebarColor;
        sidebar.Padding = new Padding(20, 22, 16, 16);

        PictureBox logo = new PictureBox();
        logo.Name = "GameIcon";
        logo.Location = new Point(0, 14);
        logo.Size = new Size(292, 100);
        logo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        logo.SizeMode = PictureBoxSizeMode.Zoom;
        logo.BackColor = SidebarColor;
        string logoPath = Path.Combine(LauncherPaths.ImagesDirectory, "vltqk99kevp81.png");
        if (File.Exists(logoPath))
        {
            using (Image image = Image.FromFile(logoPath))
            {
                logo.Image = new Bitmap(image);
            }
        }
        sidebar.Controls.Add(logo);

        ListBox list = new ListBox();
        list.Name = "ProfileList";
        list.Location = new Point(16, 220);
        list.Size = new Size(244, 340);
        list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        list.BackColor = SidebarColor;
        list.ForeColor = MainTextColor;
        list.BorderStyle = BorderStyle.None;
        list.DrawMode = DrawMode.OwnerDrawFixed;
        list.ItemHeight = 46;
        list.IntegralHeight = false;
        list.Font = new Font("Trebuchet MS", 12F);
        list.HorizontalScrollbar = false;
        list.ScrollAlwaysVisible = false;
        sidebar.Controls.Add(list);

        VScrollBar scrollBar = new VScrollBar();
        scrollBar.Name = "ProfileScrollBar";
        scrollBar.Location = new Point(list.Right, list.Top);
        scrollBar.Size = new Size(16, 340);
        scrollBar.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        scrollBar.BackColor = PanelColor;
        sidebar.Controls.Add(scrollBar);

        Button add = CreateButton("Nuevo perfil", true);
        add.Name = "AddProfileButton";
        BootstrapIcons.SetButtonIcon(add, "plus-lg", MainTextColor, 18);
        add.Location = new Point(16, 174);
        add.Size = new Size(260, AddProfileButtonHeight);
        add.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        add.Font = new Font("Trebuchet MS", 13F, FontStyle.Bold);
        add.Padding = new Padding(12, 0, 12, 0);
        add.FlatAppearance.MouseOverBackColor = AddProfileHoverColor;
        add.FlatAppearance.MouseDownBackColor = AddProfilePressedColor;
        sidebar.Controls.Add(add);

        sidebar.Resize += delegate
        {
            logo.Width = sidebar.ClientSize.Width;
            add.Width = Math.Max(0, sidebar.ClientSize.Width - add.Left - 16);
            list.Width = Math.Max(100, sidebar.ClientSize.Width - list.Left - 32);
            scrollBar.Left = list.Right;
            scrollBar.Top = list.Top;
            list.Height = Math.Max(100, sidebar.ClientSize.Height - list.Top - 16);
            scrollBar.Height = list.Height;
            UpdateProfileScrollBar();
        };

        return sidebar;
    }

    private ProfileBackgroundPanel CreateDetails()
    {
        ProfileBackgroundPanel details = new ProfileBackgroundPanel();
        details.BackColor = BackgroundColor;
        details.ProfileNameFont = new Font(LauncherFonts.DisplayFamily, 32F, FontStyle.Bold);

        Button launch = CreateButton("Jugar", true);
        launch.Name = "LaunchButton";
        BootstrapIcons.SetButtonIcon(launch, "play-fill", MainTextColor, 18);
        launch.Location = new Point(0, 0);
        launch.Size = new Size(340, ProfileActionHeight);
        launch.Font = new Font("Trebuchet MS", 15F, FontStyle.Bold);
        details.Controls.Add(launch);
        return details;
    }

    private static Button CreateButton(string text, bool primary)
    {
        Button button = new Button();
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = primary ? AccentColor : PanelColor;
        button.ForeColor = MainTextColor;
        button.Font = new Font("Trebuchet MS", 12F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        return button;
    }

    private static ListBox FindConfigurationList(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.Name == "ProfileList")
            {
                return (ListBox)control;
            }
        }

        throw new InvalidOperationException("No se pudo crear la lista de perfiles.");
    }

    private static VScrollBar FindProfileScrollBar(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.Name == "ProfileScrollBar")
            {
                return (VScrollBar)control;
            }
        }

        throw new InvalidOperationException("No se pudo crear el scroll de perfiles.");
    }

    private static Button FindButton(Control parent, string name)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.Name == name)
            {
                return (Button)control;
            }
        }

        throw new InvalidOperationException("No se pudo crear el botón " + name + ".");
    }

    private void LayoutProfileDetails()
    {
        int contentWidth = Math.Min(488, Math.Max(1, (detailsPanel.ClientSize.Width - 88) / 2));
        int groupWidth = contentWidth * 2 + 24;
        int groupLeft = Math.Max(32, (detailsPanel.ClientSize.Width - groupWidth) / 2);
        int top = Math.Max(16, detailsPanel.ClientSize.Height - ProfileActionHeight - 40);
        int launchWidth = Math.Min(ProfileActionButtonWidth, Math.Max(1, contentWidth - 48));

        detailsPanel.ProfileNameBounds = new Rectangle(groupLeft, top, contentWidth, ProfileActionHeight);
        launchButton.SetBounds(
            groupLeft + contentWidth + 24 + contentWidth - launchWidth,
            top,
            launchWidth,
            ProfileActionHeight);
    }

    private void OnSelectionChanged(object sender, EventArgs e)
    {
        int previousIndex = selectedProfileIndex;
        int index = configurationList.SelectedIndex;
        selectedProfileIndex = index;
        UpdateProfileScrollBar();
        if (index < 0 || index >= configurations.Count)
        {
            SetProfileBackground(null);
            detailsPanel.ProfileName = string.Empty;
            launchButton.Enabled = false;
            editProfileMenuItem.Enabled = false;
            deleteProfileMenuItem.Enabled = false;
            InvalidateProfileSelectionItems(previousIndex, index);
            return;
        }

        LauncherConfiguration configuration = configurations[index];
        SetProfileBackground(configuration.BackgroundPath);
        editProfileMenuItem.Enabled = true;
        deleteProfileMenuItem.Enabled = configurations.Count > 1;
        if (rememberLastProfile)
        {
            LastProfileStore.Save(configuration.Name);
        }

        detailsPanel.ProfileName = configuration.Name;
        launchButton.Enabled = true;
        InvalidateProfileSelectionItems(previousIndex, index);
    }

    private void InvalidateProfileSelectionItems(int previousIndex, int selectedIndex)
    {
        lastProfileSelectionInvalidatedIndices = GetProfileSelectionInvalidationIndices(
            previousIndex,
            selectedIndex);
        foreach (int index in lastProfileSelectionInvalidatedIndices)
        {
            InvalidateProfileItem(index);
        }
    }

    private static int[] GetProfileSelectionInvalidationIndices(int previousIndex, int selectedIndex)
    {
        if (previousIndex < 0)
        {
            return selectedIndex < 0 ? new int[0] : new[] { selectedIndex };
        }

        if (selectedIndex < 0 || selectedIndex == previousIndex)
        {
            return new[] { previousIndex };
        }

        return new[] { previousIndex, selectedIndex };
    }

    private void SetProfileBackground(string path)
    {
        Bitmap background = null;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                using (Image source = LoadProfileBackground(path))
                {
                    double scale = Math.Min(1.0, Math.Min(1600.0 / source.Width, 1000.0 / source.Height));
                    int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                    int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                    background = new Bitmap(width, height);
                    using (Graphics graphics = Graphics.FromImage(background))
                    {
                        graphics.DrawImage(source, new Rectangle(0, 0, width, height));
                        using (SolidBrush shade = new SolidBrush(Color.FromArgb(70, 16, 24, 31)))
                        {
                            graphics.FillRectangle(shade, 0, 0, width, height);
                        }
                    }
                }
            }
            catch (Exception)
            {
                if (background != null)
                {
                    background.Dispose();
                    background = null;
                }
            }
        }

        Image previousImage = detailsPanel.BackgroundImage;
        detailsPanel.BackgroundImage = background;
        if (previousImage != null)
        {
            previousImage.Dispose();
        }
    }

    private static Image LoadProfileBackground(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase))
        {
            return Image.FromFile(path);
        }

        byte[] imageBytes = File.ReadAllBytes(path);
        SimpleDecoder decoder = new SimpleDecoder();
        return decoder.DecodeFromBytes(imageBytes, imageBytes.LongLength);
    }

    private void OnProfileScrollChanged(object sender, EventArgs e)
    {
        if (configurationList.Items.Count == 0)
        {
            return;
        }

        int visibleItems = Math.Max(1, configurationList.ClientSize.Height / configurationList.ItemHeight);
        int maximumTopIndex = Math.Max(0, configurationList.Items.Count - visibleItems);
        configurationList.TopIndex = Math.Min(profileScrollBar.Value, maximumTopIndex);
    }

    private void OnProfileListMouseMove(object sender, MouseEventArgs e)
    {
        int index = configurationList.IndexFromPoint(e.Location);
        if (index < 0 || index >= configurationList.Items.Count)
        {
            index = -1;
        }

        if (hoveredProfileIndex != index)
        {
            int previousIndex = hoveredProfileIndex;
            hoveredProfileIndex = index;
            InvalidateProfileItem(previousIndex);
            InvalidateProfileItem(index);
        }
    }

    private void InvalidateProfileItem(int index)
    {
        if (index < 0 || index >= configurationList.Items.Count)
        {
            return;
        }

        Rectangle bounds = configurationList.GetItemRectangle(index);
        if (bounds.IntersectsWith(configurationList.ClientRectangle))
        {
            configurationList.Invalidate(bounds, false);
        }
    }

    private void OnProfileListMouseLeave(object sender, EventArgs e)
    {
        if (hoveredProfileIndex != -1)
        {
            int previousIndex = hoveredProfileIndex;
            hoveredProfileIndex = -1;
            InvalidateProfileItem(previousIndex);
        }
    }

    private void OnProfileListMouseDoubleClick(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        int index = configurationList.IndexFromPoint(e.Location);
        if (index < 0 || index >= configurationList.Items.Count)
        {
            return;
        }

        Rectangle itemBounds = configurationList.GetItemRectangle(index);
        if (!IsProfileActionsIndicatorClick(itemBounds, e.Location))
        {
            OnLaunchClicked(sender, EventArgs.Empty);
        }
    }

    private void OnProfileListMouseUp(object sender, MouseEventArgs e)
    {
        int index = configurationList.IndexFromPoint(e.Location);
        if (index < 0 || index >= configurationList.Items.Count)
        {
            return;
        }

        Rectangle itemBounds = configurationList.GetItemRectangle(index);
        if (!ShouldOpenProfileActionsMenu(
            e.Button,
            itemBounds,
            e.Location,
            index == hoveredProfileIndex))
        {
            return;
        }

        configurationList.SelectedIndex = index;
        Rectangle actionsBounds = GetProfileActionsIndicatorBounds(itemBounds);
        profileActionsMenu.Show(configurationList, new Point(actionsBounds.Left, actionsBounds.Bottom));
    }

    private void OnProfileListScrolled(object sender, EventArgs e)
    {
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(new MethodInvoker(UpdateProfileScrollBar));
        }
    }

    private void UpdateProfileScrollBar()
    {
        if (configurationList == null || profileScrollBar == null)
        {
            return;
        }

        int itemCount = configurationList.Items.Count;
        int visibleItems = Math.Max(1, configurationList.ClientSize.Height / configurationList.ItemHeight);
        int largeChange = Math.Max(1, Math.Min(itemCount, visibleItems));
        profileScrollBar.Minimum = 0;
        profileScrollBar.LargeChange = largeChange;
        profileScrollBar.SmallChange = 1;
        profileScrollBar.Maximum = Math.Max(0, itemCount - 1);
        profileScrollBar.Enabled = itemCount > visibleItems;

        int maximumTopIndex = Math.Max(0, itemCount - visibleItems);
        int value = Math.Max(0, Math.Min(configurationList.TopIndex, maximumTopIndex));
        if (profileScrollBar.Value != value)
        {
            profileScrollBar.Value = value;
        }
    }

    private void OnAddProfileClicked(object sender, EventArgs e)
    {
        int index = configurationList.SelectedIndex;
        LauncherConfiguration template;
        if (configurations.Count == 0)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Selecciona el ejecutable de SimCity 4";
                dialog.Filter = "SimCity 4 (SimCity 4.exe)|SimCity 4.exe|Archivos ejecutables (*.exe)|*.exe";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                template = new LauncherConfiguration
                {
                    Target = dialog.FileName,
                    WorkingDirectory = Path.GetDirectoryName(dialog.FileName),
                    IconLocation = dialog.FileName,
                    Arguments = string.Empty,
                    BackgroundPath = string.Empty
                };
            }
        }
        else
        {
            if (index < 0 || index >= configurations.Count)
            {
                return;
            }

            template = configurations[index];
        }

        string profileName = "Nuevo perfil";
        string arguments = template.Arguments ?? string.Empty;
        string backgroundPath = template.BackgroundPath ?? string.Empty;
        while (true)
        {
            using (ProfileEditorForm editor = new ProfileEditorForm(profileName, arguments, backgroundPath))
            {
                if (editor.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                profileName = editor.ProfileName;
                arguments = editor.Arguments;
                backgroundPath = editor.BackgroundPath;
            }

            string error = ValidateProfileName(profileName, null);
            if (error == null)
            {
                break;
            }

            MessageBox.Show(error, "Perfil no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        LauncherConfiguration profile = new LauncherConfiguration
        {
            Name = profileName,
            Target = template.Target,
            Arguments = arguments,
            WorkingDirectory = template.WorkingDirectory,
            IconLocation = template.IconLocation,
            BackgroundPath = backgroundPath
        };
        profile.ShortcutPath = GetShortcutPath(profile.Name);

        try
        {
            SaveShortcut(profile, profile.ShortcutPath, profile.Arguments);
            configurations.Add(profile);
            RefreshProfileList(profile);
            SaveProfileBackgrounds();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "No se pudo crear el perfil:\r\n" + exception.Message,
                "SimCity 4 Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnEditProfileClicked(object sender, EventArgs e)
    {
        int index = configurationList.SelectedIndex;
        if (index < 0 || index >= configurations.Count)
        {
            return;
        }

        LauncherConfiguration profile = configurations[index];
        string profileName = profile.Name;
        string arguments = profile.Arguments ?? string.Empty;
        string backgroundPath = profile.BackgroundPath ?? string.Empty;
        while (true)
        {
            using (ProfileEditorForm editor = new ProfileEditorForm(profileName, arguments, backgroundPath))
            {
                if (editor.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                profileName = editor.ProfileName;
                arguments = editor.Arguments;
                backgroundPath = editor.BackgroundPath;
            }

            string error = ValidateProfileName(profileName, profile);
            if (error == null)
            {
                break;
            }

            MessageBox.Show(error, "Perfil no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        if (string.Equals(profileName, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            profileName = profile.Name;
        }

        string newShortcutPath = GetShortcutPath(profileName);
        bool renamed = !string.Equals(profile.ShortcutPath, newShortcutPath, StringComparison.OrdinalIgnoreCase);
        try
        {
            SaveShortcut(profile, newShortcutPath, arguments);
            if (renamed)
            {
                try
                {
                    File.Delete(profile.ShortcutPath);
                }
                catch
                {
                    File.Delete(newShortcutPath);
                    throw;
                }
            }

            profile.Name = profileName;
            profile.Arguments = arguments;
            profile.ShortcutPath = newShortcutPath;
            profile.BackgroundPath = backgroundPath;
            RefreshProfileList(profile);
            SaveProfileBackgrounds();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "No se pudo guardar el perfil:\r\n" + exception.Message,
                "SimCity 4 Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnDeleteProfileClicked(object sender, EventArgs e)
    {
        int index = configurationList.SelectedIndex;
        if (index < 0 || index >= configurations.Count || configurations.Count <= 1)
        {
            return;
        }

        LauncherConfiguration profile = configurations[index];
        DialogResult confirmation = MessageBox.Show(
            "¿Quieres eliminar el perfil \"" + profile.Name + "\"? Se quitará su acceso directo, pero no se borrarán los datos de la ciudad ni el juego.",
            "Eliminar perfil",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        try
        {
            File.Delete(profile.ShortcutPath);
            configurations.Remove(profile);
            LauncherConfiguration nextProfile = configurations[Math.Min(index, configurations.Count - 1)];
            RefreshProfileList(nextProfile);
            SaveProfileBackgrounds();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "No se pudo eliminar el perfil:\r\n" + exception.Message,
                "SimCity 4 Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SaveProfileBackgrounds()
    {
        try
        {
            ProfileBackgroundStore.Save(configurations);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "No se pudo guardar la imagen de fondo de los perfiles:\r\n" + exception.Message,
                "SimCity 4 Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private string ValidateProfileName(string name, LauncherConfiguration profileBeingEdited)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return "Escribe un nombre para el perfil.";
        }

        if (name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "El nombre contiene caracteres no permitidos para un archivo.";
        }

        if (!string.Equals(name, name.TrimEnd(' ', '.'), StringComparison.Ordinal))
        {
            return "El nombre no puede terminar con un espacio o un punto.";
        }

        if (Regex.IsMatch(name, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\\.|$)", RegexOptions.IgnoreCase))
        {
            return "Ese nombre está reservado por Windows.";
        }

        foreach (LauncherConfiguration profile in configurations)
        {
            if (profile != profileBeingEdited &&
                string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return "Ya existe un perfil con ese nombre.";
            }
        }

        string shortcutPath = GetShortcutPath(name);
        if (File.Exists(shortcutPath) &&
            (profileBeingEdited == null || !string.Equals(
                shortcutPath,
                profileBeingEdited.ShortcutPath,
                StringComparison.OrdinalIgnoreCase)))
        {
            return "Ya existe un acceso directo con ese nombre.";
        }

        return null;
    }

    private static string GetShortcutPath(string profileName)
    {
        return Path.Combine(LauncherPaths.ProfilesDirectory, "SC4 (" + profileName + ").lnk");
    }

    private static void SaveShortcut(
        LauncherConfiguration profile,
        string shortcutPath,
        string arguments)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            throw new InvalidOperationException("No se pudo acceder al servicio de accesos directos de Windows.");
        }

        object shell = null;
        object shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });
            SetShortcutProperty(shortcut, "TargetPath", profile.Target);
            SetShortcutProperty(shortcut, "Arguments", arguments ?? string.Empty);
            SetShortcutProperty(shortcut, "WorkingDirectory", profile.WorkingDirectory ?? string.Empty);
            SetShortcutProperty(shortcut, "IconLocation", profile.IconLocation ?? string.Empty);
            shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            Program.ReleaseComObject(shortcut);
            Program.ReleaseComObject(shell);
        }
    }

    private static void SetShortcutProperty(object shortcut, string propertyName, string value)
    {
        shortcut.GetType().InvokeMember(
            propertyName,
            BindingFlags.SetProperty,
            null,
            shortcut,
            new object[] { value });
    }

    private void RefreshProfileList(LauncherConfiguration selectedProfile)
    {
        configurations.Sort(delegate(LauncherConfiguration first, LauncherConfiguration second)
        {
            return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
        });

        configurationList.BeginUpdate();
        try
        {
            configurationList.Items.Clear();
            foreach (LauncherConfiguration profile in configurations)
            {
                configurationList.Items.Add(profile.Name);
            }

            int selectedIndex = configurations.IndexOf(selectedProfile);
            configurationList.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }
        finally
        {
            configurationList.EndUpdate();
        }

    }

    private void OnDrawConfiguration(object sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= configurationList.Items.Count)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Rectangle bounds = e.Bounds;
        if (selected)
        {
            using (SolidBrush background = new SolidBrush(SelectedColor))
            {
                e.Graphics.FillRectangle(background, bounds);
            }
        }
        else if (e.Index == hoveredProfileIndex)
        {
            using (SolidBrush background = new SolidBrush(SelectedColor))
            {
                e.Graphics.FillRectangle(background, bounds);
            }
        }

        Color textColor = selected ? MainTextColor : MutedTextColor;
        using (SolidBrush textBrush = new SolidBrush(textColor))
        {
            Rectangle textBounds = new Rectangle(
                bounds.X + 16,
                bounds.Y,
                Math.Max(0, bounds.Width - ProfileActionsIndicatorWidth - 20),
                bounds.Height);
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                e.Graphics.DrawString(
                    configurationList.Items[e.Index].ToString(),
                    selected ? selectedProfileFont : configurationList.Font,
                    textBrush,
                    textBounds,
                    format);
            }
        }

        if (ShouldDrawProfileActionsIndicator(e.Index, hoveredProfileIndex))
        {
            Rectangle actionBounds = GetProfileActionsIndicatorBounds(bounds);
            Image actionIcon = BootstrapIcons.Load("three-dots-vertical", MainTextColor, 16);
            if (actionIcon != null)
            {
                Rectangle iconBounds = new Rectangle(
                    actionBounds.Left + (actionBounds.Width - actionIcon.Width) / 2,
                    actionBounds.Top + (actionBounds.Height - actionIcon.Height) / 2,
                    actionIcon.Width,
                    actionIcon.Height);
                e.Graphics.DrawImage(actionIcon, iconBounds);
            }
            else
            {
                using (SolidBrush actionBrush = new SolidBrush(MainTextColor))
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    e.Graphics.DrawString("\u22EE", profileActionsGlyphFont, actionBrush, actionBounds, format);
                }
            }
        }

        if (selected)
        {
            using (SolidBrush accent = new SolidBrush(AccentColor))
            {
                e.Graphics.FillRectangle(accent, GetSelectedProfileIndicatorBounds(bounds));
            }
        }
    }

    private static Rectangle GetSelectedProfileIndicatorBounds(Rectangle itemBounds)
    {
        return new Rectangle(
            itemBounds.X,
            itemBounds.Y,
            SelectedProfileIndicatorWidth,
            itemBounds.Height);
    }

    private static Rectangle GetProfileActionsIndicatorBounds(Rectangle itemBounds)
    {
        return new Rectangle(
            itemBounds.Right - ProfileActionsIndicatorWidth,
            itemBounds.Y,
            ProfileActionsIndicatorWidth,
            itemBounds.Height);
    }

    private static bool IsProfileActionsIndicatorClick(Rectangle itemBounds, Point location)
    {
        return GetProfileActionsIndicatorBounds(itemBounds).Contains(location);
    }

    private static bool ShouldDrawProfileActionsIndicator(int profileIndex, int hoveredIndex)
    {
        return profileIndex >= 0 && profileIndex == hoveredIndex;
    }

    private static bool ShouldOpenProfileActionsMenu(
        MouseButtons button,
        Rectangle itemBounds,
        Point location,
        bool isHovered)
    {
        return isHovered && button == MouseButtons.Left && IsProfileActionsIndicatorClick(itemBounds, location);
    }

    private void OnLaunchClicked(object sender, EventArgs e)
    {
        int index = configurationList.SelectedIndex;
        if (index < 0 || index >= configurations.Count)
        {
            return;
        }

        LauncherConfiguration configuration = configurations[index];
        if (!File.Exists(configuration.Target))
        {
            MessageBox.Show(
                "No se encontró el ejecutable de SimCity 4:\r\n" + configuration.Target,
                "SimCity 4 Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        try
        {
            ProcessStartInfo process = new ProcessStartInfo();
            process.FileName = configuration.Target;
            process.Arguments = configuration.Arguments ?? string.Empty;
            process.WorkingDirectory = Directory.Exists(configuration.WorkingDirectory)
                ? configuration.WorkingDirectory
                : Path.GetDirectoryName(configuration.Target);
            process.UseShellExecute = true;
            Process.Start(process);
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "No se pudo iniciar " + configuration.Name + ":\r\n" + exception.Message,
                "SimCity 4 Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void LoadWindowIcon()
    {
        foreach (LauncherConfiguration configuration in configurations)
        {
            string iconPath = GetIconPath(configuration.IconLocation);
            if (!File.Exists(iconPath))
            {
                continue;
            }

            try
            {
                using (Icon gameIcon = new Icon(iconPath))
                {
                    Icon = (Icon)gameIcon.Clone();
                }
                return;
            }
            catch
            {
                return;
            }
        }
    }

    private static string GetIconPath(string iconLocation)
    {
        if (string.IsNullOrWhiteSpace(iconLocation))
        {
            return string.Empty;
        }

        string path = iconLocation.Trim().Trim('"');
        int comma = path.LastIndexOf(',');
        int iconIndex;
        if (comma > 0 && int.TryParse(path.Substring(comma + 1), out iconIndex))
        {
            path = path.Substring(0, comma).Trim().Trim('"');
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

}

internal sealed class ProfileActionsColorTable : ProfessionalColorTable
{
    private readonly Color backgroundColor;
    private readonly Color selectedColor;
    private readonly Color borderColor;

    public ProfileActionsColorTable(Color backgroundColor, Color selectedColor, Color borderColor)
    {
        this.backgroundColor = backgroundColor;
        this.selectedColor = selectedColor;
        this.borderColor = borderColor;
    }

    public override Color ToolStripDropDownBackground
    {
        get { return backgroundColor; }
    }

    public override Color ImageMarginGradientBegin
    {
        get { return backgroundColor; }
    }

    public override Color ImageMarginGradientMiddle
    {
        get { return backgroundColor; }
    }

    public override Color ImageMarginGradientEnd
    {
        get { return backgroundColor; }
    }

    public override Color MenuItemSelected
    {
        get { return selectedColor; }
    }

    public override Color MenuItemSelectedGradientBegin
    {
        get { return selectedColor; }
    }

    public override Color MenuItemSelectedGradientEnd
    {
        get { return selectedColor; }
    }

    public override Color MenuItemBorder
    {
        get { return selectedColor; }
    }

    public override Color MenuBorder
    {
        get { return borderColor; }
    }
}

internal sealed class ProfileEditorForm : Form
{
    private const int EditorButtonGap = 8;
    private static readonly Color BackgroundColor = Color.FromArgb(16, 24, 31);
    private static readonly Color PanelColor = Color.FromArgb(25, 38, 48);
    private static readonly Color AccentColor = Color.FromArgb(157, 88, 128);
    private static readonly Color MainTextColor = Color.FromArgb(235, 241, 243);
    private static readonly Color MutedTextColor = Color.FromArgb(154, 171, 180);

    private readonly TextBox nameBox;
    private readonly TextBox argumentsBox;
    private readonly TextBox backgroundPathBox;
    private readonly Button chooseBackgroundButton;
    private readonly Button clearBackgroundButton;
    private readonly Button saveButton;
    private readonly Button cancelButton;

    public ProfileEditorForm(string profileName, string arguments, string backgroundPath)
    {
        Text = "Perfil";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 420);
        BackColor = BackgroundColor;
        ForeColor = MainTextColor;
        Font = new Font("Trebuchet MS", 12F);

        Label nameLabel = new Label();
        nameLabel.Text = "NOMBRE DEL PERFIL";
        nameLabel.Font = new Font(LauncherFonts.DisplayFamily, 11F, FontStyle.Bold);
        nameLabel.ForeColor = MutedTextColor;
        nameLabel.Location = new Point(24, 22);
        nameLabel.Size = new Size(300, 20);
        Controls.Add(nameLabel);

        nameBox = new TextBox();
        nameBox.Text = profileName ?? string.Empty;
        nameBox.MaxLength = 100;
        nameBox.Location = new Point(24, 46);
        nameBox.Size = new Size(472, 28);
        nameBox.BackColor = PanelColor;
        nameBox.ForeColor = MainTextColor;
        nameBox.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(nameBox);

        Label argumentsLabel = new Label();
        argumentsLabel.Text = "PARÁMETROS DE INICIO";
        argumentsLabel.Font = new Font(LauncherFonts.DisplayFamily, 11F, FontStyle.Bold);
        argumentsLabel.ForeColor = MutedTextColor;
        argumentsLabel.Location = new Point(24, 91);
        argumentsLabel.Size = new Size(300, 20);
        Controls.Add(argumentsLabel);

        argumentsBox = new TextBox();
        argumentsBox.Text = arguments ?? string.Empty;
        argumentsBox.Location = new Point(24, 115);
        argumentsBox.Size = new Size(472, 130);
        argumentsBox.Multiline = true;
        argumentsBox.ScrollBars = ScrollBars.Vertical;
        argumentsBox.AcceptsReturn = true;
        argumentsBox.BackColor = PanelColor;
        argumentsBox.ForeColor = MainTextColor;
        argumentsBox.BorderStyle = BorderStyle.FixedSingle;
        argumentsBox.Font = new Font("Consolas", 11F);
        Controls.Add(argumentsBox);

        Label backgroundLabel = new Label();
        backgroundLabel.Text = "IMAGEN DE FONDO";
        backgroundLabel.Font = new Font(LauncherFonts.DisplayFamily, 11F, FontStyle.Bold);
        backgroundLabel.ForeColor = MutedTextColor;
        backgroundLabel.Location = new Point(24, 257);
        backgroundLabel.Size = new Size(300, 20);
        Controls.Add(backgroundLabel);

        backgroundPathBox = new TextBox();
        backgroundPathBox.Text = backgroundPath ?? string.Empty;
        backgroundPathBox.Location = new Point(24, 282);
        backgroundPathBox.Size = new Size(270, 28);
        backgroundPathBox.ReadOnly = true;
        backgroundPathBox.BackColor = PanelColor;
        backgroundPathBox.ForeColor = MainTextColor;
        backgroundPathBox.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(backgroundPathBox);

        chooseBackgroundButton = CreateButton("Elegir", false);
        BootstrapIcons.SetButtonIcon(chooseBackgroundButton, "image", MainTextColor, 16);
        chooseBackgroundButton.Location = new Point(backgroundPathBox.Right + EditorButtonGap, 280);
        chooseBackgroundButton.Size = new Size(100, 32);
        SetEditorButtonIconAlignment(chooseBackgroundButton);
        chooseBackgroundButton.Click += OnChooseBackgroundClicked;
        Controls.Add(chooseBackgroundButton);

        clearBackgroundButton = CreateButton("Quitar", false);
        BootstrapIcons.SetButtonIcon(clearBackgroundButton, "x-lg", MainTextColor, 14);
        clearBackgroundButton.Size = new Size(86, 32);
        clearBackgroundButton.Location = new Point(
            chooseBackgroundButton.Right + EditorButtonGap,
            chooseBackgroundButton.Top);
        SetEditorButtonIconAlignment(clearBackgroundButton);
        clearBackgroundButton.Click += delegate { backgroundPathBox.Clear(); };
        Controls.Add(clearBackgroundButton);

        saveButton = CreateButton("Guardar", true);
        BootstrapIcons.SetButtonIcon(saveButton, "check-lg", MainTextColor, 16);
        saveButton.Size = new Size(134, 38);
        saveButton.Location = new Point(ClientSize.Width - 24 - saveButton.Width, 363);
        SetEditorButtonIconAlignment(saveButton);
        saveButton.DialogResult = DialogResult.OK;
        Controls.Add(saveButton);

        cancelButton = CreateButton("Cancelar", false);
        BootstrapIcons.SetButtonIcon(cancelButton, "x-lg", MainTextColor, 14);
        cancelButton.Size = new Size(108, 38);
        cancelButton.Location = new Point(
            saveButton.Left - EditorButtonGap - cancelButton.Width,
            saveButton.Top);
        SetEditorButtonIconAlignment(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Shown += delegate
        {
            nameBox.SelectAll();
            nameBox.Focus();
        };
    }

    public bool ButtonLayoutIsValid
    {
        get
        {
            return chooseBackgroundButton.Left - backgroundPathBox.Right == EditorButtonGap &&
                clearBackgroundButton.Left - chooseBackgroundButton.Right == EditorButtonGap &&
                clearBackgroundButton.Right == ClientSize.Width - 24 &&
                saveButton.Left - cancelButton.Right == EditorButtonGap &&
                saveButton.Right == ClientSize.Width - 24 &&
                saveButton.BackColor == AccentColor &&
                saveButton.ForeColor == MainTextColor &&
                HasButtonContentSpace(chooseBackgroundButton) &&
                HasButtonContentSpace(clearBackgroundButton) &&
                HasButtonContentSpace(saveButton) &&
                HasButtonContentSpace(cancelButton);
        }
    }

    private static void SetEditorButtonIconAlignment(Button button)
    {
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Padding = Padding.Empty;
    }

    private static bool HasButtonContentSpace(Button button)
    {
        if (button.Image == null)
        {
            return false;
        }

        int contentWidth = TextRenderer.MeasureText(button.Text, button.Font).Width +
            button.Image.Width + EditorButtonGap;
        return button.ImageAlign == ContentAlignment.MiddleCenter &&
            button.TextAlign == ContentAlignment.MiddleCenter &&
            button.TextImageRelation == TextImageRelation.ImageBeforeText &&
            button.Padding == Padding.Empty &&
            button.Width >= contentWidth;
    }

    public string ProfileName
    {
        get { return nameBox.Text.Trim(); }
    }

    public string Arguments
    {
        get { return argumentsBox.Text; }
    }

    public string BackgroundPath
    {
        get { return backgroundPathBox.Text.Trim(); }
    }

    private void OnChooseBackgroundClicked(object sender, EventArgs e)
    {
        using (OpenFileDialog dialog = new OpenFileDialog())
        {
            dialog.Title = "Seleccionar imagen de fondo";
            dialog.Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|Todos los archivos|*.*";
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;
            if (File.Exists(backgroundPathBox.Text))
            {
                dialog.FileName = backgroundPathBox.Text;
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    backgroundPathBox.Text = CopyBackgroundToFolder(
                        dialog.FileName,
                        LauncherPaths.BackgroundsDirectory);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "No se pudo copiar la imagen a la carpeta del launcher:\r\n" + exception.Message,
                        "Imagen de fondo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }
    }

    private static string CopyBackgroundToFolder(string sourcePath, string destinationDirectory)
    {
        string source = Path.GetFullPath(sourcePath);
        string launcherDirectory = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(launcherDirectory);
        string destination = Path.Combine(launcherDirectory, Path.GetFileName(source));
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            return destination;
        }

        string name = Path.GetFileNameWithoutExtension(source);
        string extension = Path.GetExtension(source);
        int copyNumber = 2;
        while (File.Exists(destination))
        {
            destination = Path.Combine(
                launcherDirectory,
                name + " (" + copyNumber + ")" + extension);
            copyNumber++;
        }

        File.Copy(source, destination);
        return destination;
    }

    private static Button CreateButton(string text, bool primary)
    {
        Button button = new Button();
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = primary ? AccentColor : PanelColor;
        button.ForeColor = MainTextColor;
        button.Font = new Font("Trebuchet MS", 10F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        return button;
    }
}