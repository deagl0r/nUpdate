// ServiceStopOperationPanel.cs, 10.06.2019
// Copyright (C) Dominic Beger 17.06.2019

using System.Windows.Forms;
using nUpdate.Actions;

namespace nUpdate.Administration.Core.Operations.Panels
{
    public partial class ServiceStopOperationPanel : UserControl, IOperationPanel
    {
        public ServiceStopOperationPanel()
        {
            InitializeComponent();
        }

        public string ServiceName
        {
            get => serviceNameTextBox.Text;
            set => serviceNameTextBox.Text = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(serviceNameTextBox.Text);
        public IUpdateAction Operation => new StopServiceAction();
    }
}