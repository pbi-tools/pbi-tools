<Query Kind="Statements">
  <Reference>&lt;ProgramFilesX64&gt;\Microsoft Power BI Desktop\bin\Microsoft.Mashup.Client.UI.dll</Reference>
  <Reference>&lt;ProgramFilesX64&gt;\Microsoft Power BI Desktop\bin\Microsoft.Mashup.Document.dll</Reference>
  <Reference>&lt;ProgramFilesX64&gt;\Microsoft Power BI Desktop\bin\Microsoft.PowerBI.Client.Shared.dll</Reference>
  <Reference>&lt;ProgramFilesX64&gt;\Microsoft Power BI Desktop\bin\Microsoft.PowerBI.Client.Windows.dll</Reference>
  <Namespace>Microsoft.Mashup.Host.Document</Namespace>
  <Namespace>Microsoft.PowerBI.Client.Windows.Services</Namespace>
  <Namespace>Microsoft.PowerBI.Client.Windows.Telemetry</Namespace>
  <Namespace>Microsoft.PowerBI.Client.Shared</Namespace>
  <Namespace>Microsoft.PowerBI.Client.Windows</Namespace>
</Query>

var di = DependencyInjectionService.Get();

var sysEnv = new SystemEnvironment();
var settings = new PowerBICloudSettings(sysEnv).Dump("PowerBICloudSettings");

di.RegisterInstance<ISystemEnvironment>(sysEnv);
di.RegisterInstance<IApplicationPaths>(new PowerBIConstants(settings, sysEnv));
di.RegisterInstance<IPowerBISettings>(settings);

var fsman = new FeatureSwitchManager(new VersionInfo().Dump());
fsman.RegisterKnownSwitches();
fsman.GetNames().Dump("Names");
fsman.GetPreviewFeatureSwitches(enabled: false).Dump("Disabled");
fsman.GetPreviewFeatureSwitches(enabled: true).Dump("Enabled");

di.RegisterInstance<IFeatureSwitchManager>(fsman);
ReportExtensions.IsV3FeatureSwitchEnabled().Dump("V3 Enabled");
