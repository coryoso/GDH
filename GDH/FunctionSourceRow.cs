using System;
using System.Drawing;
using System.Windows.Forms;  
using Grasshopper;

namespace GDH
{
	public class FunctionSourceRow : TableLayoutPanel
	{
		public string SourceName { get; set; }

		public string SourcePath { get; set; }

		public Button EditButton { get; set; }

		public TextBox PathTextBox { get; set; }

		public CheckBox RowCheckbox { get; set; }

		public event EventHandler<ReplaceRowArgs> ReplaceRow;

		private void OnReplaceRow(string rowToReplace)
		{
			if (this.ReplaceRow != null)
			{
				PathTextBox.Text = SourceName;
				this.ReplaceRow(this, new ReplaceRowArgs
				{
					RowName = rowToReplace
				});
			}
		}

		public FunctionSourceRow(string name, string path)
		{
			if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
			{
				return;
			}
			SourceName = name;
			SourcePath = path;
			base.RowStyles.Clear();
			base.ColumnStyles.Clear();
			base.RowCount = 1;
			base.ColumnCount = 3;
			base.RowStyles.Add(new RowStyle(SizeType.Percent, 1f));
			base.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize, 0.05f));
			base.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0.9f));
			base.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize, 0.05f));
			base.Location = new Point(0, 0);
			base.Name = "FunctionSourceRow";
			PathTextBox = InitTextBox(SourceName, SourcePath);
			EditButton = InitButton();
			RowCheckbox = InitCheckbox();
			Anchor = AnchorStyles.Left | AnchorStyles.Right;
			base.Height = PathTextBox.Height;
			EditButton.Click += delegate
			{
				SetFunctionSourceForm setFunctionSourceForm = new SetFunctionSourceForm(SourcePath.Trim(), SourceName.Trim());
				if (setFunctionSourceForm.ShowModal(Instances.EtoDocumentEditor))
				{
					string sourceName2 = SourceName;
					SourcePath = setFunctionSourceForm.Path;
					SourceName = setFunctionSourceForm.Name;
					OnReplaceRow(sourceName2);
				}
			};
			RowCheckbox.CheckedChanged += delegate
			{
				string sourceName = SourceName;
				OnReplaceRow(sourceName);
			};
			base.Controls.Add(RowCheckbox, 0, 0);
			base.Controls.Add(PathTextBox, 1, 0);
			base.Controls.Add(EditButton, 2, 0);
		}

		private Button InitButton()
		{
			return new Button
			{
				Size = new Size(PathTextBox.Height, PathTextBox.Height),
				Margin = new Padding(0),
				Image = HopsFunctionMgr.EditIcon(),
				Name = "EditRowButton"
			};
		}

		private TextBox InitTextBox(string name, string path)
		{
			TextBox textBox = new TextBox();
			textBox.Dock = DockStyle.Fill;
			textBox.Margin = new Padding(0);
			textBox.Name = "Textbox";
			textBox.ReadOnly = true;
			textBox.BackColor = SystemColors.Window;
			textBox.Text = name;
			textBox.MouseHover += Txt_MouseHover;
			return textBox;
		}

		private void Txt_MouseHover(object sender, EventArgs e)
		{
			TextBox textBox = (TextBox)sender;
			int VisibleTime = 3000;
			new ToolTip().Show(SourcePath, textBox, 24, -24, VisibleTime);
		}

		private CheckBox InitCheckbox()
		{
			return new CheckBox
			{
				Checked = false,
				Size = new Size(PathTextBox.Height, PathTextBox.Height),
				Name = "IsSelectedCheckbox"
			};
		}
	}
}