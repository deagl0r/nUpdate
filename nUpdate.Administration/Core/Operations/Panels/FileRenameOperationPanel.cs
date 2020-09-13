// FileRenameOperationPanel.cs, 10.06.2019
// Copyright (C) Dominic Beger 17.06.2019

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using nUpdate.Actions;
using nUpdate.Administration.UI.Controls;
using nUpdate.Administration.UI.Popups;

namespace nUpdate.Administration.Core.Operations.Panels
{
    public partial class FileRenameOperationPanel : UserControl, IOperationPanel
    {
        public FileRenameOperationPanel()
        {
            InitializeComponent();
        }

        public string DestinationFilePath
        {
            get => newNameTextBox.Text;
            set => newNameTextBox.Text = value;
        }

        public string SourceFilePath
        {
            get => pathTextBox.Text;
            set => pathTextBox.Text = value;
        }

        public bool IsValid
        {
            get
            {
                return !Controls.OfType<CueTextBox>().Any(item => string.IsNullOrEmpty(item.Text)) &&
                       SourceFilePath.Contains("\\") &&
                       SourceFilePath.Split(new[] {"\\"}, StringSplitOptions.RemoveEmptyEntries).Length >= 2 &&
                       DestinationFilePath.Contains("\\") &&
                       DestinationFilePath.Split(new[] {"\\"}, StringSplitOptions.RemoveEmptyEntries).Length >= 2;
            }
        }

        public IUpdateAction Operation => new MoveFileAction();

        private void environmentVariablesButton_Click(object sender, EventArgs e)
        {
            Popup.ShowPopup(this, SystemIcons.Information, "Environment variables.",
                "%appdata%: AppData\n%temp%: Temp\n%program%: Program's directory\n%desktop%: Desktop directory",
                PopupButtons.Ok);
        }

        private void FileRenameOperationPanel_Load(object sender, EventArgs e)
        {
            // Language initializing follows here
        }

        private void pathTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!pathTextBox.Text.Contains("/"))
                return;
            pathTextBox.Text = pathTextBox.Text.Replace('/', '\\');
            pathTextBox.SelectionStart = pathTextBox.Text.Length;
            pathTextBox.SelectionLength = 0;
        }
    }
}