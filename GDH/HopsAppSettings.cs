using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper;
using Rhino;
using Rhino.Runtime;

namespace GDH
{
	internal static class HopsAppSettings
	{
		private const string HOPS_SERVERS = "Hops:Servers";

		private const string HOPS_APIKEY = "Hops:ApiKey";

		private const string HOPS_HTTP_TIMEOUT = "Hops:HttpTimeout";

		private const string HIDE_WORKER_WINDOWS = "Hops:HideWorkerWindows";

		private const string LAUNCH_WORKER_AT_START = "Hops:LaunchWorkerAtStart";

		private const string LOCAL_WORKER_COUNT = "Hops:LocalWorkerCount";

		private const string MAX_CONCURRENT_REQUESTS = "Hops:MaxConcurrentRequests";

		private const string RECURSION_LIMIT = "Hops:RecursionLimit";

		private const string HOPS_FUNCTION_PATHS = "Hops:FunctionPaths";

		private const string HOPS_FUNCTION_NAMES = "Hops:FunctionNames";

		private const string HOPS_FUNCTION_SELECTED_STATE = "Hops:FunctionSelectedState";

		private static int _httpTimeout = 0;

		private static int _maxConcurrentRequests = 0;

		public static List<FunctionSourceRow> FunctionSources { get; set; } = new List<FunctionSourceRow>();


		public static bool ShowFunctionManager { get; set; } = true;


		public static string[] Servers
		{
			get
			{
				string serversSetting = Instances.Settings.GetValue("Hops:Servers", "");
				if (string.IsNullOrWhiteSpace(serversSetting))
				{
					return new string[0];
				}
				return serversSetting.Split('\n');
			}
			set
			{
				if (value == null)
				{
					Instances.Settings.SetValue("Hops:Servers", "");
				}
				else
				{
					StringBuilder sb = new StringBuilder();
					for (int i = 0; i < value.Length; i++)
					{
						string s = value[i].Trim();
						if (!string.IsNullOrEmpty(s))
						{
							if (sb.Length > 0)
							{
								sb.Append('\n');
							}
							sb.Append(s);
						}
					}
					Instances.Settings.SetValue("Hops:Servers", sb.ToString());
				}
				GDH.Servers.SettingsChanged();
			}
		}

		public static string GoogleDrivePath
		{
			get
			{
				string path = Instances.Settings.GetValue("GDH:GDrivePath", "");
				if (string.IsNullOrWhiteSpace(path))
				{
					return string.Empty;
				}
				return path;
			}
			set
			{
				Instances.Settings.SetValue("GDH:GDrivePath", value);
			}
		}

		public static string APIKey
		{
			get
			{
				string apiKey = Instances.Settings.GetValue("Hops:ApiKey", "");
				if (string.IsNullOrWhiteSpace(apiKey))
				{
					return string.Empty;
				}
				return apiKey;
			}
			set
			{
				if (value == null)
				{
					Instances.Settings.SetValue("Hops:ApiKey", "");
				}
				else
				{
					Instances.Settings.SetValue("Hops:ApiKey", value);
				}
			}
		}

		public static string[] FunctionSourcePaths
		{
			get
			{
				string pathSetting = Instances.Settings.GetValue("Hops:FunctionPaths", "");
				if (string.IsNullOrWhiteSpace(pathSetting))
				{
					return new string[0];
				}
				return pathSetting.Split('\n');
			}
			set
			{
				if (value == null)
				{
					Instances.Settings.SetValue("Hops:FunctionPaths", "");
					return;
				}
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < value.Length; i++)
				{
					string s = value[i].Trim();
					if (!string.IsNullOrEmpty(s))
					{
						if (sb.Length > 0)
						{
							sb.Append('\n');
						}
						sb.Append(s);
					}
				}
				Instances.Settings.SetValue("Hops:FunctionPaths", sb.ToString());
			}
		}

