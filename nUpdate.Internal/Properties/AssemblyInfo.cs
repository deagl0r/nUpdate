// AssemblyInfo.cs, 10.06.2019
// Copyright (C) Dominic Beger 17.06.2019

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Durch Festlegen von ComVisible auf "false" werden die Typen in dieser Assembly unsichtbar 
// für COM-Komponenten. Wenn Sie auf einen Typ in dieser Assembly von 
// COM zugreifen müssen, legen Sie das ComVisible-Attribut für diesen Typ auf "true" fest.

[assembly: ComVisible(true)]

// Die folgende GUID bestimmt die ID der Typbibliothek, wenn dieses Projekt für COM verfügbar gemacht wird

[assembly: Guid("9eee07b4-de28-44f7-99ff-6747fbf96913")]
[assembly: InternalsVisibleTo("nUpdate.Shared")]
[assembly: InternalsVisibleTo("nUpdate.ProvideTAP")]
[assembly: InternalsVisibleTo("nUpdate.WithoutTAP")]
[assembly: InternalsVisibleTo("nUpdate.UpdateInstaller")]
[assembly: InternalsVisibleTo("nUpdate.WPFUserInterface")]
[assembly: InternalsVisibleTo("nUpdate.WPFUpdateInstaller")]