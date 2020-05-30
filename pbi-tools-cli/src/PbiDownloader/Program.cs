using System;

namespace PbiDownloader
{
    class Program
    {
        // Serilog

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            // Download EXE
            //   https://download.microsoft.com/download/8/8/0/880BCA75-79DD-466A-927D-1ABF1F5454B0/PBIDesktopSetup_x64.exe
            // Extract MSI
            //   https://silentinstallhq.com/microsoft-power-bi-desktop-silent-install-how-to-guide/
            //   https://www.project-c.ch/2015/07/16/how-to-extract-a-burn-wix-based-installer-installation-setup-file/
            //   WiX:  .\WiX\packages\WiX\tools\dark.exe .\PBIDesktopSetup_x64.exe -x ./install
            //   Azure Pipelines: $(WIX)bin\dark.exe
            // Install MSI
            //   ACTION=ADMIN TARGETDIR=""
            // Determine Version
            //   Rename folder or delete
            // Clean older versions
        }
    }

    /*
          ./
          ./$(Pipeline.Workspace)/.cache
    */
}
