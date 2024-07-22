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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PowerArgs;
using Serilog;

namespace PbiTools.Cli
{
    public static class CmdLineArgumentsDefinitionExtensions
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<CmdLineActions>();

        public static CommandLineArgumentsDefinition For<T>()
        {
            return new CommandLineArgumentsDefinition(typeof(T));
        }

        /// <summary>
        /// Removes all auto-generated action aliases, leaving only explicitly declared ones.
        /// </summary>
        public static CommandLineArgumentsDefinition RemoveAutoAliases(this CommandLineArgumentsDefinition def, bool isUsage,
            [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
        {
            Log.Verbose("'RemoveAutoAliases' invoked. Caller: {Caller}", callerName);

            foreach (var action in def.Actions)
            {
                if (action.Source is MethodInfo method)
                {
                    if (action.DefaultAlias == method.Name && action.Aliases.Count > 1)
                        action.Aliases.Remove(action.DefaultAlias);

                    if (isUsage)
                    { 
                        var firstShortcutAttr = action.Metadata.OfType<ArgShortcut>().FirstOrDefault();  // this is the desired Default
                        while (action.DefaultAlias != firstShortcutAttr?.Shortcut && firstShortcutAttr != null)
                        {
                            action.Aliases.Remove(action.DefaultAlias);
                        }
                    }
                    else
                    { 
                        foreach (var alias in action.Metadata.OfType<ArgAltShortcut>())
                        {
                            if (!action.Aliases.Contains(alias.Shortcut))
                                action.Aliases.Add(alias.Shortcut);
                        }
                    }

                }
            }
            return def;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ApplyDefinitionTransforms : ArgHook
    {

        public override void BeforeValidateDefinition(HookContext context)
        {
            Log.Verbose("Invoking 'BeforeValidateDefinition'");

            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            { 
                foreach (var action in context.Definition.Actions)
                {
                    Log.Verbose("Action: {Source}", action.Source);
                    Log.Verbose(" Default Alias: {DefaultAlias}", action.DefaultAlias);
                    foreach (var alias in action.Aliases)
                        Log.Verbose(" Alias: {Alias}", alias);
                    foreach (var metadata in action.Metadata)
                    {
                        if (metadata is ArgShortcut shortcut)
                            Log.Verbose(" Shortcut: {Shortcut}", shortcut.Shortcut);
                        else
                            Log.Verbose(" Metadata: {Metadata}", metadata);
                    }
                }        
            }

            /* this is not getting called for ArgsUsage.GenerateFromTemplate() */
            context.Definition.RemoveAutoAliases(false);
        }

        public override void BeforeInvoke(HookContext context)
        {
            Log.Verbose("Invoking Action: {SpecifiedAction}", context.SpecifiedAction);
        }

        public override void BeforePrepareUsage(HookContext context)
        {
            context.Definition.RemoveAutoAliases(true);

            // remove hidden args:
            new List<CommandLineAction>(
                context.Definition.Actions.Where(a => a.Metadata.HasMeta<OmitFromUsageDocs>())
            )
            .ForEach(a => context.Definition.Actions.Remove(a));
        }

    }


    [AttributeUsage(AttributeTargets.Method)]
    public class ArgAltShortcut : Attribute, ICommandLineActionMetadata
    {
        public ArgAltShortcut(string shortcut)
        {
            this.Shortcut = shortcut;
        }
        
        public string Shortcut { get; }

    }
}
