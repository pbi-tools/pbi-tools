// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace PbiTools.Win32
{
    public static class ProcessExtensions
    {
        public static Process GetParent(this Process process)
        {
            try
            {
                using (var query = new ManagementObjectSearcher(
                    "SELECT ParentProcessId " +
                    "FROM Win32_Process " +
                    "WHERE ProcessId=" + process.Id))
                {
                    return query
                        .Get()
                        .OfType<ManagementObject>()
                        .Select(p => Process.GetProcessById((int)(uint)p["ParentProcessId"]))
                        .FirstOrDefault();
                }
            }
            catch
            {
                return null;
            }
        }

        //
        // Original source: https://github.com/windows-toolkit/Microsoft.Toolkit.Win32/blob/master/Tests/UnitTests.WebView.Shared/ProcessExtensions.cs
        //

        // Define an extension method for type System.Process that returns the command 
        // line via WMI.
        public static string GetCommandLine(this Process process)
        {
            string cmdLine = null;
            using (var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
            {
                // By definition, the query returns at most 1 match, because the process 
                // is looked up by ID (which is unique by definition).
                var matchEnum = searcher.Get().GetEnumerator();
                if (matchEnum.MoveNext()) // Move to the 1st item.
                {
                    cmdLine = matchEnum.Current["CommandLine"]?.ToString();
                }
            }
            if (cmdLine == null)
            {
                // Not having found a command line implies 1 of 2 exceptions, which the
                // WMI query masked:
                // An "Access denied" exception due to lack of privileges.
                // A "Cannot process request because the process (<pid>) has exited."
                // exception due to the process having terminated.
                // We provoke the same exception again simply by accessing process.MainModule.
                var dummy = process.MainModule; // Provoke exception.
            }
            return cmdLine;
        }


        #region CommandLineParser

        //
        // Sourced from https://stackoverflow.com/questions/298830/split-string-containing-command-line-parameters-into-string-in-c-sharp/298990#298990
        //

        public static IEnumerable<string> SplitCommandLine(this string commandLine)
        {
            bool inQuotes = false;

            return commandLine.Split(c =>
                                    {
                                        if (c == '\"')
                                            inQuotes = !inQuotes;

                                        return !inQuotes && c == ' ';
                                    })
                            .Select(arg => arg.Trim().TrimMatchingQuotes('\"'))
                            .Where(arg => !string.IsNullOrEmpty(arg));
        }

        public static IEnumerable<string> Split(this string str, Func<char, bool> controller)
        {
            int nextPiece = 0;

            for (int c = 0; c < str.Length; c++)
            {
                if (controller(str[c]))
                {
                    yield return str.Substring(nextPiece, c - nextPiece);
                    nextPiece = c + 1;
                }
            }

            yield return str.Substring(nextPiece);
        }

        public static string TrimMatchingQuotes(this string input, char quote)
        {
            if ((input.Length >= 2) &&
                (input[0] == quote) && (input[input.Length - 1] == quote))
                return input.Substring(1, input.Length - 2);

            return input;
        }

        #endregion
    }
}
#endif