﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSScriptNpp
{
    public partial class ConfigForm : Form
    {
        Config data;

        CSScriptIntellisense.ConfigForm panel;

        public ConfigForm()
        {
            InitializeComponent();
        }

        public ConfigForm(Config data)
        {
            this.data = data;

            InitializeComponent();

            panel = new CSScriptIntellisense.ConfigForm(CSScriptIntellisense.Config.Instance);
            generalPage.Controls.Add(panel.ContentPanel);

            checkUpdates.Checked = data.CheckUpdatesOnStartup;
            useCS6.Checked = data.UseRoslynProvider;

            installedEngineLocation.Text = CSScriptHelper.SystemCSScriptDir ?? "<not detected>";
            installedEngineLocation.SelectionStart = installedEngineLocation.Text.Length - 1;

            embeddedEngine.Checked = data.UseEmbeddedEngine;
            restorePanels.Checked = data.RestorePanelsAtStartup;
            if (!data.UseEmbeddedEngine)
            {
                if (data.UseCustomEngine.IsEmpty())
                {
                    installedEngine.Checked = true;
                }
                else
                {
                    customEngine.Checked = true;
                    customEngineLocation.Text = data.UseCustomEngine;
                }
            }
        }

        private void ConfigForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            panel.OnClosing();
            data.CheckUpdatesOnStartup = checkUpdates.Checked;
            data.UseEmbeddedEngine = embeddedEngine.Checked;
            data.RestorePanelsAtStartup = restorePanels.Checked;

            //data.UseRoslynProvider = useCS6.Checked;
            //all Roslyn individual config values are merged into RoslynIntellisense;
            data.UseRoslynProvider = CSScriptIntellisense.Config.Instance.RoslynIntellisense;

            if (customEngine.Checked)
            {
                data.UseCustomEngine = customEngineLocation.Text;
            }
            else
            {
                data.UseCustomEngine = "";
            }

            Config.Instance.Save();
        }

        private void ConfigForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string file = Config.Instance.GetFileName();
            Config.Instance.Save();

            Task.Factory.StartNew(() =>
            {
                try
                {
                    Thread.Sleep(500);
                    DateTime timestamp = File.GetLastWriteTimeUtc(file);
                    Process.Start("notepad.exe", file).WaitForExit();
                    if (File.GetLastWriteTimeUtc(file) != timestamp)
                        Config.Instance.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: \n" + ex.ToString(), "Notepad++");
                }
            });

            Close();
        }

        void engine_CheckedChanged(object sender, EventArgs e)
        {
            customEngineLocation.ReadOnly = !customEngine.Checked;
        }
    }
}