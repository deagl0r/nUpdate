// BaseDialog.cs, 10.06.2019
// Copyright (C) Dominic Beger 17.06.2019

using System.Windows.Forms;

namespace nUpdate.UI.WindowsForms.Dialogs
{
    internal class BaseDialog : Form
    {
        internal UpdateManager UpdateManager { get; set; }
    }
}