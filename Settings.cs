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
            Target.DataCollectorName = (DataCollectorCB.SelectedItem as GXAmiDataCollector).Name;
            Target.MediaName = MediaCB.Text;
            Target.MediaSettings = Target.Media.Settings;
        }

        #endregion

        private void DataCollectorCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            MediaCB.Items.Clear();
            GXAmiDataCollector dc = DataCollectorCB.SelectedItem as GXAmiDataCollector;
            Target.DataCollector = dc.Guid;
            List<string> allowerMediaTypes = new List<string>();
            if (Target.AllowerMediaTypes != null)
            {
                allowerMediaTypes.AddRange(Target.AllowerMediaTypes);
            }
            foreach (string media in dc.Medias)
            {
                //Do not shown Gateway int the media list.
                if ((allowerMediaTypes.Count == 0 || allowerMediaTypes.Contains(media)) && media != Target.MediaType)
                {
                    MediaCB.Items.Add(media);
                    if (Target.MediaName == media)
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
                if (Target.Media == null || Target.Media.MediaType != MediaCB.Text)
                {
                    GXClient cl = new GXClient();
                    Target.Media = cl.SelectMedia(MediaCB.Text);
                    if (Target.Media == null)
                    {
                        throw new Exception(MediaCB.Text + " media not found.");
                    }
                }
                if (Target.GXClient != null && Target.GXClient.PacketParser != null)
                {
                    Target.GXClient.PacketParser.InitializeMedia(Target.GXClient, Target.Media);
                }
                if (!string.IsNullOrEmpty(Target.MediaSettings))
                {
                    Target.Media.Settings = Target.MediaSettings;
                }
                if (Target.Media is GXSerial)
                {
                    (Target.Media as GXSerial).AvailablePorts = (DataCollectorCB.SelectedItem as GXAmiDataCollector).SerialPorts;
                }
                else if (Target.Media is GXTerminal)
                {
                    (Target.Media as GXTerminal).AvailablePorts = (DataCollectorCB.SelectedItem as GXAmiDataCollector).SerialPorts;
                }
                PropertiesForm = Target.Media.PropertiesForm;
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
