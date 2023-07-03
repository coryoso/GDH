using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms; 
using GH_IO.Serialization;
using GH_IO.Types;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Hops;
using Newtonsoft.Json;
using Resthopper.IO;
using Rhino;
using Rhino.Geometry;
using Rhino.Runtime;

using SaveFileDialog = Eto.Forms.SaveFileDialog;
using MessageBox = Eto.Forms.MessageBox;
using FileFilter = Eto.Forms.FileFilter;
using Eto.Forms;
using OpenFileDialog = Eto.Forms.OpenFileDialog;

namespace GDH
{
	public class GDHComponent : GH_TaskCapableComponent<Schema>, IGH_VariableParameterComponent
	{
		/// <summary>
		/// Each implementation of GH_Component must provide a public 
		/// constructor without any arguments.
		/// Category represents the Tab in which the component will appear, 
		/// Subcategory the panel. If you use non-existing tab or panel names, 
		/// new tabs/panels will automatically be created.
		/// </summary>
		private class ComponentAttributes : GH_ComponentAttributes
		{
			private GDHComponent _component;

			public ComponentAttributes(GDHComponent parentComponent)
				: base((IGH_Component)(object)parentComponent)
			{
				_component = parentComponent;
			}

			protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
			{
				//IL_0003: Unknown result type (might be due to invalid IL or missing references)
				//IL_0009: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Invalid comparison between Unknown and I4
				base.Render(canvas, graphics, channel);
				if ((int)channel == 20 && GH_Canvas.ZoomFadeMedium > 0 && !string.IsNullOrWhiteSpace(_component.RemoteDefinitionLocation))
				{
					RenderHop(graphics, GH_Canvas.ZoomFadeMedium, new PointF(((GH_Attributes<IGH_Component>)(object)this).Bounds.Right, ((GH_Attributes<IGH_Component>)(object)this).Bounds.Bottom));
				}
			}

			private void RenderHop(Graphics graphics, int alpha, PointF anchor)
			{
				RectangleF boxHops = new RectangleF(anchor.X - 16f, anchor.Y - 8f, 16f, 16f);
				Bitmap bmp = Hops48Icon();
				graphics.DrawImage(bmp, boxHops);
			}

			public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
			{
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				try
				{
					_component.ShowSetDefinitionUi();
				}
				catch (Exception ex)
				{
					((GH_ActiveObject)_component).AddRuntimeMessage((GH_RuntimeMessageLevel)20, ex.Message);
				}
				return ((GH_Attributes<IGH_Component>)this).RespondToMouseDoubleClick(sender, e);
			}
		}

		private int _majorVersion;

		private int _minorVersion = 1;

		private RemoteDefinition _remoteDefinition;

		private bool _cacheResultsInMemory = true;

		private bool _cacheResultsOnServer = true;

		private bool _remoteDefinitionRequiresRebuild;

		private bool _synchronous = true;

		private bool _showEnabledInput;

		private bool _enabledThisSolve = true;

		private bool _showPathInput;

		private int _iteration;

		private SolveDataList _workingSolveList;

		private int _solveSerialNumber;

		private int _solveRecursionLevel;

		private Schema _lastCreatedSchema;

		private static bool _isHeadless;

		private static int _currentSolveSerialNumber;

		private bool _solvedCallback;

		private HTTPRecord _httpRecord;

		private const string TagVersion = "RemoteSolveVersion";

		private const string TagPath = "RemoteDefinitionLocation";

		private const string TagCacheResultsOnServer = "CacheSolveResults";

		private const string TagCacheResultsInMemory = "CacheResultsInMemory";

		private const string TagSynchronousSolve = "SynchronousSolve";

		private const string TagShowEnabled = "ShowInput_Enabled";

		private const string TagShowPath = "ShowInput_Path";

		private const string TagInternalizeDefinitionFlag = "InternalizeFlag";

		private const string TagInternalizeDefinition = "InternalizeDefinition";

		private static Bitmap _hops24Icon;

		private static Bitmap _hops48Icon;

		private string _tempPath;

		public override Guid ComponentGuid => ((object)this).GetType().GUID;

		public override GH_Exposure Exposure => (GH_Exposure)8;

		public int SolveSerialNumber => _solveSerialNumber;

		public HTTPRecord HTTPRecord
		{
			get
			{
				if (_httpRecord == null)
				{
					_httpRecord = new HTTPRecord();
				}
				return _httpRecord;
			}
		}

		protected override Bitmap Icon => Hops24Icon();

		public string RemoteDefinitionLocation
		{
			get
			{
				if (_remoteDefinition != null)
				{
					return Path.GetFileName(_remoteDefinition.Path);
				}
				return string.Empty;
			}
			set
			{
				if (_remoteDefinition != null)
				{
					_remoteDefinition.Dispose();
					_remoteDefinition = null;
				}
				if (!string.IsNullOrWhiteSpace(value))
				{
					_remoteDefinition = RemoteDefinition.Create(value, this);
					DefineInputsAndOutputs();
				}
			}
		}

		static GDHComponent()
		{
			_isHeadless = false;
			_currentSolveSerialNumber = 1;
			if (HostUtils.RunningOnWindows && !RhinoApp.IsRunningHeadless && HopsAppSettings.Servers.Length == 0 && HopsAppSettings.LaunchWorkerAtStart)
			{
				Servers.StartServerOnLaunch();
			}
		}

		public GDHComponent()
			: base("CoolHops", "Hops", "Solve an external definition using Rhino Compute", "Params", "Util")
		{
			_isHeadless = RhinoApp.IsRunningHeadless;
		}

		protected override string HtmlHelp_Source()
		{
			return "GOTO:https://developer.rhino3d.com/guides/compute/hops-component/";
		}

		protected override void RegisterInputParams(GH_InputParamManager pManager)
		{
		}

		protected override void RegisterOutputParams(GH_OutputParamManager pManager)
		{
		}

		protected override void BeforeSolveInstance()
		{
			this.Message = "";
			_enabledThisSolve = true;
			_lastCreatedSchema = null;
			_solveRecursionLevel = 0;
			GH_Document doc = base.OnPingDocument();
			//GH_Document doc = ((GH_DocumentObject)this).OnPingDocument();
			if (_isHeadless && doc != null)
			{
				_solveRecursionLevel = doc.ConstantServer["ComputeRecursionLevel"]._Int;
			}
			if (!_solvedCallback)
			{
				_solveSerialNumber = _currentSolveSerialNumber++;
				if (_workingSolveList != null)
				{
					_workingSolveList.Canceled = true;
				}
				_workingSolveList = new SolveDataList(_solveSerialNumber, this, _remoteDefinition, _cacheResultsInMemory);
			}
			base.BeforeSolveInstance();
		}

