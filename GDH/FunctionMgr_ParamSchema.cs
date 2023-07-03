namespace GDH
{
	public class FunctionMgr_ParamSchema
	{
		public string Name { get; set; }

		public string Nickname { get; set; }

		public string Description { get; set; }

		public string ParamType { get; set; }

		public string ResultType { get; set; }

		public int AtLeast { get; set; }

		public int AtMost { get; set; }

		public bool TreeAccess { get; set; }
	}
}