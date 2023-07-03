using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using GDH;
using Rhino;

namespace GDH
{
	internal static class SolveIterationQueue
	{
		private static Task _solveTask;

		private static ConcurrentStack<SolveDataList> _stack = new ConcurrentStack<SolveDataList>();

		private static int _maxConcurrentRequests;

		private static bool _idleSet = false;

		private static ConcurrentDictionary<Guid, Action> _componentCallbacks = new ConcurrentDictionary<Guid, Action>();

		public static void Add(SolveDataList datalist)
		{
			if (!_idleSet && !datalist.Synchronous)
			{
				_idleSet = true;
				RhinoApp.Idle += ((EventHandler)RhinoApp_Idle);
			}
			_maxConcurrentRequests = HopsAppSettings.MaxConcurrentRequests;
			if (datalist.Synchronous)
			{
				ConcurrentStack<SolveDataList> stack = new ConcurrentStack<SolveDataList>();
				stack.Push(datalist);
				Task.Run(delegate
				{
					ProcessStack(stack);
				}).Wait();
				return;
			}
			_stack.Push(datalist);
			if (_solveTask == null || _solveTask.IsCompleted)
			{
				_solveTask = Task.Run(delegate
				{
					ProcessStack(_stack);
				});
			}
		}

		private static void RhinoApp_Idle(object sender, EventArgs e)
		{
			if (_stack.Count > 0 && (_solveTask == null || _solveTask.IsCompleted))
			{
				_solveTask = Task.Run(delegate
				{
					ProcessStack(_stack);
				});
			}
			foreach (Action value in _componentCallbacks.Values)
			{
				value();
			}
			_componentCallbacks.Clear();
		}

		public static void AddIdleCallback(Guid componentId, Action callback)
		{
			_componentCallbacks[componentId] = callback;
		}

		private static void ProcessStack(ConcurrentStack<SolveDataList> stack)
		{
			List<Task> childTasks = new List<Task>();
			SolveDataList datalist;
			while (stack.TryPop(out datalist))
			{
				for (int i = 0; i < datalist.Count; i++)
				{
					Task t = datalist.Solve(i);
					if (t == null)
					{
						continue;
					}
					childTasks.Add(t);
					if (childTasks.Count < _maxConcurrentRequests)
					{
						continue;
					}
					Task[] taskArray = childTasks.ToArray();
					Task.WaitAny(childTasks.ToArray());
					childTasks.Clear();
					Task[] array = taskArray;
					foreach (Task task in array)
					{
						if (!task.IsCompleted)
						{
							childTasks.Add(task);
						}
					}
				}
			}
			Task[] remainingTasks = childTasks.ToArray();
			if (remainingTasks.Length != 0)
			{
				Task.WaitAll(remainingTasks);
			}
		}
	}
}