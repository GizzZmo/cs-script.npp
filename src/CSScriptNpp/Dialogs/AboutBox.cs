﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using CSScriptNpp.Dialogs;

namespace CSScriptNpp
{
    partial class AboutBox : Form
    {
        public AboutBox()
        {
            InitializeComponent();
            this.Text = "About CS-Script";
            this.label3.Text = $"Version: {AssemblyVersion}; CLR: v{Environment.Version.Major}.{Environment.Version.Minor}.{Environment.Version.Build}";
            this.label5.Text = AssemblyCopyright;
            this.textBoxDescription.Text = AssemblyDescription;
            this.includePrereleases.Checked = Config.Instance.CheckPrereleaseUpdates;
            if (downloadingMsi)
                SetUpdateStatus("Downloading...");
        }

        public Action PostCloseAction = () => { };

        #region Assembly Attribute Accessors

        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                return
                    Assembly.GetExecutingAssembly().GetName().Version.ToString() +
                    "  (script engine cscs.exe: " + CSScriptHelper.ScriptEngineVersion() + ")";
            }
        }

        public string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }

        #endregion Assembly Attribute Accessors

        void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/oleg-shilo/cs-script.npp/blob/master/license.txt");
            }
            catch { }
        }

        void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/oleg-shilo/cs-script.npp");
            }
            catch { }
        }

        void updateCheckBtn_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;

            SetUpdateStatus("Checking...");
            Task.Factory.StartNew(CheckForUpdates);
        }

        static bool downloadingMsi = false;

        void SetUpdateStatus(string status = null)
        {
            if (status == null)
            {
                updateCheckBtn.Enabled = true;
                updateCheckBtn.Text = "Check for Updates...";
            }
            else
            {
                updateCheckBtn.Enabled = true;
                updateCheckBtn.Text = status;
            }
        }

        void CheckForUpdates()
        {
            Distro distro = CSScriptHelper.GetLatestAvailableVersion();

            Invoke((Action)delegate
            {
                SetUpdateStatus();
                Cursor = Cursors.Default;
            });

            if (distro == null)
            {
                MessageBox.Show("Cannot check for updates. The latest release Web page will be opened instead.", "CS-Script");
                try
                {
                    Process.Start(Plugin.HomeUrl);
                }
                catch { }
            }
            else
            {
                var latestVersion = new Version(distro.Version);
                var nppVersion = Assembly.GetExecutingAssembly().GetName().Version;

                if (nppVersion == latestVersion)
                {
                    MessageBox.Show("You are already running the latest version - v" + distro.Version, "CS-Script");
                }
                else if (nppVersion > latestVersion)
                {
                    MessageBox.Show("Wow... your version is even newer than the latest one - v" + distro.Version + ".", "CS-Script");
                }
                else if (nppVersion < latestVersion)
                {
                    PostCloseAction = //Task.Factory.StartNew(
                        () =>
                        {
                            using (var dialog = new UpdateOptionsPanel(distro))
                                dialog.ShowModal();
                        };//);

                    Invoke((Action)delegate
                    {
                        Close();
                    });
                }
            }
        }

        void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/oleg-shilo/cs-script.npp/issues");
            }
            catch { }
        }

        int blinkingCount = 0;

        void timer1_Tick(object sender, EventArgs e)
        {
            if (updateCheckBtn.Text.StartsWith("Downloading"))
            {
                blinkingCount++;
                if (blinkingCount > 3)
                    blinkingCount = 0;

                updateCheckBtn.Text = "Downloading" + new string('.', blinkingCount);
            }
        }

        void linkLabel4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(PluginEnv.LogDir);
            }
            catch { }
        }

        void includePrereleases_CheckedChanged(object sender, EventArgs e)
        {
            if (this.includePrereleases.Checked != Config.Instance.CheckPrereleaseUpdates)
            {
                Config.Instance.CheckPrereleaseUpdates = this.includePrereleases.Checked;
                Config.Instance.Save();
            }
        }

        void linkLabel5_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var plugin_dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var updater_exe = Path.Combine(plugin_dir, "CSScriptNpp", "Updater.exe");

            try
            {
                //Process.Start(updater_exe, "restore"); //not reliable
            }
            catch { }
        }

        void button1_Click(object sender, EventArgs e)
        {
            CSScriptHelper.LoadRoslyn();
        }
    }
}