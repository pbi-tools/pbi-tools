// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PowerArgs;

namespace PbiTools
{
    public static class CmdLineArgumentsDefinitionExtensions
    {
        public static CommandLineArgumentsDefinition For<T>()
        {
            return new CommandLineArgumentsDefinition(typeof(T));
        }

        /// <summary>
        /// Removes all auto-generated action aliases, leaving only explicitly declared ones.
        /// </summary>
        public static CommandLineArgumentsDefinition RemoveAutoAliases(this CommandLineArgumentsDefinition def)
        {
            foreach (var action in def.Actions)
            {
                if (action.Source is MethodInfo method)
                {
                    if (action.DefaultAlias == method.Name && action.Aliases.Count > 1)
                        action.Aliases.Remove(action.DefaultAlias);
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
            /* this is not getting called for ArgsUsage.GenerateFromTemplate() */
            context.Definition.RemoveAutoAliases();
        }

        //public override void BeforeParse(HookContext context)
        //{
        //    context.Definition.RemoveAutoAliases();
        //}

        public override void BeforePrepareUsage(HookContext context)
        {
            context.Definition.RemoveAutoAliases();

            // remove hidden args:
            new List<CommandLineAction>(
                context.Definition.Actions.Where(a => a.Metadata.HasMeta<OmitFromUsageDocs>())
            )
            .ForEach(a => context.Definition.Actions.Remove(a));
        }

    }

}