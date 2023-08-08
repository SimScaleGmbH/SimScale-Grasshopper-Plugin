using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace External_Building_Aerodynamics
{
  public class External_Building_AerodynamicsInfo : GH_AssemblyInfo
  {
    public override string Name => "SimScale ";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon => null;

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "";

    public override Guid Id => new Guid("359c3aa8-9705-45a5-9800-aa07a67c3d05");

    //Return a string identifying you or your company.
    public override string AuthorName => "";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "";
  }
}
