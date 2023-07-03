using System;
using System.Collections.ObjectModel;
using Eto.Drawing;
using Eto.Forms;
using Rhino.Resources;
using Rhino.Runtime;
using Rhino.UI.Controls;

namespace GDH
{
	internal class SetDefinitionForm : Dialog<bool>
	{
		public string Path { get; set; }

		public SetDefinitionForm(string currentPath)
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Expected O, but got Unknown
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Expected O, but got Unknown
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Expected O, but got Unknown
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Expected O, but got Unknown
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_011a: Expected O, but got Unknown
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0125: Expected O, but got Unknown
			//IL_0131: Unknown result type (might be due to invalid IL or missing references)
			//IL_0169: Unknown result type (might be due to invalid IL or missing references)
			//IL_016f: Expected O, but got Unknown
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f2: Expected O, but got Unknown
			//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0205: Unknown result type (might be due to invalid IL or missing references)
			//IL_0208: Unknown result type (might be due to invalid IL or missing references)
			//IL_0212: Unknown result type (might be due to invalid IL or missing references)
			//IL_0218: Unknown result type (might be due to invalid IL or missing references)
			//IL_021d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0224: Unknown result type (might be due to invalid IL or missing references)
			//IL_023a: Expected O, but got Unknown
			//IL_023a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0250: Expected O, but got Unknown
			Path = currentPath;
			((Window)this).Title =("Set Definition");
			bool onWindows = HostUtils.RunningOnWindows;
			Button val = new Button();
			((TextControl)val).Text=(onWindows ? "OK" : "Apply");
			((Dialog)this).DefaultButton=(val);
			((Dialog)this).DefaultButton.Click+=((EventHandler<EventArgs>)delegate
			{
				base.Close(true);
			});
			Button val2 = new Button();
			((TextControl)val2).Text=("C&ancel");
			((Dialog)this).AbortButton=(val2);
			((Dialog)this).AbortButton.Click+=((EventHandler<EventArgs>)delegate
			{
				base.Close(false);
			});
			TableLayout buttons = new TableLayout();
			if (onWindows)
			{
				buttons.Spacing=(new Size(5, 5));
				buttons.Rows.Add(new TableRow((TableCell[])(object)new TableCell[3]
				{
				default(TableCell),
				DefaultButton,
				AbortButton
				}));
			}
			else
			{
				buttons.Rows.Add(new TableRow((TableCell[])(object)new TableCell[3]
				{
				default(TableCell),
				AbortButton,
				DefaultButton
				}));
			}
			TextBox textbox = new TextBox();
			((Control)textbox).Size=(new Size(250, -1));
			textbox.PlaceholderText=("URL or Path");
			if (!string.IsNullOrWhiteSpace(Path))
			{
				((TextControl)textbox).Text=(Path);
			}
			ImageButton filePickButton = new ImageButton();
			filePickButton.Image=((Image)(object)Assets.Rhino.Eto.Bitmaps
				.TryGet(ResourceIds.FolderopenPng, new Size(24, 24)));
			filePickButton.Click+=((EventHandler<EventArgs>)delegate
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Expected O, but got Unknown
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Expected O, but got Unknown
				//IL_0045: Unknown result type (might be due to invalid IL or missing references)
				//IL_004b: Invalid comparison between Unknown and I4
				OpenFileDialog val6 = new OpenFileDialog();
				((FileDialog)val6).Filters.Add(new FileFilter("Grasshopper Document", new string[2] { ".gh", ".ghx" }));
				Window val7 = (Window)(object)(onWindows ? this : null);
				if ((int)((CommonDialog)val6).ShowDialog(val7) == 1)
				{
					((TextControl)textbox).Text=(((FileDialog)val6).FileName);
				}
			});
			StackLayout val3 = new StackLayout();
			val3.Orientation=((Orientation)0);
			Size spacing = buttons.Spacing;
			val3.Spacing=((Size)(spacing)).Width;
			val3.Items.Add(textbox);
			val3.Items.Add(filePickButton);
			StackLayout locationRow = val3;
			TableLayout val4 = new TableLayout();
			val4.Padding=(new Padding(10));
			val4.Spacing=(new Size(5, 5));
			Collection<TableRow> rows = val4.Rows;
			TableRow val5 = new TableRow();
			val5.ScaleHeight=(true);
			val5.Cells.Add(locationRow);
			rows.Add(val5);
			val4.Rows.Add(buttons);
			((Panel)this).Content=((Control)val4);
			((Window)this).Closed+=((EventHandler<EventArgs>)delegate
			{
				Path = ((TextControl)textbox).Text;
			});
		}
	}
}