using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GuruxAMI.Client;
using GuruxAMI.Common;
using Gurux.Common;
using Gurux.Communication;
using Gurux.Serial;
using Gurux.Terminal;

namespace GuruxAMI.Gateway
{
    partial class Settings : Form, Gurux.Common.IGXPropertyPage
    {
        Form PropertiesForm;
        GXAmiGateway Target;

        public Settings(GXAmiGateway target)
        {
            Target = target;
            InitializeComponent();
        }

        #region IGXPropertyPage Members

        void Gurux.Common.IGXPropertyPage.Initialize()
        {
            Target.Initialize();
            if (Target.Client != null)
            {
                foreach (GXAmiDataCollector it in Target.Client.GetDataCollectors())
                {
                    int pos = DataCollectorCB.Items.Add(it);
                    if (Target.DataCollector == it.Guid)
                    {
                        DataCollectorCB.SelectedItem = it;
                    }
                }
                //Select first Data Collector if not selected.
                if (DataCollectorCB.SelectedIndex == -1 && DataCollectorCB.Items.Count != 0)
                {
                    DataCollectorCB.SelectedIndex = 0;
                }
            }
        }

        void Gurux.Common.IGXPropertyPage.Apply()
        {
            ((IGXPropertyPage)PropertiesForm).Apply();
            Target.DataCollector = (DataCollectorCB.SelectedItem as GXAmiDataCollector).Guid;
            Target.Media = MediaCB.Text;
            Target.MediaSettings = Target.Target.Settings;
        }

        #endregion

        private void DataCollectorCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            MediaCB.Items.Clear();
            GXAmiDataCollector dc = DataCollectorCB.SelectedItem as GXAmiDataCollector;
            Target.DataCollector = dc.Guid;
            foreach (string media in dc.Medias)
            {
                //Do not shown Gateway int the media list.
                if (media != Target.MediaType)
                {
                    MediaCB.Items.Add(media);
                    if (Target.Media == media)
                    {
                        MediaCB.SelectedItem = media;
                    }
                }
            }
            //Select first media if not selected.
            if (MediaCB.SelectedIndex == -1 && MediaCB.Items.Count != 0)
            {
                MediaCB.SelectedIndex = 0;
            }
        }

        private void MediaCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                MediaFrame.Controls.Clear();
                if (Target.Target == null || Target.Target.MediaType != MediaCB.Text)
                {
                    GXClient cl = new GXClient();
                    Target.Target = cl.SelectMedia(MediaCB.Text);
                    if (Target.Target == null)
                    {
                        throw new Exception(MediaCB.Text + " media not found.");
                    }
                }
                if (Target.GXClient != null && Target.GXClient.PacketParser != null)
                {
                    Target.GXClient.PacketParser.InitializeMedia(Target.GXClient, Target.Target);
                }
                if (!string.IsNullOrEmpty(Target.MediaSettings))
                {
                    Target.Target.Settings = Target.MediaSettings;
                }
                if (Target.Target is GXSerial)
                {
                    (Target.Target as GXSerial).AvailablePorts = (DataCollectorCB.SelectedItem as GXAmiDataCollector).SerialPorts;
                }
                else if (Target.Target is GXTerminal)
                {
                    (Target.Target as GXTerminal).AvailablePorts = (DataCollectorCB.SelectedItem as GXAmiDataCollector).SerialPorts;
                }
                PropertiesForm = Target.Target.PropertiesForm;
                ((IGXPropertyPage)PropertiesForm).Initialize();
                while (PropertiesForm.Controls.Count != 0)
                {
                    Control ctr = PropertiesForm.Controls[0];
                    if (ctr is Panel)
                    {
                        if (!ctr.Enabled)
                        {
                            PropertiesForm.Controls.RemoveAt(0);
                            continue;
                        }
                    }
                    MediaFrame.Controls.Add(ctr);
                }
            }
            catch (Exception ex)
            {
                GXCommon.ShowError(this, ex);
            }
        }
    }
}
