<Query Kind="Program">
  <NuGetReference>PInvoke.Msi</NuGetReference>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

async Task Main()
{
	var downloadUrlBase = "https://download.microsoft.com/download/8/8/0/880BCA75-79DD-466A-927D-1ABF1F5454B0/";
	var downloadFilename =
	// "PBIDesktopSetup-2019-11_x64.exe";
	// "PBIDesktopSetup-2019-12_x64.exe";
	// "PBIDesktopSetup-2020-02_x64.exe";
	// "PBIDesktopSetup-2020-03_x64.exe";
	// "PBIDesktopSetup-2020-04_x64.exe";
	// "PBIDesktopSetup-2020-05_x64.exe";
	// "PBIDesktopSetup-2020-06_x64.exe";
	// "PBIDesktopSetup-2020-07_x64.exe";
	// "PBIDesktopSetup-2020-08_x64.exe";
	// "PBIDesktopSetup-2020-09_x64.exe";
	// "PBIDesktopSetup-2020-10_x64.exe";
	// "PBIDesktopSetup-2020-11_x64.exe";
	// "PBIDesktopSetup-2020-12_x64.exe";
	// "PBIDesktopSetup-2021-02_x64.exe";
	// "PBIDesktopSetup-2021-03_x64.exe";
	// "PBIDesktopSetup-2021-04_x64.exe";
	// "PBIDesktopSetup-2021-05_x64.exe";
	// "PBIDesktopSetup-2021-06_x64.exe";
	// "PBIDesktopSetup-2021-07_x64.exe";
	// "PBIDesktopSetup-2021-08_x64.exe";
	// "PBIDesktopSetup-2021-09_x64.exe";
	// "PBIDesktopSetup-2021-10_x64.exe";
	// "PBIDesktopSetup-2021-11_x64.exe";
	// "PBIDesktopSetup-2021-12_x64.exe";
	"PBIDesktopSetup_x64.exe";
	var skipDownload = false;
	var skipExtract = false;

	/***************************************************************/
	var rootFolder = Path.GetFullPath(Path.Combine(Util.CurrentQueryPath, "../../../../pbi-downloader/PBIDesktop_x64")).Dump();
	/***************************************************************/
	var tempFolder = Path.Combine(rootFolder, "_temp");
	
	var downloadPath = Path.Combine(tempFolder, downloadFilename);

	if (!skipDownload)
	{
		"Downloading...".Dump(downloadFilename);
		using (var http = new HttpClient())
		using (ConsoleProgressIndicator.Start())
		{
			var stream = await http.GetStreamAsync($"{downloadUrlBase}{downloadFilename}");
			using (var file = File.Create(downloadPath))
			{
				await stream.CopyToAsync(file).Dump();  // This might take a while, so progress updates would be useful
			}
		}
	}
	
	var versionInfo = FileVersionInfo.GetVersionInfo(downloadPath).Dump();
	// Here, determine if extraction is needed
	
	if (skipExtract) return;

	var extractPath = Path.Combine(tempFolder, Path.GetFileNameWithoutExtension(downloadFilename));
	Directory.CreateDirectory(extractPath);

	var destDir = Path.Combine(tempFolder, versionInfo.ProductVersion);
	var logPath = $"{destDir}.log";

	var darkExe = Environment.ExpandEnvironmentVariables(@"%WIX%bin\dark.exe"); // TODO Find exe: $env:WIX, $env:PATH, ??
	using (var darkProc = new Process())
	{
		darkProc.StartInfo = new ProcessStartInfo
		{
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
			UseShellExecute = false, // Allows stdout/stderr redirect
			FileName = darkExe,
			Arguments = $"\"{downloadPath}\" -x \"{extractPath}\"",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			WorkingDirectory = tempFolder
		};
	
		darkProc.OutputDataReceived += (sender, e) => Console.WriteLine($"INF: {e.Data}");
		darkProc.ErrorDataReceived += (sender, e) => Console.WriteLine($"ERR: {e.Data}");
		
		darkProc.Start();
		
		darkProc.BeginOutputReadLine();
		darkProc.BeginErrorReadLine();
		
		darkProc.WaitForExit();
		
		darkProc.ExitCode.Dump("Exit Code"); // Expect: 0
	}
	
	var msiPath = Directory.EnumerateFiles(Path.Combine(extractPath, "AttachedContainer"), "*.msi").FirstOrDefault().Dump();
	if (msiPath != null)
	{
		Directory.CreateDirectory(destDir);

		var hwnd = IntPtr.Zero;
		MsiSetInternalUI(INSTALLUILEVEL.INSTALLUILEVEL_NONE, ref hwnd);
		MsiEnableLog(INSTALLLOGMODE.INSTALLLOGMODE_INFO, logPath, INSTALLLOGATTRIBUTES.INSTALLLOGATTRIBUTES_FLUSHEACHLINE);

		// 'ADMIN' action performs network drive install (extraction only)
		var result = PInvoke.Msi.MsiInstallProduct(msiPath, $"ACTION=ADMIN TARGETDIR=\"{destDir}\"");
		result.Dump(); // Expect: NERR_Success
	}

	var archiveDir = Path.Combine(rootFolder, versionInfo.ProductVersion).Dump("Moving to...");
	if (!Directory.Exists(archiveDir))
	{
		Directory.Move(destDir, archiveDir);
		File.Move(logPath, Path.Combine(rootFolder, $"{versionInfo.ProductVersion}.log"));
	}
	else
	{
		$"{archiveDir} exists already!".Dump("WARNING");
	}
}