		public void OnWorkingListComplete()
		{
			_solvedCallback = true;
			if (_workingSolveList.SolvedFor(_solveSerialNumber))
			{
				((GH_DocumentObject)this).ExpireSolution(true);
			}
			_solvedCallback = false;
		}

		protected override void SolveInstance(IGH_DataAccess DA)
		{
			if (String.IsNullOrEmpty(HopsAppSettings.GoogleDrivePath))
			{
				((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)20, $"Pls setup gdrive path");
			}
			if (!_enabledThisSolve)
			{
				return;
			}
			_iteration++;
			if (_isHeadless && _solveRecursionLevel > HopsAppSettings.RecursionLimit)
			{
				((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)20, $"Hops recursion level beyond limit of {HopsAppSettings.RecursionLimit}. Please help us understand why you need this by emailing steve@mcneel.com");
				return;
			}
			if (_showPathInput && DA.Iteration == 0)
			{
				string path = "";
				if (!DA.GetData<string>("_Path", ref path))
				{
					((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)10, "No URL or path defined for definition");
					return;
				}
				if (!string.Equals(path, RemoteDefinitionLocation))
				{
					RebuildWithNewPathAndRecompute(path);
					return;
				}
			}
			if (string.IsNullOrWhiteSpace(RemoteDefinitionLocation) && _remoteDefinition?.InternalizedDefinition == null)
			{
				((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)10, "No URL or path defined for definition");
				return;
			}
			if (_showEnabledInput && DA.Iteration == 0)
			{
				bool enabled = true;
				if (DA.GetData<bool>("_Enabled", ref enabled) && !enabled)
				{
					_enabledThisSolve = false;
					return;
				}
			}
			if (base.InPreSolve)
			{
				if (_workingSolveList.SolvedFor(_solveSerialNumber))
				{
					Task<Schema> solvedTask = Task.FromResult(_workingSolveList.SolvedSchema(DA.Iteration));
					base.TaskList.Add(solvedTask);
					return;
				}
				List<string> warnings2;
				List<string> errors2;
				Schema inputSchema2 = _remoteDefinition.CreateSolveInput(DA, _cacheResultsOnServer, _solveRecursionLevel, out warnings2, out errors2);
				if (warnings2 != null && warnings2.Count > 0)
				{
					foreach (string warning2 in warnings2)
					{
						((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)10, warning2);
					}
					return;
				}
				if (errors2 != null && errors2.Count > 0)
				{
					foreach (string error2 in errors2)
					{
						((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)20, error2);
					}
					return;
				}
				if (inputSchema2 != null)
				{
					if (_lastCreatedSchema == null)
					{
						_lastCreatedSchema = inputSchema2;
					}
					_workingSolveList.Add(inputSchema2);
				}
				return;
			}
			if (base.TaskList.Count == 0)
			{
				_workingSolveList.StartSolving(_synchronous);
				if (!_synchronous)
				{
					((GH_Component)this).Message = "solving...";
					return;
				}
				for (int i = 0; i < _workingSolveList.Count; i++)
				{
					Schema output = _workingSolveList.SolvedSchema(i);
					base.TaskList.Add(Task.FromResult(output));
				}
			}
			Schema schema = default(Schema);
			if (!base.GetSolveResults(DA, out schema))
			{
				List<string> warnings;
				List<string> errors;
				Schema inputSchema = _remoteDefinition.CreateSolveInput(DA, _cacheResultsOnServer, _solveRecursionLevel, out warnings, out errors);
				if (warnings != null && warnings.Count > 0)
				{
					foreach (string warning in warnings)
					{
						((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)10, warning);
					}
					return;
				}
				if (errors != null && errors.Count > 0)
				{
					foreach (string error in errors)
					{
						((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)20, error);
					}
					return;
				}
				if (inputSchema != null)
				{
					schema = _remoteDefinition.Solve(inputSchema, _cacheResultsInMemory);
					if (_lastCreatedSchema == null)
					{
						_lastCreatedSchema = inputSchema;
					}
				}
				else
				{
					schema = null;
				}
			}
			if (DA.Iteration == 0)
			{
				foreach (IGH_Param item in this.Params.Output)
				{
					((IGH_ActiveObject)item).ClearData();
				}
			}
			if (schema != null)
			{
				_remoteDefinition.SetComponentOutputs(schema, DA, this.Params.Output, this);
			}
		}

		public override bool Write(GH_IWriter writer)
		{
			bool num = base.Write(writer);
			if (num)
			{
				writer.SetVersion("RemoteSolveVersion", _majorVersion, _minorVersion, 0);
				writer.SetString("RemoteDefinitionLocation", RemoteDefinitionLocation);
				writer.SetBoolean("CacheSolveResults", _cacheResultsOnServer);
				writer.SetBoolean("CacheResultsInMemory", _cacheResultsInMemory);
				writer.SetBoolean("SynchronousSolve", _synchronous);
				writer.SetBoolean("ShowInput_Enabled", _showEnabledInput);
				writer.SetBoolean("ShowInput_Path", _showPathInput);
				if (_remoteDefinition?.InternalizedDefinition != null)
				{
					writer.SetByteArray("InternalizeDefinition", _remoteDefinition.InternalizedDefinition);
				}
			}
			return num;
		}

		public override bool Read(GH_IReader reader)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			bool rc = base.Read(reader);
			if (rc)
			{
				GH_Version version = reader.GetVersion("RemoteSolveVersion");
				_majorVersion = version.major;
				_minorVersion = version.minor;
				string path = reader.GetString("RemoteDefinitionLocation");
				bool cacheResults = _cacheResultsOnServer;
				if (reader.TryGetBoolean("CacheSolveResults", ref cacheResults))
				{
					_cacheResultsOnServer = cacheResults;
				}
				cacheResults = _cacheResultsInMemory;
				if (reader.TryGetBoolean("CacheResultsInMemory", ref cacheResults))
				{
					_cacheResultsInMemory = cacheResults;
				}
				bool synchronous = _synchronous;
				if (reader.TryGetBoolean("SynchronousSolve", ref synchronous))
				{
					_synchronous = synchronous;
				}
				bool showEnabled = _showEnabledInput;
				if (reader.TryGetBoolean("ShowInput_Enabled", ref showEnabled))
				{
					_showEnabledInput = showEnabled;
				}
				bool showPath = _showPathInput;
				if (reader.TryGetBoolean("ShowInput_Path", ref showPath))
				{
					_showPathInput = showPath;
				}
				if (reader.ItemExists("InternalizeDefinition"))
				{
					try
					{
						byte[] internalizedDefinition = reader.GetByteArray("InternalizeDefinition");
						if (_remoteDefinition == null)
						{
							_remoteDefinition = RemoteDefinition.Create(null, this);
						}
						_remoteDefinition.InternalizedDefinition = internalizedDefinition;
						_remoteDefinition._pathType = RemoteDefinition.PathType.InternalizedDefinition;
						_remoteDefinition.GetRemoteDescription();
					}
					catch (Exception ex)
					{
						((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)20, "Unable to deserialize internalized grasshopper definition. " + ex.Message);
					}
				}
				if (!string.IsNullOrWhiteSpace(path))
				{
					try
					{
						if (RemoteDefinition.GetPathType(path) == RemoteDefinition.PathType.GrasshopperDefinition && !File.Exists(path))
						{
							string directoryName = Path.GetDirectoryName(((GH_IChunk)reader).ArchiveLocation);
							string remoteFileName = Path.GetFileName(path);
							string filePath = Path.Combine(directoryName, remoteFileName);
							if (File.Exists(filePath))
							{
								path = filePath;
							}
						}
						RemoteDefinitionLocation = path;
						return rc;
					}
					catch (WebException)
					{
						return rc;
					}
				}
			}
			return rc;
		}

