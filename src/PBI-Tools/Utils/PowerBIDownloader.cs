// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using HtmlAgilityPack;
using Serilog;

namespace PbiTools.Utils
{
    public class PowerBIDownloader
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PowerBIDownloader>();

        public static bool TryFetchInfo(out PowerBIDownloadInfo info)
        {
            try
            {
                var web = new HtmlWeb();
                // TODO Support explicit proxy settings?
                var html = web.Load("https://www.microsoft.com/en-us/download/details.aspx?id=58494" /*"https://aka.ms/pbiSingleInstaller"*/); // MUST get the en-us version so that the text matching works

                info = new PowerBIDownloadInfo {
                    Version = html.DocumentNode.SelectSingleNode("//div[contains(@class,'header') and contains(text(),'Version:')]")
                        .ParentNode.SelectSingleNode("p")
                        .InnerText,
                    DatePublished = html.DocumentNode.SelectSingleNode("//div[contains(@class,'header') and contains(text(),'Date Published:')]")
                        .ParentNode.SelectSingleNode("p")
                        .InnerText
                };
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to retrieve download info for Power BI Desktop.");
                info = null;
                return false;
            }
        }

    }

    public class PowerBIDownloadInfo
    {
        public string Version { get; set; }
        public string DatePublished { get; set; }
        public string DownloadUrl64 => "https://download.microsoft.com/download/8/8/0/880BCA75-79DD-466A-927D-1ABF1F5454B0/PBIDesktopSetup_x64.exe";
        public string EulaUrl => "https://powerbi.microsoft.com/desktop-eula/";
    }
}