// Define other methods, classes and namespaces here
public enum INSTALLUILEVEL
{
	INSTALLUILEVEL_NOCHANGE = 0,    // UI level is unchanged
	INSTALLUILEVEL_DEFAULT = 1,    // default UI is used
	INSTALLUILEVEL_NONE = 2,    // completely silent installation
	INSTALLUILEVEL_BASIC = 3,    // simple progress and error handling
	INSTALLUILEVEL_REDUCED = 4,    // authored UI, wizard dialogs suppressed
	INSTALLUILEVEL_FULL = 5,    // authored UI with wizards, progress, errors
	INSTALLUILEVEL_ENDDIALOG = 0x80, // display success/failure dialog at end of install
	INSTALLUILEVEL_PROGRESSONLY = 0x40, // display only progress dialog
	INSTALLUILEVEL_HIDECANCEL = 0x20, // do not display the cancel button in basic UI
	INSTALLUILEVEL_SOURCERESONLY = 0x100, // force display of source resolution even if quiet
}

[DllImport("msi.dll", SetLastError = true)]
static extern int MsiSetInternalUI(INSTALLUILEVEL dwUILevel, ref IntPtr phWnd);

public enum INSTALLMESSAGE
{
	INSTALLMESSAGE_FATALEXIT = 0x00000000, // premature termination, possibly fatal OOM
	INSTALLMESSAGE_ERROR = 0x01000000, // formatted error message
	INSTALLMESSAGE_WARNING = 0x02000000, // formatted warning message
	INSTALLMESSAGE_USER = 0x03000000, // user request message
	INSTALLMESSAGE_INFO = 0x04000000, // informative message for log
	INSTALLMESSAGE_FILESINUSE = 0x05000000, // list of files in use that need to be replaced
	INSTALLMESSAGE_RESOLVESOURCE = 0x06000000, // request to determine a valid source location
	INSTALLMESSAGE_OUTOFDISKSPACE = 0x07000000, // insufficient disk space message
	INSTALLMESSAGE_ACTIONSTART = 0x08000000, // start of action: action name & description
	INSTALLMESSAGE_ACTIONDATA = 0x09000000, // formatted data associated with individual action item
	INSTALLMESSAGE_PROGRESS = 0x0A000000, // progress gauge info: units so far, total
	INSTALLMESSAGE_COMMONDATA = 0x0B000000, // product info for dialog: language Id, dialog caption
	INSTALLMESSAGE_INITIALIZE = 0x0C000000, // sent prior to UI initialization, no string data
	INSTALLMESSAGE_TERMINATE = 0x0D000000, // sent after UI termination, no string data
	INSTALLMESSAGE_SHOWDIALOG = 0x0E000000 // sent prior to display or authored dialog or wizard
}

