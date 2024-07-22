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
using System.Diagnostics;
using PowerArgs;
using Serilog;
using Serilog.Events;

namespace PbiTools
{
    using Cli;
    using Configuration;
    using Utils;

#if NETFRAMEWORK
    class Module
    {
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void ModuleInit()
        {
            DependenciesResolver.LoadExternalAmoLibraries();
            CosturaUtility.Initialize();
        }
    }
#endif

    class Program
    {
#if NETFRAMEWORK
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern ErrorModes SetErrorMode(ErrorModes uMode);

        [Flags]
        private enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOOPENFILEERRORBOX = 0x8000
        }
#endif

        static Program()
        {
#if NETFRAMEWORK
            // Prevent the "This program has stopped working" messages.
            var prevMode = SetErrorMode(ErrorModes.SEM_NOGPFAULTERRORBOX);
            SetErrorMode(prevMode | ErrorModes.SEM_NOGPFAULTERRORBOX); // Set error mode w/o overwriting prev settings
#endif
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(AppSettings.LevelSwitch)
                .WriteTo.Console(
#if !DEBUG
                        outputTemplate: AppSettings.LevelSwitch.MinimumLevel switch
                        {
                            LogEventLevel.Information
                              => "{Message:lj}{NewLine}{Exception}",
                            var level when level < LogEventLevel.Information
                              => "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                            _ => "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                        }
#endif
                    )
                    .Filter.ByExcluding(_ => AppSettings.ShouldSuppressConsoleLogs)
                .CreateLogger();
            
            if (AppSettings.LevelSwitch.MinimumLevel < LogEventLevel.Information)
                Log.Information("Log level: {LogLevel}", AppSettings.LevelSwitch.MinimumLevel);

            if (!AppSettings.TryApplyCustomCulture(out var error)) {
                Log.Warning(error, $"The UI Culture specified in [{Env.UICulture}] could not be applied. Continuing with default OS settings.");
            }

            ArgRevivers.SetReviver(CmdLineActions.NullableRevivers.Int);
        }

        //internal static IConfigurationRoot Configuration { get; }
        internal static AppSettings AppSettings { get; } = new AppSettings();

        static int Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                Console.Title = "pbi-tools";
                var template = """
{{ProductName!}} | {{Copyright!}}

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

See <https://goto.pbi.tools/license> for full license details.


""";
                ArgUsage.GenerateUsageFromTemplate<CmdLineActions>(template).WriteLine();
            }

            // When invoked w/o args, print usage and exit immediately (do not trigger ArgException)
            if ((args ?? Array.Empty<string>()).Length == 0)
            {
                ArgUsage.GenerateUsageFromTemplate<CmdLineActions>().WriteLine();
                return (int)ExitCode.NoArgsProvided;
            }

            var result = default(ExitCode);
            try
            {
                var action = Args.ParseAction<CmdLineActions>(args);
                
                action.Invoke(); // throws ArgException (DuplicateArg, MissingArg, UnexpectedArg, UnknownArg, ValidationArg)

                // in Debug compilation, propagates any exceptions thrown by executing action
                // in Release compilation, a user friendly error message is displayed, and exceptions thrown are available via the HandledException property

                /* This branch only applies in Release (StandardExceptionHandling enabled) mode, and only to __parser__ exceptions */
                if (action.HandledException != null)
                {
                    // Standard output has been generated by PowerArgs framework already
                    Console.WriteLine();
                    Log.Verbose(action.HandledException, "PowerArgs exception");
                }

                result = action.HandledException == null ? ExitCode.Success : ExitCode.InvalidArgs;
            }
            catch (ArgException ex)
            {
                // In RELEASE mode, the PowerArgs error has already been emitted, and the usage docs have been printed to the console at this stage
                
                if (!Environment.UserInteractive)
                {
                    Log.Fatal(ex, "Bad user input.");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(ex.Message);
                    Console.ResetColor();
#if DEBUG
                    ArgUsage.GenerateUsageFromTemplate(ex.Context.Definition).WriteLine();
#endif
                }
                result = ExitCode.InvalidArgs;
            }
            catch (AggregateException exx) when (exx.GetBaseException() is PbiToolsCliException ex)
            {
                Log.Error(ex.Message);
                result = ex.ErrorCode;
            }
            catch (PbiToolsCliException ex)
            {
                Log.Error(ex.Message);
                result = ex.ErrorCode;
            }
            catch (Exception ex) when (ex.IsKnownException())
            {
                Log.Fatal("The operation FAILED: {ExceptionMessage}", ex.Message);
                result = ExitCode.KnownException;
            }
            catch (Exception ex) /* Any unhandled exception */
            {
                // TODO Explicitly log into crash file...
                // If CWD is not writable, put into user profile and show path...

                Log.Fatal(ex, "An unhandled exception occurred.");
                result = ExitCode.UnexpectedError;
            }

            // Prevent closing of window when debugging
            if (Debugger.IsAttached && Environment.UserInteractive)
            {
                Console.WriteLine();
                Console.Write("Press ENTER to exit...");
                Console.ReadLine();
            }

            // ExitCode:
            return (int)result;
        }
    }

    [System.Serializable]
    public class PbiToolsCliException : System.Exception
    {
        public ExitCode ErrorCode { get; }

        public PbiToolsCliException(ExitCode errorCode, string message) : base(message) {
            this.ErrorCode = errorCode;
        }

        public PbiToolsCliException(ExitCode errorCode, Exception inner, string message) : base(message, inner) {
            this.ErrorCode = errorCode;
        }

        protected PbiToolsCliException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    internal static class Exceptions
    {
        public static bool IsKnownException(this Exception ex) => ex switch
        { 
            System.IO.IOException e when (e.HResult & 0xffff) == 32 => true, // The process cannot access the file because it is being used by another process.
            _ => false
        };
    }

    public enum ExitCode
    {
        UnexpectedError = -9,
        UnspecifiedError = -8,
        KnownException = -7,
        NotImplemented = -3,
        InvalidArgs = -2,
        NoArgsProvided = -1,
        Success = 0,
        PathNotFound = 1,
        DependenciesNotInstalled = 2,
        FileExists = 3,
        UnsupportedFileType = 4,
        OverwriteNotAllowed = 5,
    }

}