		public static string[] FunctionSourceNames
		{
			get
			{
				string nameSetting = Instances.Settings.GetValue("Hops:FunctionNames", "");
				if (string.IsNullOrWhiteSpace(nameSetting))
				{
					return new string[0];
				}
				return nameSetting.Split('\n');
			}
			set
			{
				if (value == null)
				{
					Instances.Settings.SetValue("Hops:FunctionNames", "");
					return;
				}
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < value.Length; i++)
				{
					string s = value[i].Trim();
					if (!string.IsNullOrEmpty(s))
					{
						if (sb.Length > 0)
						{
							sb.Append('\n');
						}
						sb.Append(s);
					}
				}
				Instances.Settings.SetValue("Hops:FunctionNames", sb.ToString());
			}
		}

		public static string[] FunctionSourceSelectedStates
		{
			get
			{
				string selectedSetting = Instances.Settings.GetValue("Hops:FunctionSelectedState", "");
				if (string.IsNullOrWhiteSpace(selectedSetting))
				{
					return new string[0];
				}
				return selectedSetting.Split('\n');
			}
			set
			{
				if (value == null)
				{
					Instances.Settings.SetValue("Hops:FunctionSelectedState", "");
					return;
				}
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < value.Length; i++)
				{
					string s = value[i].Trim();
					if (!string.IsNullOrEmpty(s))
					{
						if (sb.Length > 0)
						{
							sb.Append('\n');
						}
						sb.Append(s);
					}
				}
				Instances.Settings.SetValue("Hops:FunctionSelectedState", sb.ToString());
			}
		}

		public static bool HideWorkerWindows
		{
			get
			{
				return Instances.Settings.GetValue("Hops:HideWorkerWindows", true);
			}
			set
			{
				Instances.Settings.SetValue("Hops:HideWorkerWindows", value);
			}
		}

		public static bool LaunchWorkerAtStart
		{
			get
			{
				return Instances.Settings.GetValue("Hops:LaunchWorkerAtStart", true);
			}
			set
			{
				Instances.Settings.SetValue("Hops:LaunchWorkerAtStart", value);
			}
		}

		public static int LocalWorkerCount
		{
			get
			{
				return Instances.Settings.GetValue("Hops:LocalWorkerCount", 1);
			}
			set
			{
				if (value >= 0)
				{
					Instances.Settings.SetValue("Hops:LocalWorkerCount", value);
				}
			}
		}

		public static int RecursionLimit
		{
			get
			{
				return Instances.Settings.GetValue("Hops:RecursionLimit", 10);
			}
			set
			{
				if (value >= 0)
				{
					Instances.Settings.SetValue("Hops:RecursionLimit", value);
				}
			}
		}

		public static int HTTPTimeout
		{
			get
			{
				if (_httpTimeout == 0)
				{
					_httpTimeout = Instances.Settings.GetValue("Hops:HttpTimeout", 100);
				}
				return _httpTimeout;
			}
			set
			{
				if (value >= 1)
				{
					Instances.Settings.SetValue("Hops:HttpTimeout", value);
					_httpTimeout = value;
				}
			}
		}

		public static int MaxConcurrentRequests
		{
			get
			{
				if (_maxConcurrentRequests == 0)
				{
					_maxConcurrentRequests = Instances.Settings.GetValue("Hops:MaxConcurrentRequests", 4);
				}
				return _maxConcurrentRequests;
			}
			set
			{
				if (value >= 1)
				{
					Instances.Settings.SetValue("Hops:MaxConcurrentRequests", value);
					_maxConcurrentRequests = value;
				}
			}
		}

		public static void CheckFunctionManagerStatus()
		{
			Version ver = typeof(RhinoApp).Assembly.GetName().Version;
			if (HostUtils.RunningOnOSX)
			{
				if ((ver.Major < 8 || ver.Build < 22126) && (ver.Major != 7 || ver.Minor < 19))
				{
					ShowFunctionManager = false;
					return;
				}
				ShowFunctionManager = true;
			}
			ShowFunctionManager = true;
		}

		public static void InitFunctionSources()
		{
			if (FunctionSourcePaths.Length != FunctionSourceNames.Length && FunctionSourcePaths.Length != FunctionSourceSelectedStates.Length)
			{
				return;
			}
			if (FunctionSources == null)
			{
				FunctionSources = new List<FunctionSourceRow>();
			}
			if (FunctionSources.Count > 0)
			{
				FunctionSources.Clear();
			}
			for (int i = 0; i < FunctionSourcePaths.Length; i++)
			{
				FunctionSourceRow row = new FunctionSourceRow(FunctionSourceNames[i].Trim(), FunctionSourcePaths[i].Trim());
				FunctionSources.Add(row);
				if (bool.TryParse(FunctionSourceSelectedStates[i], out var isChecked))
				{
					FunctionSources[i].RowCheckbox.Checked = isChecked;
				}
			}
		}
	}
}