// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace PbiTools.ProjectSystem
{
    public static class SettingsExtensions
    {
        public static bool IsDefault(this IHasDefaultValue settings) =>
            settings == default || settings.IsDefault;

        public static bool IsDefault<T>(this T[] settings) =>
            settings == null || settings.Length == 0;
    }
}
