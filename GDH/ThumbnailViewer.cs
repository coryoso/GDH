using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace GDH
{
	public class ThumbnailViewer : Form
	{
		private IContainer components;

		public PictureBox pictureBox;

		protected override bool CanRaiseEvents => false;

		protected override bool ShowWithoutActivation => true;

		public ThumbnailViewer()
		{
			InitializeComponent();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.pictureBox = new System.Windows.Forms.PictureBox();
			((System.ComponentModel.ISupportInitialize)this.pictureBox).BeginInit();
			base.SuspendLayout();
			this.pictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pictureBox.Location = new System.Drawing.Point(6, 6);
			this.pictureBox.Margin = new System.Windows.Forms.Padding(0);
			this.pictureBox.Name = "pictureBox";
			this.pictureBox.Size = new System.Drawing.Size(150, 100);
			this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
			this.pictureBox.TabIndex = 0;
			this.pictureBox.TabStop = false;
			base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.Color.White;
			base.ClientSize = new System.Drawing.Size(162, 112);
			base.Controls.Add(this.pictureBox);
			base.Enabled = false;
			base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
			base.Name = "ThumbnailViewer";
			base.Padding = new System.Windows.Forms.Padding(6);
			base.ShowInTaskbar = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
			((System.ComponentModel.ISupportInitialize)this.pictureBox).EndInit();
			base.ResumeLayout(false);
		}
	}
}