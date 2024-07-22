/*
 * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.
 * Copyright (C) 2018 Mathias Thierbach
 *
 * pbi-tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * pbi-tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * A copy of the GNU Affero General Public License is available in the LICENSE file,
 * and at <https://goto.pbi.tools/license>.
 */

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
                    Version = html.DocumentNode.SelectSingleNode("//div[contains(@class,'dlcdetail__filegrid')]//h3[contains(text(),'Version:')]")
                        .ParentNode.SelectSingleNode("p")
                        .InnerText,
                    DatePublished = html.DocumentNode.SelectSingleNode("//div[contains(@class,'dlcdetail__filegrid')]//h3[contains(text(),'Date Published:')]")
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
