using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using GDH;
using Grasshopper.Kernel;
using Hops;
using Rhino.Runtime;

namespace GDH
{
	internal class Servers
	{
		private class ComputeServer
		{
			private readonly Process _process;

			private readonly int _port;

			private readonly string _url;

			public bool IsLocalProcess => _process != null;

			public ComputeServer(Process proc, int port)
			{
				_process = proc;
				_port = port;
				_url = null;
			}

			public ComputeServer(string url)
			{
				_process = null;
				_port = 0;
				_url = url.Trim('/');
			}

			public bool IsProcess(Process proc)
			{
				if (_process == null)
				{
					return false;
				}
				return _process.Id == proc.Id;
			}

			public int LocalProcessPort()
			{
				return _port;
			}

			public string GetUrl()
			{
				if (_process != null)
				{
					if (_process.HasExited)
					{
						return null;
					}
					return $"http://localhost:{_port}";
				}
				return _url;
			}
		}

		private static Task<bool> _initServerTask;

		private const int RhinoComputePort = 6500;

		private static object _lockObject = new object();

		private static Queue<ComputeServer> _computeServerQueue = new Queue<ComputeServer>();

		private static bool _settingsNeedReading = true;

		public static int ActiveLocalComputeCount => Process.GetProcessesByName("compute.geometry").Length;

		public static void StartServerOnLaunch()
		{
			_initServerTask = Task.Run(delegate
			{
				lock (_lockObject)
				{
					LaunchLocalRhinoCompute(_computeServerQueue, waitUntilServing: true);
				}
				return true;
			});
		}

		public static void SettingsChanged()
		{
			_settingsNeedReading = true;
		}

		public static string GetDescriptionUrl(string definitionPath)
		{
			return GetComputeServerBaseUrl() + "/io?pointer=" + HttpUtility.UrlEncode(definitionPath);
		}

		public static string GetDescriptionPostUrl()
		{
			return GetComputeServerBaseUrl() + "/io";
		}

		public static string GetDescriptionUrl(Guid componentId)
		{
			if (componentId == Guid.Empty)
			{
				return null;
			}
			return GetComputeServerBaseUrl() + "/io?pointer=" + HttpUtility.UrlEncode(componentId.ToString());
		}

		public static string GetSolveUrl()
		{
			return GetComputeServerBaseUrl() + "/grasshopper";
		}

		private static string GetComputeServerBaseUrl()
		{
			if (_initServerTask != null)
			{
				_initServerTask.Wait();
				_initServerTask = null;
			}
			string url = null;
			lock (_lockObject)
			{
				if (_settingsNeedReading)
				{
					_settingsNeedReading = false;
					string[] servers = HopsAppSettings.Servers;
					ComputeServer[] serverArray = _computeServerQueue.ToArray();
					_computeServerQueue.Clear();
					string[] array = servers;
					foreach (string server in array)
					{
						if (!string.IsNullOrWhiteSpace(server))
						{
							_computeServerQueue.Enqueue(new ComputeServer(server));
						}
					}
					ComputeServer[] array2 = serverArray;
					foreach (ComputeServer item in array2)
					{
						if (item.IsLocalProcess)
						{
							_computeServerQueue.Enqueue(item);
						}
					}
				}
				if (_computeServerQueue.Count > 0)
				{
					ComputeServer current2 = _computeServerQueue.Dequeue();
					url = current2.GetUrl();
					if (!string.IsNullOrEmpty(url))
					{
						_computeServerQueue.Enqueue(current2);
					}
				}
				if (string.IsNullOrEmpty(url) && HostUtils.RunningOnWindows)
				{
					_computeServerQueue = new Queue<ComputeServer>();
					if (_computeServerQueue.Count == 0)
					{
						LaunchLocalRhinoCompute(_computeServerQueue, waitUntilServing: true);
					}
					if (_computeServerQueue.Count > 0)
					{
						ComputeServer current = _computeServerQueue.Dequeue();
						_computeServerQueue.Enqueue(current);
						url = current.GetUrl();
					}
				}
			}
			if (string.IsNullOrEmpty(url))
			{
				string message = "No compute server found";
				if (HostUtils.RunningOnOSX)
				{
					message += ": Mac Rhino only supports external compute servers";
				}
				throw new Exception(message);
			}
			return url;
		}

