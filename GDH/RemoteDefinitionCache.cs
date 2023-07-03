using GDH;
using System;
using System.Collections.Generic;
using System.IO;

namespace GDH
{
	internal static class RemoteDefinitionCache
	{
		private static List<RemoteDefinition> _definitions = new List<RemoteDefinition>();

		private static Dictionary<string, FileSystemWatcher> _filewatchers;

		private static HashSet<string> _watchedFiles = new HashSet<string>();

		public static void Add(RemoteDefinition definition)
		{
			if (!definition.Path.StartsWith("http", StringComparison.OrdinalIgnoreCase) && File.Exists(definition.Path) && !_definitions.Contains(definition))
			{
				_definitions.Add(definition);
				RegisterFileWatcher(definition.Path);
			}
		}

		public static void Remove(RemoteDefinition definition)
		{
			if (!_definitions.Remove(definition) || definition.Path == null)
			{
				return;
			}
			string directory = Path.GetDirectoryName(Path.GetFullPath(definition.Path));
			bool removeFileWatcher = true;
			foreach (RemoteDefinition definition2 in _definitions)
			{
				string existingDefDirectory = Path.GetDirectoryName(Path.GetFullPath(definition2.Path));
				if (directory.Equals(existingDefDirectory, StringComparison.OrdinalIgnoreCase))
				{
					removeFileWatcher = false;
					break;
				}
			}
			if (removeFileWatcher && _filewatchers.TryGetValue(directory, out var watcher))
			{
				watcher.EnableRaisingEvents = false;
				watcher.Dispose();
				_filewatchers.Remove(directory);
			}
		}

		private static void RegisterFileWatcher(string path)
		{
			if (!File.Exists(path))
			{
				return;
			}
			if (_filewatchers == null)
			{
				_filewatchers = new Dictionary<string, FileSystemWatcher>();
			}
			path = Path.GetFullPath(path);
			if (!_watchedFiles.Contains(path.ToLowerInvariant()))
			{
				_watchedFiles.Add(path.ToLowerInvariant());
				string directory = Path.GetDirectoryName(path);
				if (!_filewatchers.ContainsKey(directory) && Directory.Exists(directory))
				{
					FileSystemWatcher fsw = new FileSystemWatcher(directory);
					fsw.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.Security;
					fsw.Changed += Fsw_Changed;
					fsw.EnableRaisingEvents = true;
					_filewatchers[directory] = fsw;
				}
			}
		}

		private static void Fsw_Changed(object sender, FileSystemEventArgs e)
		{
			string path = e.FullPath.ToLowerInvariant();
			if (!_watchedFiles.Contains(path))
			{
				return;
			}
			foreach (RemoteDefinition definition in _definitions)
			{
				string definitionPath = Path.GetFullPath(definition.Path);
				if (path.Equals(definitionPath, StringComparison.OrdinalIgnoreCase))
				{
					definition.OnWatchedFileChanged();
				}
			}
		}
	}
}