		public bool CanInsertParameter(GH_ParameterSide side, int index)
		{
			return false;
		}

		public bool CanRemoveParameter(GH_ParameterSide side, int index)
		{
			return false;
		}

		public IGH_Param CreateParameter(GH_ParameterSide side, int index)
		{
			return null;
		}

		public bool DestroyParameter(GH_ParameterSide side, int index)
		{
			return true;
		}

		public void VariableParameterMaintenance()
		{
		}

		private static Bitmap Hops24Icon()
		{
			if (_hops24Icon == null)
			{
				_hops24Icon = new Bitmap(typeof(GDHComponent).Assembly.GetManifestResourceStream("GDH.Resources.itech_24.png"));
			}
			return _hops24Icon;
		}

		public static Bitmap Hops48Icon()
		{
			if (_hops48Icon == null)
			{
				_hops48Icon = new Bitmap(typeof(GDHComponent).Assembly.GetManifestResourceStream("GDH.Resources.itech_48.png"));
			}
			return _hops48Icon;
		}

		public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
		{
			base.AppendAdditionalMenuItems(menu);
			for (int i = menu.Items.Count - 1; i >= 0; i--)
			{
				if (menu.Items[i].Text.Equals("parallel computing", StringComparison.OrdinalIgnoreCase))
				{
					menu.Items.RemoveAt(i);
				}
				else if (menu.Items[i].Text.Equals("variable parameters", StringComparison.OrdinalIgnoreCase))
				{
					menu.Items.RemoveAt(i);
				}
			}
			if (menu.Items[menu.Items.Count - 1] is ToolStripSeparator)
			{
				menu.Items.RemoveAt(menu.Items.Count - 1);
			}
			ToolStripMenuItem tsi = new ToolStripMenuItem("&Google Drive Path...", null, delegate
			{
				OpenFileDialog fileDialog = new OpenFileDialog();
				
				if (fileDialog.ShowDialog(Instances.EtoDocumentEditor) == Eto.Forms.DialogResult.Ok)
				{
					string lastDirectory = Path.GetDirectoryName(fileDialog.FileName);
					HopsAppSettings.GoogleDrivePath = lastDirectory;

					this.ClearRuntimeMessages();
				}
			});
			menu.Items.Add(tsi);

			menu.Items.Add(new ToolStripSeparator());
			tsi = new ToolStripMenuItem("&Component...", null, delegate
			{
				ShowSetDefinitionUi();
			});
			if (!_showPathInput)
			{
				tsi.Font = new Font(tsi.Font, FontStyle.Bold);
			}
			tsi.Enabled = !_showPathInput;
			menu.Items.Add(tsi);
			tsi = HopsFunctionMgr.AddFunctionMgrControl(this);
			if (tsi != null)
			{
				menu.Items.Add(tsi);
			}
			tsi = new ToolStripMenuItem("Internalize Definition", null, delegate
			{
				if (File.Exists(RemoteDefinitionLocation))
				{
					_remoteDefinition.InternalizeDefinition(RemoteDefinitionLocation);
					DefineInputsAndOutputs();
				}
			});
			tsi.ToolTipText = "Make the referenced definition permanent and clear any existing source paths";
			if (!File.Exists(RemoteDefinitionLocation))
			{
				tsi.Enabled = false;
			}
			menu.Items.Add(tsi);
			menu.Items.Add(new ToolStripSeparator());
			tsi = new ToolStripMenuItem("Show Input: Path", null, delegate
			{
				_showPathInput = !_showPathInput;
				DefineInputsAndOutputs();
			});
			tsi.ToolTipText = "Create input for path";
			tsi.Checked = _showPathInput;
			menu.Items.Add(tsi);
			tsi = new ToolStripMenuItem("Show Input: Enabled", null, delegate
			{
				_showEnabledInput = !_showEnabledInput;
				DefineInputsAndOutputs();
			});
			tsi.ToolTipText = "Create input for enabled";
			tsi.Checked = _showEnabledInput;
			menu.Items.Add(tsi);
			tsi = new ToolStripMenuItem("Asynchronous", null, delegate
			{
				_synchronous = !_synchronous;
			});
			tsi.ToolTipText = "Do not block while solving";
			tsi.Checked = !_synchronous;
			menu.Items.Add(tsi);
			tsi = new ToolStripMenuItem("Cache In Memory", null, delegate
			{
				_cacheResultsInMemory = !_cacheResultsInMemory;
			});
			tsi.ToolTipText = "Keep previous results in memory cache";
			tsi.Checked = _cacheResultsInMemory;
			menu.Items.Add(tsi);
			tsi = new ToolStripMenuItem("Cache On Server", null, delegate
			{
				_cacheResultsOnServer = !_cacheResultsOnServer;
			});
			tsi.ToolTipText = "Tell the compute server to cache results for reuse in the future";
			tsi.Checked = _cacheResultsOnServer;
			menu.Items.Add(tsi);
			ToolStripMenuItem exportTsi = new ToolStripMenuItem("Export");
			exportTsi.Enabled = _remoteDefinition != null;
			menu.Items.Add(exportTsi);
			tsi = new ToolStripMenuItem("Export python sample...", null, delegate
			{
				ExportAsPython();
			});
			exportTsi.DropDownItems.Add(tsi);
			ToolStripMenuItem restAPITsi = new ToolStripMenuItem("REST API");
			restAPITsi.Enabled = _remoteDefinition != null;
			exportTsi.DropDownItems.Add(restAPITsi);
			tsi = new ToolStripMenuItem("Last IO request...", null, delegate
			{
				ExportLastIORequest();
			});
			restAPITsi.DropDownItems.Add(tsi);
			tsi = new ToolStripMenuItem("Last IO response...", null, delegate
			{
				ExportLastIOResponse();
			});
			restAPITsi.DropDownItems.Add(tsi);
			tsi = new ToolStripMenuItem("Last Solve request...", null, delegate
			{
				ExportLastSolveRequest();
			});
			restAPITsi.DropDownItems.Add(tsi);
			tsi = new ToolStripMenuItem("Last Solve response...", null, delegate
			{
				ExportLastSolveResponse();
			});
			restAPITsi.DropDownItems.Add(tsi);
		}

