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

namespace GuruxAMI.Gateway
{
    public partial class Settings : Form, Gurux.Common.IGXPropertyPage
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
            string host = Target.Host;
            if (host == "*")
            {
                host = "localhost";
            }
            bool webServer = host.StartsWith("http://");
            if (!webServer && (string.IsNullOrEmpty(host) || Target.Port == 0))
            {
                throw new ArgumentException("Invalid url");
            }
            if (Target.Client == null)
            {
                string baseUr;
                if (webServer)
                {
                    baseUr = host;
                }
                else
                {
                    baseUr = "http://" + host + ":" + Target.Port + "/";
                }
                Target.Client = new GXAmiClient(baseUr, Target.UserName, Target.Password);
            }
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
            foreach(string media in (DataCollectorCB.SelectedItem as GXAmiDataCollector).Medias)
            {
                MediaCB.Items.Add(media);
                if (Target.Media == media)
                {
                    MediaCB.SelectedItem = media;
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
                GXClient cl = new GXClient();
                Target.Target = cl.SelectMedia(MediaCB.Text);
                if (Target.Target == null)
                {
                    throw new Exception(MediaCB.Text + " media not found.");
                }
                Target.Target.Settings = Target.MediaSettings;
                if (Target.GXClient.PacketParser != null)
                {
                    Target.GXClient.PacketParser.InitializeMedia(Target.GXClient, Target.Target);
                }
                if (Target.Target is GXSerial)
                {
                    (Target.Target as GXSerial).AvailablePorts = (DataCollectorCB.SelectedItem as GXAmiDataCollector).SerialPorts;
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
