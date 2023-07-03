using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GDH;
using Grasshopper.Kernel;
using Resthopper.IO;
using Rhino;

namespace GDH
{
	internal class SolveDataList
	{
		private readonly int _solveSerialNumber;

		private readonly GDHComponent _parentComponent;

		private readonly bool _useMemoryCacheWhenSolving;

		private readonly RemoteDefinition _remoteDefinition;

		private bool _synchronous;

		private List<SolveData> _data = new List<SolveData>();

		private bool _solveStarted;

		private int _solvedCount;

		public int Count => _data.Count;

		public bool Canceled { get; set; }

		public bool Synchronous => _synchronous;

		public SolveDataList(int serialNumber, GDHComponent component, RemoteDefinition remoteDefinition, bool useMemoryCache)
		{
			_solveSerialNumber = serialNumber;
			_parentComponent = component;
			_useMemoryCacheWhenSolving = useMemoryCache;
			_remoteDefinition = remoteDefinition;
		}

		public void Add(Schema inputSchema)
		{
			_data.Add(new SolveData(inputSchema));
		}

		public void StartSolving(bool waitUntilComplete)
		{
			if (!_solveStarted && !Canceled)
			{
				_solveStarted = true;
				_synchronous = waitUntilComplete;
				SolveIterationQueue.Add(this);
			}
		}

		public Task Solve(int index)
		{
			if (Canceled)
			{
				return null;
			}
			return _data[index].Solve(_remoteDefinition, _useMemoryCacheWhenSolving, OnItemSolved);
		}

		private void OnItemSolved()
		{
			Interlocked.Increment(ref _solvedCount);
			if (_solvedCount == _data.Count && !_synchronous)
			{
				SolveIterationQueue.AddIdleCallback(((GH_InstanceDescription)_parentComponent).InstanceGuid, delegate
				{
					_parentComponent.OnWorkingListComplete();
				});
			}
		}

		private void RhinoApp_Idle(object sender, EventArgs e)
		{
			if (_solveSerialNumber != _parentComponent.SolveSerialNumber || Canceled)
			{
				RhinoApp.Idle -= ((EventHandler)RhinoApp_Idle);
			}
			else if (SolvedFor(_parentComponent.SolveSerialNumber))
			{
				((GH_DocumentObject)_parentComponent).ExpireSolution(true);
				RhinoApp.Idle -= ((EventHandler)RhinoApp_Idle);
			}
		}

		public Schema SolvedSchema(int index)
		{
			return _data[index].Output;
		}

		public bool SolvedFor(int serialNumber)
		{
			if (!_solveStarted || _solveSerialNumber != serialNumber)
			{
				return false;
			}
			foreach (SolveData datum in _data)
			{
				if (!datum.HasSolveData)
				{
					return false;
				}
			}
			return true;
		}
	}
}