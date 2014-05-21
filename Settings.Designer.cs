namespace GuruxAMI.Gateway
{
    partial class Settings
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.MediaPanel = new System.Windows.Forms.Panel();
            this.MediaCB = new System.Windows.Forms.ComboBox();
            this.MediaLbl = new System.Windows.Forms.Label();
            this.DataCollectorPanel = new System.Windows.Forms.Panel();
            this.DataCollectorCB = new System.Windows.Forms.ComboBox();
            this.DataCollectorLbl = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.MediaFrame = new System.Windows.Forms.Panel();
            this.groupBox1.SuspendLayout();
            this.MediaPanel.SuspendLayout();
            this.DataCollectorPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.MediaFrame);
            this.groupBox1.Controls.Add(this.panel1);
            this.groupBox1.Controls.Add(this.MediaPanel);
            this.groupBox1.Controls.Add(this.DataCollectorPanel);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(284, 262);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Data Collector Settings;";
            // 
            // MediaPanel
            // 
            this.MediaPanel.Controls.Add(this.MediaCB);
            this.MediaPanel.Controls.Add(this.MediaLbl);
            this.MediaPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.MediaPanel.Location = new System.Drawing.Point(3, 55);
            this.MediaPanel.Name = "MediaPanel";
            this.MediaPanel.Size = new System.Drawing.Size(278, 41);
            this.MediaPanel.TabIndex = 27;
            // 
            // MediaCB
            // 
            this.MediaCB.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.MediaCB.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.MediaCB.FormattingEnabled = true;
            this.MediaCB.Location = new System.Drawing.Point(85, 6);
            this.MediaCB.Name = "MediaCB";
            this.MediaCB.Size = new System.Drawing.Size(178, 21);
            this.MediaCB.TabIndex = 15;
            this.MediaCB.SelectedIndexChanged += new System.EventHandler(this.MediaCB_SelectedIndexChanged);
            // 
            // MediaLbl
            // 
            this.MediaLbl.AutoSize = true;
            this.MediaLbl.Location = new System.Drawing.Point(5, 9);
            this.MediaLbl.Name = "MediaLbl";
            this.MediaLbl.Size = new System.Drawing.Size(36, 13);
            this.MediaLbl.TabIndex = 12;
            this.MediaLbl.Text = "Media";
            // 
            // DataCollectorPanel
            // 
            this.DataCollectorPanel.Controls.Add(this.DataCollectorCB);
            this.DataCollectorPanel.Controls.Add(this.DataCollectorLbl);
            this.DataCollectorPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.DataCollectorPanel.Location = new System.Drawing.Point(3, 16);
            this.DataCollectorPanel.Name = "DataCollectorPanel";
            this.DataCollectorPanel.Size = new System.Drawing.Size(278, 39);
            this.DataCollectorPanel.TabIndex = 26;
            // 
            // DataCollectorCB
            // 
            this.DataCollectorCB.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DataCollectorCB.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.DataCollectorCB.FormattingEnabled = true;
            this.DataCollectorCB.Location = new System.Drawing.Point(85, 8);
            this.DataCollectorCB.Name = "DataCollectorCB";
            this.DataCollectorCB.Size = new System.Drawing.Size(178, 21);
            this.DataCollectorCB.TabIndex = 15;
            this.DataCollectorCB.SelectedIndexChanged += new System.EventHandler(this.DataCollectorCB_SelectedIndexChanged);
            // 
            // DataCollectorLbl
            // 
            this.DataCollectorLbl.AutoSize = true;
            this.DataCollectorLbl.Location = new System.Drawing.Point(5, 11);
            this.DataCollectorLbl.Name = "DataCollectorLbl";
            this.DataCollectorLbl.Size = new System.Drawing.Size(74, 13);
            this.DataCollectorLbl.TabIndex = 12;
            this.DataCollectorLbl.Text = "Data Collector";
            // 
            // panel1
            // 
            this.panel1.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel1.Location = new System.Drawing.Point(3, 96);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(7, 163);
            this.panel1.TabIndex = 29;
            // 
            // MediaFrame
            // 
            this.MediaFrame.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MediaFrame.Location = new System.Drawing.Point(10, 96);
            this.MediaFrame.Name = "MediaFrame";
            this.MediaFrame.Size = new System.Drawing.Size(271, 163);
            this.MediaFrame.TabIndex = 30;
            // 
            // Settings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.Controls.Add(this.groupBox1);
            this.Name = "Settings";
            this.Text = "Settings";
            this.groupBox1.ResumeLayout(false);
            this.MediaPanel.ResumeLayout(false);
            this.MediaPanel.PerformLayout();
            this.DataCollectorPanel.ResumeLayout(false);
            this.DataCollectorPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Panel MediaPanel;
        private System.Windows.Forms.ComboBox MediaCB;
        private System.Windows.Forms.Label MediaLbl;
        private System.Windows.Forms.Panel DataCollectorPanel;
        private System.Windows.Forms.ComboBox DataCollectorCB;
        private System.Windows.Forms.Label DataCollectorLbl;
        private System.Windows.Forms.Panel MediaFrame;
        private System.Windows.Forms.Panel panel1;


    }
}