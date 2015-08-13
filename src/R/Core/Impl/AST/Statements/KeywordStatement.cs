﻿using System.Diagnostics;
using Microsoft.R.Core.AST.Definitions;
using Microsoft.R.Core.AST.Statements.Conditionals;
using Microsoft.R.Core.AST.Statements.Definitions;
using Microsoft.R.Core.AST.Statements.Loops;
using Microsoft.R.Core.Parser;
using Microsoft.R.Core.Tokens;

namespace Microsoft.R.Core.AST.Statements
{
    [DebuggerDisplay("[{Text}]")]
    public class KeywordStatement : Statement, IKeywordStatement
    {
        public TokenNode Keyword { get; private set; }

        public string Text { get; private set; }

        public override bool Parse(ParseContext context, IAstNode parent)
        {
            if (ParseKeyword(context, this))
            {
                if (ParseSemicolon(context, this))
                {
                    return base.Parse(context, parent);
                }
            }

            return false;
        }

        protected bool ParseKeyword(ParseContext context, IAstNode parent)
        {
            this.Keyword = RParser.ParseKeyword(context, this);
            this.Text = context.TextProvider.GetText(this.Keyword);

            return true;
        }

        /// <summary>
        /// Abstract factory
        /// </summary>
        public static IStatement CreateStatement(ParseContext context, IAstNode parent)
        {
            RToken currentToken = context.Tokens.CurrentToken;
            string keyword = context.TextProvider.GetText(currentToken);
            IStatement statement = null;

            switch (keyword)
            {
                case "if":
                    statement = new If();
                    break;

                case "for":
                    statement = new For();
                    break;

                case "while":
                    statement = new KeywordExpressionScopeStatement();
                    break;

                case "repeat":
                    statement = new KeywordScopeStatement(allowsSimpleScope: false);
                    break;

                case "break":
                case "next":
                    statement = new KeywordStatement();
                    break;

                case "return":
                    statement = new KeywordExpressionStatement();
                    break;

                case "library":
                    statement = new KeywordIdentifierStatement();
                    break;

                default:
                    context.AddError(new ParseError(ParseErrorType.UnexpectedToken, ErrorLocation.Token, currentToken));
                    break;
            }

            return statement;
        }
    }
}