		public override void CreateAttributes()
		{
			((GH_DocumentObject)this).Attributes = ((IGH_Attributes)(object)new ComponentAttributes(this));
		}

		private void ShowSetDefinitionUi()
		{
			SetDefinitionForm form = new SetDefinitionForm(RemoteDefinitionLocation);
			if (form.ShowModal(Instances.EtoDocumentEditor))
			{
				IGH_ObjectProxy comp = Instances.ComponentServer.FindObjectByName(form.Path, true, true);
				if (comp != null)
				{
					RemoteDefinitionLocation = comp.Guid.ToString();
				}
				else
				{
					RemoteDefinitionLocation = form.Path;
				}
			}
		}

		private void ExportAsPython()
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Expected O, but got Unknown
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Expected O, but got Unknown
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Invalid comparison between Unknown and I4
			if (_lastCreatedSchema == null)
			{
				Eto.Forms.MessageBox.Show("No input created. Run this component at least once", Eto.Forms.MessageBoxType.Error);
				return;
			}
			Eto.Forms.SaveFileDialog dlg = new Eto.Forms.SaveFileDialog();
			dlg.Filters.Add(new Eto.Forms.FileFilter("Python script", new string[1] { ".py" }));
			if (dlg.ShowDialog(Instances.EtoDocumentEditor) != Eto.Forms.DialogResult.Ok)
			{
				return;
			}
			string solveUrl = Servers.GetSolveUrl();
			if (solveUrl.EndsWith("grasshopper", StringComparison.InvariantCultureIgnoreCase))
			{
				solveUrl = solveUrl.Substring(0, solveUrl.Length - "grasshopper".Length);
			}
			StringBuilder sb = new StringBuilder();
			sb.Append("# pip install compute_rhino3d and rhino3dm\r\nimport compute_rhino3d.Util\r\nimport compute_rhino3d.Grasshopper as gh\r\nimport rhino3dm\r\nimport json\r\n\r\ncompute_rhino3d.Util.url = '");
			sb.Append(solveUrl);
			sb.Append("'\r\n\r\n# create DataTree for each input\r\ninput_trees = []\r\n");
			foreach (Resthopper.IO.DataTree<ResthopperObject> val in _lastCreatedSchema.Values)
			{
				sb.AppendLine("tree = gh.DataTree(\"" + val.ParamName + "\")");
				foreach (KeyValuePair<string, List<ResthopperObject>> kv in val.InnerTree)
				{
					List<string> values = new List<string>();
					foreach (ResthopperObject v in kv.Value)
					{
						values.Add(v.Data);
					}
					string innerData = JsonConvert.SerializeObject((object)values);
					sb.AppendLine("tree.Append([" + kv.Key + "], " + innerData + ")");
					sb.AppendLine("input_trees.append(tree)");
					sb.AppendLine();
				}
			}
			sb.AppendLine("output = gh.EvaluateDefinition('" + RemoteDefinitionLocation.Replace("\\", "\\\\") + "', input_trees)");
			sb.Append("errors = output['errors']\r\nif errors:\r\n    print('ERRORS')\r\n    for error in errors:\r\n        print(error)\r\nwarnings = output['warnings']\r\nif warnings:\r\n    print('WARNINGS')\r\n    for warning in warnings:\r\n        print(warning)\r\n\r\nvalues = output['values']\r\nfor value in values:\r\n    name = value['ParamName']\r\n    inner_tree = value['InnerTree']\r\n    print(name)\r\n    for path in inner_tree:\r\n        print(path)\r\n        values_at_path = inner_tree[path]\r\n        for value_at_path in values_at_path:\r\n            data = value_at_path['data']\r\n            if isinstance(data, str) and 'archive3dm' in data:\r\n                obj = rhino3dm.CommonObject.Decode(json.loads(data))\r\n                print(obj)\r\n            else:\r\n                print(data)\r\n");
			File.WriteAllText(dlg.FileName, sb.ToString());
		}

		private void ExportLastIORequest()
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected O, but got Unknown
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Expected O, but got Unknown
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Invalid comparison between Unknown and I4
			if (string.IsNullOrEmpty(HTTPRecord.IORequest))
			{
				MessageBox.Show("No IO request has been made. Run this component at least once", Eto.Forms.MessageBoxType.Error);
				return;
			}
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Filters.Add(new FileFilter("JSON file", new string[1] { ".json" }));
			if (dlg.ShowDialog(Instances.EtoDocumentEditor) == Eto.Forms.DialogResult.Ok)
			{
				File.WriteAllText(dlg.FileName, HTTPRecord.IORequest);
			}
		}

		private void ExportLastIOResponse()
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected O, but got Unknown
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Expected O, but got Unknown
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Invalid comparison between Unknown and I4
			if (string.IsNullOrEmpty(HTTPRecord.IOResponse))
			{
				MessageBox.Show("No IO response has been received. Run this component at least once", Eto.Forms.MessageBoxType.Error);
				return;
			}
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Filters.Add(new FileFilter("JSON file", new string[1] { ".json" }));
			if (dlg.ShowDialog(Instances.EtoDocumentEditor) == Eto.Forms.DialogResult.Ok)
			{
				File.WriteAllText(dlg.FileName, HTTPRecord.IOResponse);
			}
		}

		private void ExportLastSolveRequest()
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected O, but got Unknown
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Expected O, but got Unknown
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Invalid comparison between Unknown and I4
			if (string.IsNullOrEmpty(HTTPRecord.SolveRequest))
			{
				MessageBox.Show("No solve request has been made. Run this component at least once", Eto.Forms.MessageBoxType.Error);
				return;
			}
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Filters.Add(new FileFilter("JSON file", new string[1] { ".json" }));
			if (dlg.ShowDialog(Instances.EtoDocumentEditor) ==Eto.Forms.DialogResult.Ok)
			{
				File.WriteAllText(dlg.FileName, HTTPRecord.SolveRequest);
			}
		}

		private void ExportLastSolveResponse()
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected O, but got Unknown
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Expected O, but got Unknown
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Invalid comparison between Unknown and I4
			if (string.IsNullOrEmpty(HTTPRecord.SolveResponse))
			{
				MessageBox.Show("No solve response has been received. Run this component at least once", Eto.Forms.MessageBoxType.Error);
				return;
			}
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Filters.Add(new FileFilter("JSON file", new string[1] { ".json" }));
			if (dlg.ShowDialog(Instances.EtoDocumentEditor) == Eto.Forms.DialogResult.Ok)
			{
				File.WriteAllText(dlg.FileName, HTTPRecord.SolveResponse);
			}
		}

		private void RebuildWithNewPathAndRecompute(string path)
		{
			if (!string.Equals(path, RemoteDefinitionLocation))
			{
				_tempPath = path;
				RhinoApp.Idle += RebuildAfterSolution;
			}
		}

		private void RebuildAfterSolution(object sender, EventArgs e)
		{
			GH_Document doc = ((GH_DocumentObject)this).OnPingDocument();
			if (doc != null && doc.SolutionDepth == 0)
			{
				RhinoApp.Idle -= RebuildAfterSolution;
				RemoteDefinitionLocation = _tempPath;
				_tempPath = null;
			}
		}

		public override void CollectData()
		{
			base.CollectData();
			//((GH_Component)this).CollectData();
			if (_showPathInput && !string.IsNullOrWhiteSpace(RemoteDefinitionLocation) && !((GH_Component)this).Params.Input[0].VolatileData.IsEmpty)
			{
				GH_Path path = ((GH_Component)this).Params.Input[0].VolatileData.get_Path(0);
				string newPath = ((GH_Component)this).Params.Input[0].VolatileData.get_Branch(path)[0].ToString();
				if (!string.Equals(newPath, RemoteDefinitionLocation))
				{
					RebuildWithNewPathAndRecompute(newPath);
				}
			}
		}

		private void DefineInputsAndOutputs()
		{
			//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_052a: Unknown result type (might be due to invalid IL or missing references)
			//IL_052f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0733: Unknown result type (might be due to invalid IL or missing references)
			//IL_0752: Unknown result type (might be due to invalid IL or missing references)
			//IL_0768: Unknown result type (might be due to invalid IL or missing references)
			//IL_078a: Unknown result type (might be due to invalid IL or missing references)
			//IL_07a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_07b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_07cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_07e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_07f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_080e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0824: Unknown result type (might be due to invalid IL or missing references)
			//IL_0843: Unknown result type (might be due to invalid IL or missing references)
			//IL_0859: Unknown result type (might be due to invalid IL or missing references)
			//IL_0886: Unknown result type (might be due to invalid IL or missing references)
			//IL_08bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_08d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_08f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0909: Unknown result type (might be due to invalid IL or missing references)
			//IL_0933: Unknown result type (might be due to invalid IL or missing references)
			//IL_0949: Unknown result type (might be due to invalid IL or missing references)
			//IL_0957: Unknown result type (might be due to invalid IL or missing references)
			//IL_0970: Unknown result type (might be due to invalid IL or missing references)
			//IL_0986: Unknown result type (might be due to invalid IL or missing references)
			//IL_099c: Unknown result type (might be due to invalid IL or missing references)
			//IL_09c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_09dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a07: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a1d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a2b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a4d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a63: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a71: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a8a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0aa9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0abf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ae1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0af7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b0d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b20: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b33: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b4f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b62: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b70: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b86: Unknown result type (might be due to invalid IL or missing references)
			if (_remoteDefinition != null)
			{
				((GH_ActiveObject)this).ClearRuntimeMessages();
				Bitmap customIcon;
				string description = _remoteDefinition.GetDescription(out customIcon);
				if (_remoteDefinition.IsNotResponingUrl())
				{
					((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)20, "Unable to connect to server");
					(Instances.ActiveCanvas)?.Invalidate();
					return;
				}
				if (_remoteDefinition.IsInvalidUrl())
				{
					((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)20, "Path appears valid, but to something that is not Hops related");
					(Instances.ActiveCanvas)?.Invalidate();
					return;
				}
				if (HTTPRecord.IOResponseSchema != null && HTTPRecord.IOResponseSchema.Errors.Count > 0)
				{
					using List<string>.Enumerator enumerator = HTTPRecord.IOResponseSchema.Errors.GetEnumerator();
					if (enumerator.MoveNext())
					{
						string error = enumerator.Current;
						((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)20, error);
						(Instances.ActiveCanvas)?.Invalidate();
						return;
					}
				}
				if (HTTPRecord.IOResponseSchema != null && HTTPRecord.IOResponseSchema.Warnings.Count > 0)
				{
					foreach (string warning in HTTPRecord.IOResponseSchema.Warnings)
					{
						((GH_ActiveObject)this).AddRuntimeMessage((GH_RuntimeMessageLevel)10, warning);
					}
				}
				if (!string.IsNullOrWhiteSpace(description) && !((GH_InstanceDescription)this).Description.Equals(description))
				{
					((GH_InstanceDescription)this).Description = description;
				}
				Dictionary<string, Tuple<InputParamSchema, IGH_Param>> inputs = _remoteDefinition.GetInputParams();
				Dictionary<string, IGH_Param> outputs = _remoteDefinition.GetOutputParams();
				bool buildInputs = inputs != null;
				bool buildOutputs = outputs != null;
				Dictionary<string, List<IGH_Param>> inputSources = new Dictionary<string, List<IGH_Param>>();
				foreach (IGH_Param param9 in ((GH_Component)this).Params.Input)
				{
					inputSources.Add(((IGH_InstanceDescription)param9).Name, new List<IGH_Param>(param9.Sources));
				}
				Dictionary<string, List<IGH_Param>> outputRecipients = new Dictionary<string, List<IGH_Param>>();
				foreach (IGH_Param param8 in ((GH_Component)this).Params.Output)
				{
					outputRecipients.Add(((IGH_InstanceDescription)param8).Name, new List<IGH_Param>(param8.Recipients));
				}
				int inputCount = inputs?.Count ?? 0;
				if (_showEnabledInput)
				{
					inputCount++;
				}
				if (_showPathInput)
				{
					inputCount++;
				}
				InputParamSchema item;
				IGH_Param item2;
				if (_iteration == 0)
				{
					if (buildInputs && ((GH_Component)this).Params.Input.Count == inputCount)
					{
						IGH_Param[] array = ((GH_Component)this).Params.Input.ToArray();
						foreach (IGH_Param param7 in array)
						{
							if (!inputs.ContainsKey(((IGH_InstanceDescription)param7).Name))
							{
								buildInputs = true;
								break;
							}
							inputs[((IGH_InstanceDescription)param7).Name].Deconstruct(out item, out item2);
							InputParamSchema input2 = item;
							param7.Access = RemoteDefinition.AccessFromInput(input2);
						}
					}
					if (buildOutputs && ((GH_Component)this).Params.Output.Count == outputs.Count)
					{
						buildOutputs = false;
						IGH_Param[] array = ((GH_Component)this).Params.Output.ToArray();
						foreach (IGH_Param param6 in array)
						{
							if (!outputs.ContainsKey(((IGH_InstanceDescription)param6).Name))
							{
								buildOutputs = true;
								break;
							}
						}
					}
				}
				if (buildInputs)
				{
					IGH_Param[] array = ((GH_Component)this).Params.Input.ToArray();
					foreach (IGH_Param param5 in array)
					{
						((GH_Component)this).Params.UnregisterInputParameter(param5);
					}
				}
				if (buildOutputs)
				{
					IGH_Param[] array = ((GH_Component)this).Params.Output.ToArray();
					foreach (IGH_Param param4 in array)
					{
						((GH_Component)this).Params.UnregisterOutputParameter(param4);
					}
				}
				bool recompute = false;
				if (buildInputs && inputs != null)
				{
					bool containsEmptyDefaults = false;
					GH_InputParamManager mgr3 = CreateInputManager();
					if (_showPathInput)
					{
						int paramIndex4 = mgr3.AddTextParameter("_Path", "Path", "URL to remote process", (GH_ParamAccess)0);
						if (paramIndex4 >= 0 && inputSources.TryGetValue("_Path", out var rehookInputs3))
						{
							foreach (IGH_Param rehookInput3 in rehookInputs3)
							{
								((GH_Component)this).Params.Input[paramIndex4].AddSource(rehookInput3);
							}
						}
					}
					if (_showEnabledInput)
					{
						int paramIndex3 = mgr3.AddBooleanParameter("_Enabled", "Enabled", "Enabled state for solving", (GH_ParamAccess)0);
						if (paramIndex3 >= 0 && inputSources.TryGetValue("_Enabled", out var rehookInputs2))
						{
							foreach (IGH_Param rehookInput2 in rehookInputs2)
							{
								((GH_Component)this).Params.Input[paramIndex3].AddSource(rehookInput2);
							}
						}
					}
					foreach (KeyValuePair<string, Tuple<InputParamSchema, IGH_Param>> kv2 in inputs)
					{
						string name2 = kv2.Key;
						kv2.Value.Deconstruct(out item, out item2);
						InputParamSchema input = item;
						IGH_Param param3 = item2;
						GH_ParamAccess access = RemoteDefinition.AccessFromInput(input);
						string inputDescription = name2;
						if (!string.IsNullOrWhiteSpace(input.Description))
						{
							inputDescription = input.Description;
						}
						if (input.Default == null)
						{
							containsEmptyDefaults = true;
						}
						string nickname2 = name2;
						if (!string.IsNullOrWhiteSpace(input.Nickname))
						{
							nickname2 = input.Nickname;
						}
						int paramIndex2 = -1;
						if (!(param3 is Param_Arc))
						{
							if (param3 is Param_Boolean)
							{
								paramIndex2 = ((input.Default != null) ? mgr3.AddBooleanParameter(name2, nickname2, inputDescription, access, Convert.ToBoolean(input.Default)) : mgr3.AddBooleanParameter(name2, nickname2, inputDescription, access));
							}
							else if (!(param3 is Param_Box))
							{
								if (!(param3 is Param_Brep))
								{
									if (!(param3 is Param_Circle))
									{
										if (!(param3 is Param_Colour))
										{
											if (!(param3 is Param_Complex))
											{
												if (!(param3 is Param_Culture))
												{
													if (!(param3 is Param_Curve))
													{
														if (!(param3 is Param_Field))
														{
															if (param3 is Param_FilePath)
															{
																paramIndex2 = ((input.Default != null) ? mgr3.AddTextParameter(name2, nickname2, inputDescription, access, input.Default.ToString()) : mgr3.AddTextParameter(name2, nickname2, inputDescription, access));
															}
															else
															{
																if (param3 is Param_GenericObject)
																{
																	throw new Exception("generic param not supported");
																}
																if (!(param3 is Param_Geometry))
																{
																	if (param3 is Param_Group)
																	{
																		throw new Exception("group param not supported");
																	}
																	if (param3 is Param_Guid)
																	{
																		throw new Exception("guid param not supported");
																	}
																	if (param3 is Param_Integer)
																	{
																		paramIndex2 = ((input.Default != null) ? mgr3.AddIntegerParameter(name2, nickname2, inputDescription, access, Convert.ToInt32(input.Default)) : mgr3.AddIntegerParameter(name2, nickname2, inputDescription, access));
																	}
																	else if (!(param3 is Param_Interval))
																	{
																		if (!(param3 is Param_Interval2D))
																		{
																			if (param3 is Param_LatLonLocation)
																			{
																				throw new Exception("latlonlocation param not supported");
																			}
																			if (param3 is Param_Line)
																			{
																				paramIndex2 = ((input.Default != null) ? mgr3.AddLineParameter(name2, nickname2, inputDescription, access, JsonConvert.DeserializeObject<Line>(input.Default.ToString())) : mgr3.AddLineParameter(name2, nickname2, inputDescription, access));
																			}
																			else if (!(param3 is Param_Matrix))
																			{
																				if (!(param3 is Param_Mesh))
																				{
																					if (!(param3 is Param_MeshFace))
																					{
																						if (param3 is Param_MeshParameters)
																						{
																							throw new Exception("meshparameters paran not supported");
																						}
																						if (param3 is Param_Number)
																						{
																							paramIndex2 = ((input.Default != null) ? mgr3.AddNumberParameter(name2, nickname2, inputDescription, access, Convert.ToDouble(input.Default)) : mgr3.AddNumberParameter(name2, nickname2, inputDescription, access));
																						}
																						else if (param3 is Param_Plane)
																						{
																							paramIndex2 = ((input.Default != null) ? mgr3.AddPlaneParameter(name2, nickname2, inputDescription, access, JsonConvert.DeserializeObject<Plane>(input.Default.ToString())) : mgr3.AddPlaneParameter(name2, nickname2, inputDescription, access));
																						}
																						else if (param3 is Param_Point)
																						{
																							paramIndex2 = ((input.Default != null) ? mgr3.AddPointParameter(name2, nickname2, inputDescription, access, JsonConvert.DeserializeObject<Point3d>(input.Default.ToString())) : mgr3.AddPointParameter(name2, nickname2, inputDescription, access));
																						}
																						else if (!(param3 is Param_Rectangle))
																						{
																							if (param3 is Param_String)
																							{
																								paramIndex2 = ((input.Default != null) ? mgr3.AddTextParameter(name2, nickname2, inputDescription, access, input.Default.ToString()) : mgr3.AddTextParameter(name2, nickname2, inputDescription, access));
																							}
																							else if (!(param3 is Param_StructurePath))
																							{
																								if (!(param3 is Param_SubD))
																								{
																									if (!(param3 is Param_Surface))
																									{
																										if (!(param3 is Param_Time))
																										{
																											if (!(param3 is Param_Transform))
																											{
																												if (param3 is Param_Vector)
																												{
																													paramIndex2 = ((input.Default != null) ? mgr3.AddVectorParameter(name2, nickname2, inputDescription, access, JsonConvert.DeserializeObject<Vector3d>(input.Default.ToString())) : mgr3.AddVectorParameter(name2, nickname2, inputDescription, access));
																												}
																												else if (param3 is GH_NumberSlider)
																												{
																													paramIndex2 = mgr3.AddNumberParameter(name2, nickname2, inputDescription, access);
																												}
																											}
																											else
																											{
																												paramIndex2 = mgr3.AddTransformParameter(name2, nickname2, inputDescription, access);
																											}
																										}
																										else
																										{
																											paramIndex2 = mgr3.AddTimeParameter(name2, nickname2, inputDescription, access);
																										}
																									}
																									else
																									{
																										paramIndex2 = mgr3.AddSurfaceParameter(name2, nickname2, inputDescription, access);
																									}
																								}
																								else
																								{
																									paramIndex2 = mgr3.AddSubDParameter(name2, nickname2, inputDescription, access);
																								}
																							}
																							else
																							{
																								paramIndex2 = mgr3.AddPathParameter(name2, nickname2, inputDescription, access);
																							}
																						}
																						else
																						{
																							paramIndex2 = mgr3.AddRectangleParameter(name2, nickname2, inputDescription, access);
																						}
																					}
																					else
																					{
																						paramIndex2 = mgr3.AddMeshFaceParameter(name2, nickname2, inputDescription, access);
																					}
																				}
																				else
																				{
																					paramIndex2 = mgr3.AddMeshParameter(name2, nickname2, inputDescription, access);
																				}
																			}
																			else
																			{
																				paramIndex2 = mgr3.AddMatrixParameter(name2, nickname2, inputDescription, access);
																			}
																		}
																		else
																		{
																			paramIndex2 = mgr3.AddInterval2DParameter(name2, nickname2, inputDescription, access);
																		}
																	}
																	else
																	{
																		paramIndex2 = mgr3.AddIntervalParameter(name2, nickname2, inputDescription, access);
																	}
																}
																else
																{
																	paramIndex2 = mgr3.AddGeometryParameter(name2, nickname2, inputDescription, access);
																}
															}
														}
														else
														{
															paramIndex2 = mgr3.AddFieldParameter(name2, nickname2, inputDescription, access);
														}
													}
													else
													{
														paramIndex2 = mgr3.AddCurveParameter(name2, nickname2, inputDescription, access);
													}
												}
												else
												{
													paramIndex2 = mgr3.AddCultureParameter(name2, nickname2, inputDescription, access);
												}
											}
											else
											{
												paramIndex2 = mgr3.AddComplexNumberParameter(name2, nickname2, inputDescription, access);
											}
										}
										else
										{
											paramIndex2 = mgr3.AddColourParameter(name2, nickname2, inputDescription, access);
										}
									}
									else
									{
										paramIndex2 = mgr3.AddCircleParameter(name2, nickname2, inputDescription, access);
									}
								}
								else
								{
									paramIndex2 = mgr3.AddBrepParameter(name2, nickname2, inputDescription, access);
								}
							}
							else
							{
								paramIndex2 = mgr3.AddBoxParameter(name2, nickname2, inputDescription, access);
							}
						}
						else
						{
							paramIndex2 = mgr3.AddArcParameter(name2, nickname2, inputDescription, access);
						}
						if (paramIndex2 < 0 || !inputSources.TryGetValue(name2, out var rehookInputs))
						{
							continue;
						}
						foreach (IGH_Param rehookInput in rehookInputs)
						{
							((GH_Component)this).Params.Input[paramIndex2].AddSource(rehookInput);
						}
					}
					if (!containsEmptyDefaults)
					{
						recompute = true;
					}
				}
				if (buildOutputs && outputs != null)
				{
					GH_OutputParamManager mgr2 = CreateOutputManager();
					foreach (KeyValuePair<string, IGH_Param> kv in outputs)
					{
						string name = kv.Key;
						IGH_Param param2 = kv.Value;
						string nickname = name;
						if (!string.IsNullOrWhiteSpace(((IGH_InstanceDescription)param2).NickName))
						{
							nickname = ((IGH_InstanceDescription)param2).NickName;
						}
						string outputDescription = name;
						if (!string.IsNullOrWhiteSpace(((IGH_InstanceDescription)param2).Description))
						{
							outputDescription = ((IGH_InstanceDescription)param2).Description;
						}
						int paramIndex = -1;
						if (!(param2 is Param_Arc))
						{
							if (!(param2 is Param_Boolean))
							{
								if (!(param2 is Param_Box))
								{
									if (!(param2 is Param_Brep))
									{
										if (!(param2 is Param_Circle))
										{
											if (!(param2 is Param_Colour))
											{
												if (!(param2 is Param_Complex))
												{
													if (!(param2 is Param_Culture))
													{
														if (!(param2 is Param_Curve))
														{
															if (!(param2 is Param_Field))
															{
																if (!(param2 is Param_FilePath))
																{
																	if (!(param2 is Param_GenericObject))
																	{
																		if (!(param2 is Param_Geometry))
																		{
																			if (param2 is Param_Group)
																			{
																				throw new Exception("group param not supported");
																			}
																			if (param2 is Param_Guid)
																			{
																				throw new Exception("guid param not supported");
																			}
																			if (!(param2 is Param_Integer))
																			{
																				if (!(param2 is Param_Interval))
																				{
																					if (!(param2 is Param_Interval2D))
																					{
																						if (param2 is Param_LatLonLocation)
																						{
																							throw new Exception("latlonlocation param not supported");
																						}
																						if (!(param2 is Param_Line))
																						{
																							if (!(param2 is Param_Matrix))
																							{
																								if (!(param2 is Param_Mesh))
																								{
																									if (!(param2 is Param_MeshFace))
																									{
																										if (param2 is Param_MeshParameters)
																										{
																											throw new Exception("meshparameters param not supported");
																										}
																										if (!(param2 is Param_Number))
																										{
																											if (!(param2 is Param_Plane))
																											{
																												if (!(param2 is Param_Point))
																												{
																													if (!(param2 is Param_Rectangle))
																													{
																														if (!(param2 is Param_String))
																														{
																															if (!(param2 is Param_StructurePath))
																															{
																																if (!(param2 is Param_SubD))
																																{
																																	if (!(param2 is Param_Surface))
																																	{
																																		if (!(param2 is Param_Time))
																																		{
																																			if (!(param2 is Param_Transform))
																																			{
																																				if (param2 is Param_Vector)
																																				{
																																					paramIndex = mgr2.AddVectorParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																																				}
																																			}
																																			else
																																			{
																																				paramIndex = mgr2.AddTransformParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																																			}
																																		}
																																		else
																																		{
																																			paramIndex = mgr2.AddTimeParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																																		}
																																	}
																																	else
																																	{
																																		paramIndex = mgr2.AddSurfaceParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																																	}
																																}
																																else
																																{
																																	paramIndex = mgr2.AddSubDParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																																}
																															}
																															else
																															{
																																paramIndex = mgr2.AddPathParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																															}
																														}
																														else
																														{
																															paramIndex = mgr2.AddTextParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																														}
																													}
																													else
																													{
																														paramIndex = mgr2.AddRectangleParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																													}
																												}
																												else
																												{
																													paramIndex = mgr2.AddPointParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																												}
																											}
																											else
																											{
																												paramIndex = mgr2.AddPlaneParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																											}
																										}
																										else
																										{
																											paramIndex = mgr2.AddNumberParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																										}
																									}
																									else
																									{
																										paramIndex = mgr2.AddMeshFaceParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																									}
																								}
																								else
																								{
																									paramIndex = mgr2.AddMeshParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																								}
																							}
																							else
																							{
																								paramIndex = mgr2.AddMatrixParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																							}
																						}
																						else
																						{
																							paramIndex = mgr2.AddLineParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																						}
																					}
																					else
																					{
																						paramIndex = mgr2.AddInterval2DParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																					}
																				}
																				else
																				{
																					paramIndex = mgr2.AddIntervalParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																				}
																			}
																			else
																			{
																				paramIndex = mgr2.AddIntegerParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																			}
																		}
																		else
																		{
																			paramIndex = mgr2.AddGeometryParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																		}
																	}
																	else
																	{
																		paramIndex = mgr2.AddGenericParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																	}
																}
																else
																{
																	paramIndex = mgr2.AddTextParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
																}
															}
															else
															{
																paramIndex = mgr2.AddFieldParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
															}
														}
														else
														{
															paramIndex = mgr2.AddCurveParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
														}
													}
													else
													{
														paramIndex = mgr2.AddCultureParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
													}
												}
												else
												{
													paramIndex = mgr2.AddComplexNumberParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
												}
											}
											else
											{
												paramIndex = mgr2.AddColourParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
											}
										}
										else
										{
											paramIndex = mgr2.AddCircleParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
										}
									}
									else
									{
										paramIndex = mgr2.AddBrepParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
									}
								}
								else
								{
									paramIndex = mgr2.AddBoxParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
								}
							}
							else
							{
								paramIndex = mgr2.AddBooleanParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
							}
						}
						else
						{
							paramIndex = mgr2.AddArcParameter(name, nickname, outputDescription, (GH_ParamAccess)2);
						}
						if (paramIndex < 0 || !outputRecipients.TryGetValue(name, out var rehookOutputs))
						{
							continue;
						}
						foreach (IGH_Param item3 in rehookOutputs)
						{
							item3.AddSource(((GH_Component)this).Params.Output[paramIndex]);
						}
					}
				}
				if (customIcon != null)
				{
					((GH_DocumentObject)this).SetIconOverride(customIcon);
				}
				if (!(buildInputs || buildOutputs))
				{
					return;
				}
				((GH_Component)this).Params.OnParametersChanged();
				(Instances.ActiveCanvas)?.Invalidate();
				if (recompute)
				{
					GH_Document doc = ((GH_DocumentObject)this).OnPingDocument();
					if (doc != null)
					{
						doc.NewSolution(true);
					}
				}
			}
			else
			{
				IGH_Param[] array = ((GH_Component)this).Params.Input.ToArray();
				foreach (IGH_Param param in array)
				{
					((GH_Component)this).Params.UnregisterInputParameter(param);
				}
				GH_InputParamManager mgr = CreateInputManager();
				if (_showPathInput)
				{
					mgr.AddTextParameter("_Path", "Path", "URL to remote process", (GH_ParamAccess)0);
				}
				if (_showEnabledInput)
				{
					mgr.AddBooleanParameter("_Enabled", "Enabled", "Enabled state for solving", (GH_ParamAccess)0, true);
				}
				((GH_Component)this).Params.OnParametersChanged();
				(Instances.ActiveCanvas)?.Invalidate();
			}
		}

		private GH_InputParamManager CreateInputManager()
		{
			object obj = typeof(GH_InputParamManager).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0].Invoke(new object[1] { this });
			return (GH_InputParamManager)((obj is GH_InputParamManager) ? obj : null);
		}

		private GH_OutputParamManager CreateOutputManager()
		{
			object obj = typeof(GH_OutputParamManager).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0].Invoke(new object[1] { this });
			return (GH_OutputParamManager)((obj is GH_OutputParamManager) ? obj : null);
		}

		public void OnRemoteDefinitionChanged()
		{
			if (!_remoteDefinitionRequiresRebuild)
			{
				_remoteDefinitionRequiresRebuild = true;
				RhinoApp.Idle += ((EventHandler)RhinoApp_Idle);
			}
		}

		private void RhinoApp_Idle(object sender, EventArgs e)
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Invalid comparison between Unknown and I4
			if (!_remoteDefinitionRequiresRebuild)
			{ 
				RhinoApp.Idle -= ((EventHandler)RhinoApp_Idle);
				return;
			}
			GH_Document ghdoc = ((GH_DocumentObject)this).OnPingDocument();
			if (ghdoc == null || (int)ghdoc.SolutionState != 1)
			{
				RhinoApp.Idle -= ((EventHandler)RhinoApp_Idle);
				_remoteDefinitionRequiresRebuild = false;
				DefineInputsAndOutputs();
			}
		}
	}
}