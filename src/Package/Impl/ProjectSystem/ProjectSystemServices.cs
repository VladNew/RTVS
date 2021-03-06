﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.Common.Core;
using Microsoft.VisualStudio.R.Package.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using static System.FormattableString;

namespace Microsoft.VisualStudio.R.Package.ProjectSystem {
    [Export(typeof(IProjectSystemServices))]
    internal sealed class ProjectSystemServices : IProjectSystemServices {
        public EnvDTE.Solution GetSolution() {
            DTE dte = VsAppShell.Current.GlobalServices.GetService<DTE>();
            return dte.Solution;
        }

        public EnvDTE.Project GetActiveProject() {
            DTE dte = VsAppShell.Current.GlobalServices.GetService<DTE>();
            if (dte.Solution.Projects.Count > 0) {
                try {
                    return dte.Solution?.Projects?.Cast<EnvDTE.Project>()?.First();
                } catch (COMException) { }
            }
            return null;
        }

        /// <summary>
        /// Locates project that is currently active in Solution Explorer
        /// </summary>
        public T GetSelectedProject<T>() where T : class {
            var monSel = VsAppShell.Current.GlobalServices.GetService<IVsMonitorSelection>();
            IntPtr hierarchy = IntPtr.Zero, selectionContainer = IntPtr.Zero;
            uint itemid;
            IVsMultiItemSelect ms;

            try {
                if (VSConstants.S_OK == monSel.GetCurrentSelection(out hierarchy, out itemid, out ms, out selectionContainer)) {
                    if (hierarchy != IntPtr.Zero) {
                        return Marshal.GetObjectForIUnknown(hierarchy) as T;
                    }
                }
            } finally {
                if (hierarchy != IntPtr.Zero) {
                    Marshal.Release(hierarchy);
                }
                if (selectionContainer != IntPtr.Zero) {
                    Marshal.Release(selectionContainer);
                }
            }
            return null;
        }

        public void AddNewItem(string templateName, string name, string extension, string destinationPath) {
            var project = GetSelectedProject<IVsHierarchy>()?.GetDTEProject();
            if (project != null) {
                DTE dte = VsAppShell.Current.GlobalServices.GetService<DTE>();
                var solution = (Solution2)dte.Solution;

                // Construct name of the compressed template
                templateName = Path.ChangeExtension(templateName, "vstemplate");
                var templatePath = Path.Combine(GetProjectItemTemplatesFolder(), Path.GetFileNameWithoutExtension(templateName), templateName);

                // Given path to the project or a folder in it, generate unique file name
                var fileName = GetUniqueFileName(destinationPath, name, extension);

                // Locate folder in the project
                var projectFolder = Path.GetDirectoryName(project.FullName);
                if (destinationPath.StartsWithIgnoreCase(projectFolder)) {
                    ProjectItems projectItems = project.ProjectItems;

                    if (destinationPath.Length > projectFolder.Length) {
                        var relativePath = destinationPath.Substring(projectFolder.Length + 1);

                        // Go into folders and find project item to insert the file in
                        while (relativePath.Length > 0) {
                            int index = relativePath.IndexOf('\\');
                            string folder;
                            if (index >= 0) {
                                folder = relativePath.Substring(0, index);
                                relativePath = relativePath.Substring(index + 1);
                            } else {
                                folder = relativePath;
                                relativePath = string.Empty;
                            }
                            try {
                                var item = projectItems.Item(folder);
                                projectItems = item.ProjectItems;
                            } catch (COMException) {
                                return;
                            }
                        }
                    }
                    projectItems?.AddFromTemplate(templatePath, Path.GetFileName(fileName));
                }
            }
        }

        /// <summary>
        /// Given folder, prefix and extension generates unique file name in the project folder.
        /// </summary>
        public string GetUniqueFileName(string folder, string prefix, string extension, bool appendUnderscore = false) {
            string suffix = appendUnderscore ? "_" : string.Empty;
            string name = Path.ChangeExtension(Path.Combine(folder, prefix), extension);
            if (!File.Exists(name)) {
                return name;
            }

            for (int i = 1; ; i++) {
                name = Path.Combine(folder, Invariant($"{prefix}{suffix}{i}.{extension}"));
                if (!File.Exists(name)) {
                    return name;
                }
            }
        }

        /// <summary>
        /// Retrieves folder name of the project item templates
        /// </summary>
        public string GetProjectItemTemplatesFolder() {
            // In F5 (Experimental instance) scenario templates are deployed where the extension is.
            string assemblyPath = Assembly.GetExecutingAssembly().GetAssemblyPath();
            var templatesFolder = Path.Combine(Path.GetDirectoryName(assemblyPath), @"Templates\ItemTemplates\");
            if (!Directory.Exists(templatesFolder)) {
                // Real install scenario, templates are in 
                // C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\ItemTemplates\R
                string vsExecutableFileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                string vsFolder = Path.GetDirectoryName(vsExecutableFileName);
                templatesFolder = Path.Combine(vsFolder, @"ItemTemplates\R\");
            }
            return templatesFolder;
        }

        /// <summary>
        /// Enumerates all files in the project traversing into sub folders
        /// and items that have child elements.
        /// </summary>
        public IEnumerable<string> GetProjectFiles(EnvDTE.Project project) {
            return EnumerateProjectFiles(project?.ProjectItems);
        }

        /// <summary>
        /// Locates project by name
        /// </summary>
        public EnvDTE.Project GetProject(string projectName) {
            var projects = GetSolution()?.Projects;
            if (projects != null) {
                foreach (EnvDTE.Project p in projects) {
                    if (p.Name.EqualsOrdinal(projectName)) {
                        return p;
                    }
                }
            }
            return null;
        }

        private IEnumerable<string> EnumerateProjectFiles(EnvDTE.ProjectItems items) {
            if (items == null) {
                yield break;
            }
            foreach (var item in items) {
                var pi = item as EnvDTE.ProjectItem;
                var fullPath = (item as EnvDTE.ProjectItem)?.Properties?.Item("FullPath")?.Value as string;
                if (!string.IsNullOrEmpty(fullPath)) {
                    yield return fullPath;
                }
                if (pi.ProjectItems?.Count != 0) {
                    foreach (var x in EnumerateProjectFiles(pi.ProjectItems)) {
                        yield return x;
                    }
                }
            }
        }
    }
}
