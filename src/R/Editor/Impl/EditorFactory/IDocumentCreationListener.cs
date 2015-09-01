﻿using Microsoft.R.Editor.Document;

namespace Microsoft.Html.Editor.EditorFactory
{
    /// <summary>
    /// Exported via MEF. When document is created
    /// every exported listener will be called.
    /// </summary>
    public interface IHtmlDocumentCreationListener
    {
        void DocumentCreated(EditorDocument document);
    }
}