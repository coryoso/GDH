using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace GDH
{
	public class GDHInfo : GH_AssemblyInfo
	{
		public override string Name => "GDH";

		//Return a 24x24 pixel bitmap to represent this GHA library.
		public override Bitmap Icon => null;

		//Return a short string describing the purpose of this GHA library.
		public override string Description => "";

		public override Guid Id => new Guid("ba8a8354-78c9-4c26-8669-63b344c5990d");

		//Return a string identifying you or your company.
		public override string AuthorName => "";

		//Return a string representing your preferred contact details.
		public override string AuthorContact => "";
	}
}