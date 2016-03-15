﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Languages.Core.Formatting;
using Microsoft.Languages.Editor.Services;
using Microsoft.R.Core.AST;
using Microsoft.R.Core.AST.Definitions;
using Microsoft.R.Core.AST.Functions.Definitions;
using Microsoft.R.Core.AST.Scopes;
using Microsoft.R.Core.AST.Scopes.Definitions;
using Microsoft.R.Core.AST.Statements.Definitions;
using Microsoft.R.Core.Formatting;
using Microsoft.R.Editor.Document;
using Microsoft.R.Editor.Document.Definitions;
using Microsoft.R.Editor.Settings;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.R.Editor.SmartIndent {
    /// <summary>
    /// Provides block and smart indentation in R code
    /// </summary>
    internal sealed class SmartIndenter : ISmartIndent {
        private ITextView _textView;

        public static SmartIndenter Attach(ITextView textView) {
            SmartIndenter indenter = ServiceManager.GetService<SmartIndenter>(textView);

            if (indenter == null) {
                indenter = new SmartIndenter(textView);
            }

            return indenter;
        }

        private SmartIndenter(ITextView textView) {
            _textView = textView;
        }

        #region ISmartIndent;
        public int? GetDesiredIndentation(ITextSnapshotLine line) {
            int? res = GetDesiredIndentation(line, REditorSettings.IndentStyle);
            if (res != null && line.Snapshot.TextBuffer != _textView.TextBuffer) {
                var target = _textView.BufferGraph.MapUpToBuffer(
                    line.Start,
                    PointTrackingMode.Positive,
                    PositionAffinity.Successor,
                    _textView.TextBuffer
                );

                if (target != null) {
                    // The indentation level is relative to the line in the text view when
                    // we were created, not to the line we were provided with on this call.
                    var diff = target.Value.Position - target.Value.GetContainingLine().Start.Position;
                    return diff + res;
                }
            }
            return res;
        }

        public int? GetDesiredIndentation(ITextSnapshotLine line, IndentStyle indentStyle) {
            if (line != null) {
                if (indentStyle == IndentStyle.Block) {
                    return GetBlockIndent(line);
                } else if (indentStyle == IndentStyle.Smart) {
                    return GetSmartIndent(line);
                }
            }

            return null;
        }

        public void Dispose() {
        }
        #endregion

        public static int GetBlockIndent(ITextSnapshotLine line) {
            int lineNumber = line.LineNumber;

            //Scan the previous lines for the first line that isn't an empty line.
            while (--lineNumber >= 0) {
                ITextSnapshotLine previousLine = line.Snapshot.GetLineFromLineNumber(lineNumber);
                if (previousLine.Length > 0) {
                    return OuterIndentSizeFromLine(previousLine, REditorSettings.FormatOptions);
                }
            }

            return 0;
        }

        public static int GetSmartIndent(ITextSnapshotLine line, AstRoot ast = null) {
            ITextBuffer textBuffer = line.Snapshot.TextBuffer;
            ITextSnapshotLine prevLine = null;

            if (line.LineNumber == 0) {
                // Nothing to indent at the first line
                return 0;
            }

            if (ast == null) {
                IREditorDocument document = REditorDocument.TryFromTextBuffer(textBuffer);
                if (document == null) {
                    return 0;
                }
                var et = document.EditorTree;
                ast = (!et.IsReady && et.PreviousAstRoot != null) ? et.PreviousAstRoot : document.EditorTree.AstRoot;
            }

            // The challenge here is to find scope to base the indent on.
            // The scope may or may not have braces and may or may not be closed. 
            // Current line is normally empty so we use previous line data assuming 
            // it is not empty. If previous line is empty, we do not look up 
            // to the nearest non-empty. This is the same as C# behavior.
            // So we need to locate nearest node that implements IAstNodeWithScope
            // or the scope (implemeting IScope) itself is scope is just '{ }'.

            // First try based on the previous line. We will try start of the line
            // like in 'if(...)' { in order to locate 'if' and then, if nothing is found,
            // try end of the line as in 'x <- function(...) {'
            prevLine = line.Snapshot.GetLineFromLineNumber(line.LineNumber - 1);
            string prevLineText = prevLine.GetText();
            if(prevLineText.Trim().Equals("else", StringComparison.Ordinal)) {
                // Quick short circuit for new 'else' since it is not in the ASt yet.
                return GetBlockIndent(line) + REditorSettings.IndentSize;
            }

            int nonWsPosition = prevLine.Start + (prevLineText.Length - prevLineText.TrimStart().Length) + 1;

            // First, let's see if we are in a function argument list and then indent based on 
            // the opening brace position. This needs to be done before looking for scopes
            // since function definition is a scope-defining statement.
            // Examples: 'call(a,\n<Enter>' or 'x <- function(a,<Enter>'
            if (prevLine.Length > 0) {
                var fc = ast.GetNodeOfTypeFromPosition<IFunction>(prevLine.End - 1);
                if (fc != null && fc.Arguments != null && fc.OpenBrace != null) {
                    // We only want to indent here if position is in arguments
                    // and not in the function scope.
                    if (line.Start >= fc.OpenBrace.End && !(fc.CloseBrace != null && line.Start >= fc.CloseBrace.End)) {
                        return GetFirstArgumentIndent(textBuffer.CurrentSnapshot, fc);
                    }
                }
            }

            // First try new line so in case of 'if () { } else { | }' we find
            // the 'else' which defines the scope and not the parent 'if'.
            IAstNodeWithScope scopeStatement = ast.GetNodeOfTypeFromPosition<IAstNodeWithScope>(line.Start);
            if (scopeStatement == null) {
                // If not found, try previous line that may define the indent
                scopeStatement = ast.GetNodeOfTypeFromPosition<IAstNodeWithScope>(nonWsPosition);
                if (scopeStatement == null) {
                    // Line start position works for typical scope-defining statements like if() or while()
                    // but it won't find function definition in 'x <- function(a) {'
                    // Try end of the line instead
                    nonWsPosition = Math.Max(0, prevLineText.TrimEnd().Length - 1);
                    scopeStatement = ast.GetNodeOfTypeFromPosition<IAstNodeWithScope>(nonWsPosition);
                }
            }

            if (scopeStatement != null) {
                if (scopeStatement.Scope == null) {
                    // There is nothing after statement that allows simple scope
                    // such as in 'if(...)EOF'
                    return GetBlockIndent(line) + REditorSettings.IndentSize;
                }

                if (scopeStatement.Scope is SimpleScope) {
                    // There is statement with a simple scope above such as 'if' without { }. 
                    // We need to check if the line that is being formatted is part of this scope.
                    if (line.Start < scopeStatement.Scope.End) {
                        // Indent line one level deeper that the statement
                        return GetBlockIndent(line) + REditorSettings.IndentSize;
                    }
                    // Line is not part of the scope, provide regular indent
                    return OuterIndentSizeFromNode(textBuffer, scopeStatement, REditorSettings.FormatOptions);
                }

                // Check if line is the last line in a real scope (i.e. scope with { }) and only consists
                // of the closing }, it should be indented at the outer indent so closing scope aligns with
                // the beginning of the statement.
                if (scopeStatement.Scope.CloseCurlyBrace != null) {
                    int endOfScopeLine = textBuffer.CurrentSnapshot.GetLineNumberFromPosition(scopeStatement.Scope.CloseCurlyBrace.Start);
                    if (endOfScopeLine == line.LineNumber) {
                        return OuterIndentSizeFromNode(textBuffer, scopeStatement, REditorSettings.FormatOptions);
                    }
                }

                if (scopeStatement.Scope.OpenCurlyBrace != null && REditorSettings.FormatOptions.BracesOnNewLine) {
                    int startOfScopeLine = textBuffer.CurrentSnapshot.GetLineNumberFromPosition(scopeStatement.Scope.OpenCurlyBrace.Start);
                    if (startOfScopeLine == line.LineNumber) {
                        return OuterIndentSizeFromNode(textBuffer, scopeStatement, REditorSettings.FormatOptions);
                    }
                }

                // We are inside a scope so provide inner indent
                return InnerIndentSizeFromNode(textBuffer, scopeStatement, REditorSettings.FormatOptions);
            }

            // Try locate the scope itself, if any
            var scope = ast.GetNodeOfTypeFromPosition<IScope>(prevLine.End);
            if (scope != null && scope.OpenCurlyBrace != null) {
                if (scope.CloseCurlyBrace != null) {
                    int endOfScopeLine = textBuffer.CurrentSnapshot.GetLineNumberFromPosition(scope.CloseCurlyBrace.Start);
                    if (endOfScopeLine == line.LineNumber) {
                        return OuterIndentSizeFromNode(textBuffer, scope, REditorSettings.FormatOptions);
                    }
                }
                return InnerIndentSizeFromNode(textBuffer, scope, REditorSettings.FormatOptions);
            }

            return 0;
        }

        private static int GetFirstArgumentIndent(ITextSnapshot snapshot, IFunction fc) {
            var line = snapshot.GetLineFromPosition(fc.OpenBrace.End);
            return fc.OpenBrace.End - line.Start;
        }

        public static int InnerIndentSizeFromNode(ITextBuffer textBuffer, IAstNode node, RFormatOptions options) {
            if (node != null) {
                // Scope indentation is based on the scope defining node i.e.
                // x <- function(a) {
                //      |
                // }
                // caret indent is based on the function definition and not
                // on the position of the opening {
                var scope = node as IScope;
                if(scope != null) {
                    var scopeDefiningNode = node.Parent as IAstNodeWithScope;
                    if(scopeDefiningNode != null && scopeDefiningNode.Scope == scope) {
                        node = scopeDefiningNode;
                    }
                }
                ITextSnapshotLine startLine = textBuffer.CurrentSnapshot.GetLineFromPosition(node.Start);
                return InnerIndentSizeFromLine(startLine, options);
            }

            return 0;
        }

        public static int OuterIndentSizeFromNode(ITextBuffer textBuffer, IAstNode node, RFormatOptions options) {
            if (node != null) {
                ITextSnapshotLine startLine = textBuffer.CurrentSnapshot.GetLineFromPosition(node.Start);
                return OuterIndentSizeFromLine(startLine, options);
            }

            return 0;
        }

        public static int InnerIndentSizeFromLine(ITextSnapshotLine line, RFormatOptions options) {
            string lineText = line.GetText();
            string leadingWhitespace = lineText.Substring(0, lineText.Length - lineText.TrimStart().Length);
            IndentBuilder indentbuilder = new IndentBuilder(options.IndentType, options.IndentSize, options.TabSize);

            return IndentBuilder.TextIndentInSpaces(leadingWhitespace + indentbuilder.SingleIndentString, options.TabSize);
        }

        public static int OuterIndentSizeFromLine(ITextSnapshotLine line, RFormatOptions options) {
            string lineText = line.GetText();
            string leadingWhitespace = lineText.Substring(0, lineText.Length - lineText.TrimStart().Length);

            return IndentBuilder.TextIndentInSpaces(leadingWhitespace, options.TabSize);
        }
    }
}
