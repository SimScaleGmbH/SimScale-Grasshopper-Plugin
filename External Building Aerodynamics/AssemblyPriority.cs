using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Grasshopper.Kernel;

namespace External_Building_Aerodynamics
{
    public class External_Building_AerodynamicsPriority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            // Kitware's VTK .NET wrapper ("mummy") predates .NET Core and has its own
            // internal mechanism for pulling in its native companion DLL that .NET Core's
            // stricter assembly loader rejects outright: "Could not load file or assembly
            // 'Kitware.mummy.Runtime.Unmanaged.dll'... only single file assemblies are
            // supported" is CoreCLR's managed loader complaining that it was handed a
            // native (non-managed) PE image. Loading that DLL ourselves first, via the
            // actual .NET Core native-loading API, gets it mapped into the process before
            // any VTK code runs, so whatever mummy does internally finds it already loaded
            // instead of trying (and failing) to load it as a managed assembly itself.
            try
            {
                string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (pluginDir != null)
                {
                    NativeLibrary.Load(Path.Combine(pluginDir, "Kitware.mummy.Runtime.Unmanaged.dll"));
                }
            }
            catch
            {
                // Best-effort: if this fails, fall through and let VTK's own loading
                // attempt produce its normal error rather than blocking the whole plugin.
            }

            return GH_LoadingInstruction.Proceed;
        }
    }
}