		public static void LaunchChildComputeGeometry(int childCount)
		{
			if (childCount >= 1)
			{
				string baseUrl = GetComputeServerBaseUrl();
				int thisProc = Process.GetCurrentProcess().Id;
				string address = $"{baseUrl}/launch?children={childCount}&parent={thisProc}";
				RemoteDefinition.HttpClient.GetAsync(address);
			}
		}

		private static void LaunchLocalRhinoCompute(Queue<ComputeServer> serverQueue, bool waitUntilServing)
		{
			ComputeServer[] array = serverQueue.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].IsLocalProcess)
				{
					return;
				}
			}
			Process[] existingProcesses = Process.GetProcessesByName("rhino.compute");
			if (existingProcesses != null && existingProcesses.Length != 0 && IsPortOpen("localhost", 6500, new TimeSpan(0, 0, 0, 0, 100)))
			{
				serverQueue.Enqueue(new ComputeServer(existingProcesses[0], 6500));
			}
			string dir = null;
			if (GhaAssemblyInfo.TheAssemblyInfo != null)
			{
				dir = Path.GetDirectoryName(((GH_AssemblyInfo)GhaAssemblyInfo.TheAssemblyInfo).Location);
			}
			if (dir == null)
			{
				dir = Path.GetDirectoryName(typeof(Servers).Assembly.Location);
			}
			string pathToRhinoCompute = Path.Combine(dir, "rhino.compute", "rhino.compute.exe");
			if (!File.Exists(pathToRhinoCompute))
			{
				pathToRhinoCompute = Path.Combine(dir, "net5.0", "rhino.compute.exe");
				if (!File.Exists(pathToRhinoCompute))
				{
					return;
				}
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo(pathToRhinoCompute);
			int childCount = HopsAppSettings.LocalWorkerCount;
			if (childCount < 1)
			{
				childCount = 1;
			}
			int thisProc = Process.GetCurrentProcess().Id;
			processStartInfo.Arguments = $"--childof {thisProc} --childcount {childCount} --port {6500} --spawn-on-startup";
			processStartInfo.WindowStyle = (HopsAppSettings.HideWorkerWindows ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Minimized);
			processStartInfo.UseShellExecute = true;
			processStartInfo.CreateNoWindow = HopsAppSettings.HideWorkerWindows;
			string assemblyPath = Assembly.GetExecutingAssembly().Location;
			if (string.IsNullOrWhiteSpace(assemblyPath))
			{
				assemblyPath = ((GH_AssemblyInfo)GhaAssemblyInfo.TheAssemblyInfo).Location;
			}
			string parentPath = Path.GetDirectoryName(assemblyPath);
			processStartInfo.WorkingDirectory = Path.Combine(parentPath, "rhino.compute");
			Process process = Process.Start(processStartInfo);
			DateTime start = DateTime.Now;
			if (waitUntilServing)
			{
				while (!IsPortOpen("localhost", 6500, new TimeSpan(0, 0, 1)))
				{
					if ((DateTime.Now - start).TotalSeconds > 60.0)
					{
						process.Kill();
						throw new Exception("Unable to start a local compute server");
					}
				}
			}
			else
			{
				Thread.Sleep(100);
			}
			if (process != null)
			{
				serverQueue.Enqueue(new ComputeServer(process, 6500));
			}
		}

		private static bool IsPortOpen(string host, int port, TimeSpan timeout)
		{
			try
			{
				using TcpClient client = new TcpClient();
				IAsyncResult result = client.BeginConnect(host, port, null, null);
				bool result2 = result.AsyncWaitHandle.WaitOne(timeout);
				client.EndConnect(result);
				return result2;
			}
			catch
			{
				return false;
			}
		}
	}
}