using System;
using System.Threading.Tasks;
using GDH;
using Resthopper.IO;

namespace GDH
{
	internal class SolveData
	{
		private readonly Schema _input;

		private Task _workingTask;

		public bool HasSolveData => Output != null;

		public Schema Output { get; private set; }

		public SolveData(Schema input)
		{
			_input = input;
		}

		public Task Solve(RemoteDefinition remoteDefinition, bool useMemoryCache, Action completedCallback)
		{
			_workingTask = Task.Run(delegate
			{
				Output = remoteDefinition.Solve(_input, useMemoryCache);
				completedCallback();
			});
			return _workingTask;
		}
	}
}