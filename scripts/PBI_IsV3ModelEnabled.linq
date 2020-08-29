<Query Kind="Statements">
  <NuGetReference>System.IO.Compression</NuGetReference>
  <Namespace>System.IO.Compression</Namespace>
</Query>

var v3FeatureGuid = new Guid("C15D05E2-F1C1-4F62-94B2-0F179E080741");
var useStoreApp = true;
var userSettingsPath = useStoreApp
	? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Microsoft\\Power BI Desktop Store App", "User.zip")
	: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Power BI Desktop", "User.zip");

using (var file = File.OpenRead(userSettingsPath))
using (var zip = new ZipArchive(file))
{
	var entry = zip.Entries.Single(e => e.FullName == "FeatureSwitches/FeatureSwitches.xml");
	using (var stream = entry.Open())
	{
		var xml = XDocument.Load(stream);
		var xEntry = xml.XPathSelectElement($"//Entry[@Type='{v3FeatureGuid.ToString()}']").Dump();
		if (xEntry != null)
		{
			var enabled = xEntry.Attribute("Value").Value.Contains("1");
			enabled.Dump();
		}
	}
}
