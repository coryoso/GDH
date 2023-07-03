using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using GDH;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Rhino.Runtime;

namespace GDH
{
	public static class HopsFunctionMgr
	{
		private static Image _funcMgr24Icon;

		private static Image _funcMgr48Icon;

		private static Image _deleteIcon;

		private static Image _addIcon;

		private static Image _editIcon;

		private static HttpClient _httpClient;

		private static GDHComponent Parent { get; set; }

		private static ThumbnailViewer Viewer { get; set; }

		public static HttpClient HttpClient
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Expected O, but got Unknown
				if (_httpClient == null)
				{
					_httpClient = new HttpClient();
				}
				return _httpClient;
			}
		}

		public static ToolStripMenuItem AddFunctionMgrControl(GDHComponent _parent)
		{
			HopsAppSettings.InitFunctionSources();
			if (HopsAppSettings.FunctionSources.Count <= 0)
			{
				return null;
			}
			Parent = _parent;
			ToolStripMenuItem mainMenu = new ToolStripMenuItem("Available Functions", null, null, "Available Functions");
			mainMenu.DropDownItems.Clear();
			foreach (FunctionSourceRow row in HopsAppSettings.FunctionSources)
			{
				ToolStripMenuItem menuItem = new ToolStripMenuItem(row.SourceName, null, null, row.SourceName);
				GenerateFunctionPathMenu(menuItem, row);
				if (menuItem.DropDownItems.Count > 0)
				{
					mainMenu.DropDownItems.Add(menuItem);
				}
			}
			InitThumbnailViewer();
			return mainMenu;
		}

		private static void InitThumbnailViewer()
		{
			if (Viewer == null)
			{
				Viewer = new ThumbnailViewer();
			}
			Viewer.Owner = (Form)(object)Instances.DocumentEditor;
			Viewer.StartPosition = FormStartPosition.Manual;
			Viewer.Visible = false;
		}

		private static void GenerateFunctionPathMenu(ToolStripMenuItem menu, FunctionSourceRow row)
		{
			if (string.IsNullOrEmpty(row.SourceName) || string.IsNullOrEmpty(row.SourcePath))
			{
				return;
			}
			if (row.SourcePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					Task<HttpResponseMessage> getTask = HttpClient.GetAsync(row.SourcePath);
					if (getTask != null)
					{
						string stringResult = getTask.Result.Content.ReadAsStringAsync().Result;
						if (!string.IsNullOrEmpty(stringResult))
						{
							FunctionMgr_Schema[] response = JsonConvert.DeserializeObject<FunctionMgr_Schema[]>(stringResult);
							if (response != null)
							{
								UriFunctionPathInfo functionPaths2 = new UriFunctionPathInfo(row.SourcePath, _isfolder: true);
								functionPaths2.isRoot = true;
								functionPaths2.RootURL = row.SourcePath;
								if (!string.IsNullOrEmpty(response[0].Uri))
								{
									FunctionMgr_Schema[] array = response;
									foreach (FunctionMgr_Schema obj2 in array)
									{
										SeekFunctionMenuDirs(functionPaths2, obj2.Uri, obj2.Uri, row);
									}
								}
								else if (!string.IsNullOrEmpty(response[0].Name))
								{
									FunctionMgr_Schema[] array = response;
									foreach (FunctionMgr_Schema obj in array)
									{
										SeekFunctionMenuDirs(functionPaths2, "/" + obj.Name, "/" + obj.Name, row);
									}
								}
								if (functionPaths2.Paths.Count != 0)
								{
									functionPaths2.BuildMenus(menu, tsm_UriClick);
								}
							}
						}
					}
					return;
				}
				catch (Exception)
				{
					return;
				}
			}
			if (Directory.Exists(row.SourcePath))
			{
				FunctionPathInfo functionPaths = new FunctionPathInfo(row.SourcePath, _isfolder: true);
				functionPaths.isRoot = true;
				SeekFunctionMenuDirs(functionPaths);
				if (functionPaths.Paths.Count != 0)
				{
					functionPaths.BuildMenus(menu, tsm_FileClick, tsm_HoverEnter, tsm_HoverExit);
					functionPaths.RemoveEmptyMenuItems(menu, tsm_FileClick, tsm_HoverEnter, tsm_HoverExit);
				}
			}
		}

		public static void SeekFunctionMenuDirs(UriFunctionPathInfo path, string uri, string fullpath, FunctionSourceRow row)
		{
			if (path == null || string.IsNullOrEmpty(uri))
			{
				return;
			}
			string[] endpoints = uri.Split(new char[1] { '/' }, 2);
			if (!string.IsNullOrEmpty(endpoints[1]))
			{
				if (endpoints[1].Contains("/"))
				{
					string[] subendpoints = endpoints[1].Split(new char[1] { '/' }, 2);
					UriFunctionPathInfo functionPath2 = new UriFunctionPathInfo("/" + subendpoints[0], _isfolder: true);
					functionPath2.RootURL = row.SourcePath;
					path.Paths.Add(functionPath2);
					SeekFunctionMenuDirs(functionPath2, "/" + subendpoints[1], fullpath, row);
				}
				else
				{
					UriFunctionPathInfo functionPath = new UriFunctionPathInfo("/" + endpoints[1], _isfolder: false);
					functionPath.RootURL = row.SourcePath;
					functionPath.FullPath = fullpath;
					path.Paths.Add(functionPath);
				}
			}
		}

		public static void SeekFunctionMenuDirs(FunctionPathInfo path)
		{
			if (path != null && path.IsValid())
			{
				string[] files = Directory.GetFiles(path.FullPath);
				for (int i = 0; i < files.Length; i++)
				{
					FunctionPathInfo filePath = new FunctionPathInfo(files[i], _isfolder: false);
					path.Paths.Add(filePath);
				}
				files = Directory.GetDirectories(path.FullPath);
				for (int i = 0; i < files.Length; i++)
				{
					FunctionPathInfo subDirPath = new FunctionPathInfo(files[i], _isfolder: true);
					path.Paths.Add(subDirPath);
					SeekFunctionMenuDirs(subDirPath);
				}
			}
		}

		private static void tsm_HoverEnter(object sender, EventArgs e)
		{
			if (sender is ToolStripMenuItem)
			{
				ToolStripMenuItem ti = sender as ToolStripMenuItem;
				Bitmap thumbnail = GH_DocumentIO.GetDocumentThumbnail(ti.Name);
				if (Viewer != null && thumbnail != null && ti.Owner != null && HostUtils.RunningOnWindows)
				{
					Point point = ti.Owner.PointToScreen(new Point(ti.Width + 4, 0));
					Viewer.Location = point;
					Viewer.pictureBox.Image = thumbnail;
					Viewer.Show();
				}
			}
		}

		private static void tsm_HoverExit(object sender, EventArgs e)
		{
			if (Viewer != null && Viewer.Visible)
			{
				Viewer.Hide();
			}
		}

		private static void tsm_FileClick(object sender, MouseEventArgs e)
		{
			if (!(sender is ToolStripItem))
			{
				return;
			}
			ToolStripItem ti = sender as ToolStripItem;
			if (Parent == null)
			{
				return;
			}
			switch (e.Button)
			{
				case MouseButtons.Left:
					Parent.RemoteDefinitionLocation = ti.Name;
					if (Instances.ActiveCanvas.Document != null)
					{
						Instances.ActiveCanvas.Document.ExpireSolution();
					}
					break;
				case MouseButtons.Right:
					try
					{
						Instances.DocumentEditor.ScriptAccess_OpenDocument(ti.Name);
						break;
					}
					catch (Exception)
					{
						break;
					}
			}
		}

		private static void tsm_UriClick(object sender, MouseEventArgs e)
		{
			if (!(sender is ToolStripItem))
			{
				return;
			}
			ToolStripItem ti = sender as ToolStripItem;
			if (Parent != null)
			{
				Parent.RemoteDefinitionLocation = ti.Tag as string;
				if (Instances.ActiveCanvas.Document != null)
				{
					Instances.ActiveCanvas.Document.ExpireSolution();
				}
			}
		}

		public static Image FuncMgr24Icon()
		{
			if (_funcMgr24Icon == null)
			{
				_funcMgr24Icon = Image.FromStream(typeof(GDHComponent).Assembly.GetManifestResourceStream("Hops.resources.Hops_Function_Mgr_24x24.png"));
			}
			return _funcMgr24Icon;
		}

		public static Image FuncMgr48Icon()
		{
			if (_funcMgr48Icon == null)
			{
				_funcMgr48Icon = Image.FromStream(typeof(GDHComponent).Assembly.GetManifestResourceStream("Hops.resources.Hops_Function_Mgr_48x48.png"));
			}
			return _funcMgr48Icon;
		}

		public static Image DeleteIcon()
		{
			if (_deleteIcon == null)
			{
				_deleteIcon = Image.FromStream(typeof(GDHComponent).Assembly.GetManifestResourceStream("Hops.resources.Close_Toolbar_Active_20x20.png"));
			}
			return _deleteIcon;
		}

		public static Image AddIcon()
		{
			if (_addIcon == null)
			{
				_addIcon = Image.FromStream(typeof(GDHComponent).Assembly.GetManifestResourceStream("Hops.resources.Open_Toolbar_Active_20x20.png"));
			}
			return _addIcon;
		}

		public static Image EditIcon()
		{
			if (_editIcon == null)
			{
				_editIcon = Image.FromStream(typeof(GDHComponent).Assembly.GetManifestResourceStream("Hops.resources.edit_16x16.png"));
			}
			return _editIcon;
		}
	}
}