public enum INSTALLLOGMODE  // bit flags for use with MsiEnableLog and MsiSetExternalUI
{
	INSTALLLOGMODE_FATALEXIT = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_FATALEXIT >> 24)),
	INSTALLLOGMODE_ERROR = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_ERROR >> 24)),
	INSTALLLOGMODE_WARNING = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_WARNING >> 24)),
	INSTALLLOGMODE_USER = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_USER >> 24)),
	INSTALLLOGMODE_INFO = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_INFO >> 24)),
	INSTALLLOGMODE_RESOLVESOURCE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_RESOLVESOURCE >> 24)),
	INSTALLLOGMODE_OUTOFDISKSPACE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_OUTOFDISKSPACE >> 24)),
	INSTALLLOGMODE_ACTIONSTART = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_ACTIONSTART >> 24)),
	INSTALLLOGMODE_ACTIONDATA = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_ACTIONDATA >> 24)),
	INSTALLLOGMODE_COMMONDATA = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_COMMONDATA >> 24)),
	INSTALLLOGMODE_PROPERTYDUMP = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_PROGRESS >> 24)), // log only
	INSTALLLOGMODE_VERBOSE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_INITIALIZE >> 24)), // log only
	INSTALLLOGMODE_EXTRADEBUG = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_TERMINATE >> 24)), // log only
	INSTALLLOGMODE_LOGONLYONERROR = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_SHOWDIALOG >> 24)), // log only    
	INSTALLLOGMODE_PROGRESS = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_PROGRESS >> 24)), // external handler only
	INSTALLLOGMODE_INITIALIZE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_INITIALIZE >> 24)), // external handler only
	INSTALLLOGMODE_TERMINATE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_TERMINATE >> 24)), // external handler only
	INSTALLLOGMODE_SHOWDIALOG = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_SHOWDIALOG >> 24)), // external handler only
	INSTALLLOGMODE_FILESINUSE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_FILESINUSE >> 24)), // external handler only
}

public enum INSTALLLOGATTRIBUTES // flag attributes for MsiEnableLog
{
	INSTALLLOGATTRIBUTES_APPEND = (1 << 0),
	INSTALLLOGATTRIBUTES_FLUSHEACHLINE = (1 << 1),
}

[DllImport("msi.dll", CharSet = CharSet.Auto, SetLastError = true)]
public static extern UInt32 MsiEnableLog(INSTALLLOGMODE dwLogMode, string szLogFile, INSTALLLOGATTRIBUTES dwLogAttributes);


public class ConsoleProgressIndicator : IDisposable
{
	private readonly CancellationTokenSource _cts;
	private Task _task;

	private ConsoleProgressIndicator()
	{
		this._cts = new CancellationTokenSource();
	}

	private void Action()
	{
		while (!_cts.Token.IsCancellationRequested)
		{
			Thread.Sleep(500);
			Console.Write('.');
		}
	}

	public static IDisposable Start()
	{
		var instance = new ConsoleProgressIndicator();
		if (Environment.UserInteractive)
			instance._task = Task.Run(new Action(instance.Action), instance._cts.Token);
		else
			instance._task = Task.CompletedTask;
		return instance;
	}

	public void Dispose()
	{
		using (_cts)
		using (_task)
		{
			if (_cts.IsCancellationRequested) return;

			_cts.Cancel();
			_task.Wait();

			if (Environment.UserInteractive) Console.WriteLine();
		}
	}
}