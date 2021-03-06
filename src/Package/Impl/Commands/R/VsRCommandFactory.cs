﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.Common.Core;
using Microsoft.Languages.Editor.Controller;
using Microsoft.R.Components.ContentTypes;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.VisualStudio.R.Package.Repl.Commands;
using Microsoft.VisualStudio.R.Packages.R;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.R.Package.Commands.R {
    [Export(typeof(ICommandFactory))]
    [ContentType(RContentTypeDefinition.ContentType)]
    internal class VsRCommandFactory : ICommandFactory {
        private readonly IRInteractiveWorkflowProvider _workflowProvider;

        [ImportingConstructor]
        public VsRCommandFactory(IRInteractiveWorkflowProvider workflowProvider) {
            _workflowProvider = workflowProvider;
        }

        public IEnumerable<ICommand> GetCommands(ITextView textView, ITextBuffer textBuffer) {
            var workflow = _workflowProvider.GetOrCreate();

            if (workflow.ActiveWindow == null) {
                workflow.GetOrCreateVisualComponentAsync()
                    .ContinueOnRanToCompletion(w => w.Container.Show(focus: false, immediate: false));
            }

            return new ICommand[] {
                new ShowContextMenuCommand(textView, RGuidList.RPackageGuid, RGuidList.RCmdSetGuid, (int) RContextMenuId.R),
                new SendToReplCommand(textView, workflow),
                new ClearReplCommand(textView, _workflowProvider.GetOrCreate()),
                new GoToFormattingOptionsCommand(textView, textBuffer),
                new WorkingDirectoryCommand(workflow)
            };
        }
    }
}
