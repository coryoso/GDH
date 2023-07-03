using System.Collections.Generic;
using System.Windows.Forms;

namespace GDH
{
	public class UriFunctionPathInfo
	{
		public List<UriFunctionPathInfo> Paths = new List<UriFunctionPathInfo>();

		public string EndPoint { get; set; }

		public string FullPath { get; set; }

		public bool IsFolder { get; set; }

		public bool isRoot { get; set; }

		public string RootURL { get; set; }

		public UriFunctionPathInfo(string _endpoint, bool _isfolder)
		{
			EndPoint = _endpoint;
			IsFolder = _isfolder;
		}

		public void BuildMenus(ToolStripMenuItem ti, MouseEventHandler click_ev)
		{
			if (Paths.Count == 0)
			{
				string ep2 = EndPoint;
				if (ep2.StartsWith("/"))
				{
					ep2 = ep2.Substring(1);
				}
				ToolStripItem item2 = ti.DropDownItems.Add(ep2);
				item2.MouseDown += click_ev;
				if (RootURL.EndsWith("/"))
				{
					RootURL = RootURL.TrimEnd('/');
				}
				if (!FullPath.StartsWith("/"))
				{
					FullPath = FullPath.Insert(0, "/");
				}
				string fullURL = (string)(item2.Tag = RootURL + FullPath);
				return;
			}
			ToolStripMenuItem item;
			if (isRoot)
			{
				item = ti;
			}
			else
			{
				string ep = EndPoint;
				if (ep.StartsWith("/"))
				{
					ep = ep.Substring(1);
				}
				item = new ToolStripMenuItem(ep);
				ti.DropDownItems.Add(item);
			}
			foreach (UriFunctionPathInfo path in Paths)
			{
				path.BuildMenus(item, click_ev);
			}
		}
	}
}