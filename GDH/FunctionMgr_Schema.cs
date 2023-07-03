namespace GDH
{
	public class FunctionMgr_Schema
	{
		public string Uri { get; set; }

		public string Name { get; set; }

		public string Nickname { get; set; }

		public string Description { get; set; }

		public string Category { get; set; }

		public string Subcategory { get; set; }

		public FunctionMgr_ParamSchema[] Inputs { get; set; }

		public FunctionMgr_ParamSchema[] Outputs { get; set; }

		public string Icon { get; set; }
	}
}