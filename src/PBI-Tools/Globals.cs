// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("pbi-tools.tests")]
[assembly: InternalsVisibleTo("pbi-tools.netcore.tests")]
[assembly: InternalsVisibleTo("pbi-tools.net6.tests")]

#if NETFRAMEWORK

namespace System.Runtime.CompilerServices
{
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}

#endif