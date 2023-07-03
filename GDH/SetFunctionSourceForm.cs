using System;
using System.Collections.ObjectModel;
using Eto.Drawing;
using Eto.Forms;
using Rhino.Resources;
using Rhino.Runtime;
using Rhino.UI.Controls;

namespace GDH
{
	internal class SetFunctionSourceForm : Dialog<bool>
	{
		public string Path { get; set; }

		public string Name { get; set; }

		public SetFunctionSourceForm(string currentPath, string currentName)
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Expected O, but got Unknown
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Expected O, but got Unknown
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Expected O, but got Unknown
			//IL_011d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0122: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Expected O, but got Unknown
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Expected O, but got Unknown
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_018c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0196: Expected O, but got Unknown
			//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ca: Expected O, but got Unknown
			//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ed: Expected O, but got Unknown
			//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Expected O, but got Unknown
			//IL_020c: Unknown result type (might be due to invalid IL or missing references)
			//IL_022d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0232: Unknown result type (might be due to invalid IL or missing references)
			//IL_0239: Unknown result type (might be due to invalid IL or missing references)
			//IL_023b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0240: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0264: Unknown result type (might be due to invalid IL or missing references)
			//IL_0277: Expected O, but got Unknown
			//IL_0278: Unknown result type (might be due to invalid IL or missing references)
			//IL_027d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0280: Unknown result type (might be due to invalid IL or missing references)
			//IL_028a: Unknown result type (might be due to invalid IL or missing references)
			//IL_028d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0297: Unknown result type (might be due to invalid IL or missing references)
			//IL_029d: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bf: Expected O, but got Unknown
			//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e8: Expected O, but got Unknown
			//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fe: Expected O, but got Unknown
			Path = currentPath;
			Name = currentName;
			((Window)this).Title = ("Set Function Source");
			TextBox srcName_Textbox = new TextBox();
			((Control)srcName_Textbox).Size = (new Size(250, -1));
			srcName_Textbox.PlaceholderText = ("Nickname");
			if (!string.IsNullOrWhiteSpace(Name))
			{
				((TextControl)srcName_Textbox).Text = (Name);
			}
			((Control)srcName_Textbox).Focus();
			TextBox srcPath_Textbox = new TextBox();
			((Control)srcPath_Textbox).Size = (new Size(250, -1));
			srcPath_Textbox.PlaceholderText = ("URL or Path");
			if (!string.IsNullOrWhiteSpace(Path))
			{
				((TextControl)srcPath_Textbox).Text = (Path);
			}
			bool onWindows = HostUtils.RunningOnWindows;
			Button val = new Button();
			((TextControl)val).Text = (onWindows ? "OK" : "Apply");
			((Dialog)this).DefaultButton = (val);
			((Dialog)this).DefaultButton.Click += ((EventHandler<EventArgs>)delegate
			{
				//IL_0037: Unknown result type (might be due to invalid IL or missing references)
				//IL_003d: Invalid comparison between Unknown and I4
				if ((!string.IsNullOrEmpty(((TextControl)srcName_Textbox).Text) && !string.IsNullOrEmpty(((TextControl)srcPath_Textbox).Text)) || (int)MessageBox.Show((Control)(object)this, "Nickname and path are required fields.", "Required Field Missing", (MessageBoxButtons)0, (MessageBoxType)0, (MessageBoxDefaultButton)1) != 1)
				{
					base.Close(true);
				}
			});
			Button val2 = new Button();
			((TextControl)val2).Text = ("C&ancel");
			((Dialog)this).AbortButton = (val2);
			((Dialog)this).AbortButton.Click += ((EventHandler<EventArgs>)delegate
			{
				base.Close(false);
			});
			TableLayout buttons = new TableLayout();
			if (onWindows)
			{
				buttons.Spacing = (new Size(5, 5));
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
			StackLayout val3 = new StackLayout();
			val3.Orientation = ((Orientation)0);
			val3.Items.Add(srcName_Textbox);
			StackLayout srcNameRow = val3;
			ImageButton filePickButton = new ImageButton();
			filePickButton.Image = ((Image)(object)Assets.Rhino.Eto.Bitmaps
				.TryGet(ResourceIds.FolderopenPng, new Size(24, 24)));
			filePickButton.Click += ((EventHandler<EventArgs>)delegate
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Expected O, but got Unknown
				//IL_001a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0020: Invalid comparison between Unknown and I4
				SelectFolderDialog val8 = new SelectFolderDialog();
				Window val9 = (Window)(object)(onWindows ? this : null);
				if ((int)((CommonDialog)val8).ShowDialog(val9) == 1)
				{
					((TextControl)srcPath_Textbox).Text = (val8.Directory);
				}
			});
			StackLayout val4 = new StackLayout();
			val4.Orientation = ((Orientation)0);
			Size spacing = buttons.Spacing;
			val4.Spacing = (((Size)(spacing)).Width);
			val4.Items.Add(srcPath_Textbox);
			val4.Items.Add(filePickButton);
			StackLayout srcPathRow = val4;
			TableLayout val5 = new TableLayout();
			val5.Padding = (new Padding(10));
			val5.Spacing = (new Size(5, 5));
			Collection<TableRow> rows = val5.Rows;
			TableRow val6 = new TableRow();
			val6.ScaleHeight = (true);
			val6.Cells.Add(srcNameRow);
			rows.Add(val6);
			Collection<TableRow> rows2 = val5.Rows;
			TableRow val7 = new TableRow();
			val7.ScaleHeight = (true);
			val7.Cells.Add(srcPathRow);
			rows2.Add(val7);
			val5.Rows.Add(buttons);
			((Panel)this).Content = ((Control)val5);
			((Window)this).Closed += ((EventHandler<EventArgs>)delegate
			{
				Path = ((TextControl)srcPath_Textbox).Text;
				Name = ((TextControl)srcName_Textbox).Text;
			});
		}
	}
}