﻿// GraphView
// 
// Copyright (c) 2015 Microsoft Corporation
// 
// All rights reserved. 
// 
// MIT License
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphView.TSQL_Syntax_Tree;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace GraphView
{
    public abstract partial class WSqlFragment 
    {
        public int FirstTokenIndex { get; set; }
        public int LastTokenIndex { get; set; }

        internal void UpdateTokenInfo(WSqlFragment fragment)
        {
            if (fragment == null)
                return;
            UpdateTokenInfo(fragment.FirstTokenIndex, fragment.LastTokenIndex);
        }
        
        internal void UpdateTokenInfo(TSqlFragment fragment)
        {
            if (fragment == null)
                return;
            UpdateTokenInfo(fragment.FirstTokenIndex, fragment.LastTokenIndex);
        }

        internal void UpdateTokenInfo(int firstIndex, int lastIndex)
        {
            if (firstIndex < 0 || lastIndex < 0)
                return;
            if (firstIndex > lastIndex)
            {
                var num = firstIndex;
                firstIndex = lastIndex;
                lastIndex = num;
            }
            if (firstIndex < FirstTokenIndex || FirstTokenIndex == -1)
                FirstTokenIndex = firstIndex;
            if (lastIndex <= LastTokenIndex && LastTokenIndex != -1)
                return;
            LastTokenIndex = lastIndex;
        }

        internal virtual bool OneLine()
        {
            return false;
        }

        internal virtual string ToString(string indent)
        {
            return "";
        }

        public override string ToString()
        {
            return ToString("");
        }

        public virtual void Accept(WSqlFragmentVisitor visitor)
        {
        }

        public virtual void AcceptChildren(WSqlFragmentVisitor visitor)
        {
        }
    }

    public partial class WSqlScript : WSqlFragment
    {
        internal List<WSqlBatch> Batches { set; get; }

        internal override bool OneLine()
        {
            return Batches.Count == 1 && Batches[0].OneLine();
        }

        internal override string ToString(string indent)
        {
            var sb = new StringBuilder(1024);

            for (var i = 0; i < Batches.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append("\r\n");
                }
                sb.Append(Batches[i].ToString(indent));
            }

            return sb.ToString();
        }

        public override void Accept(WSqlFragmentVisitor visitor)
        {
            if (visitor != null)
                visitor.Visit(this);
        }

        public override void AcceptChildren(WSqlFragmentVisitor visitor)
        {
            if (Batches != null)
            {
                var index = 0;
                for (var count = Batches.Count; index < count; ++index)
                    Batches[index].Accept(visitor);
            }
            base.AcceptChildren(visitor);
        }
    }

    public partial class WSqlBatch : WSqlFragment
    {
        internal List<WSqlStatement> Statements { set; get; }

        internal override bool OneLine()
        {
            return Statements.Count == 1 && Statements[0].OneLine();
        }

        internal override string ToString(string indent)
        {
            var sb = new StringBuilder();

            for (var i = 0; i < Statements.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append("\r\n");
                }
                sb.Append(Statements[i].ToString(indent));
            }

            return sb.ToString();
        }

        public override void Accept(WSqlFragmentVisitor visitor)
        {
            if (visitor != null)
                visitor.Visit(this);
        }

        public override void AcceptChildren(WSqlFragmentVisitor visitor)
        {
            if (Statements != null)
            {
                var index = 0;
                for (var count = Statements.Count; index < count; ++index)
                    Statements[index].Accept(visitor);
            }
            base.AcceptChildren(visitor);
        }
    }


    public abstract partial class WSqlStatement : WSqlFragment 
    {
        internal static string StatementListToString(IList<WSqlStatement> statementList, string indent)
        {
            if (statementList == null || statementList.Count == 0)
            {
                return "";
            }

            var sb = new StringBuilder(1024);

            for (var i = 0; i < statementList.Count; i++)
            {
                sb.AppendFormat("{0}", statementList[i].ToString(indent));

                if (sb[sb.Length - 1] != ';' && !(statementList[i] is WCommonTableExpression))
                {
                    sb.Append(';');
                }

                if (i < statementList.Count - 1)
                {
                    sb.Append("\r\n");
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Statements with optimization hints
    /// </summary>
    public abstract partial class WStatementWithCtesAndXmlNamespaces : WSqlStatement
    {
        public IList<OptimizerHint> OptimizerHints { get; set; }

        // Turns T-SQL OptimizerHint into string 
        internal static string OptimizerHintToString(OptimizerHint hint)
        {
            var sb = new StringBuilder(1024);
            // Literal hint
            if (hint is LiteralOptimizerHint)
            {
                sb.Append(TsqlFragmentToString.OptimizerHintKind(hint.HintKind));
                var loh = hint as LiteralOptimizerHint;

                // TODO: Only support numeric literal
                sb.AppendFormat(" {0}",loh.Value.Value);
            }
            // OptimizeFor hint
            else if (hint is OptimizeForOptimizerHint)
            {
                var ooh = hint as OptimizeForOptimizerHint;
                sb.AppendFormat("OPTIMIZE FOR ");
                if (ooh.IsForUnknown)
                    sb.Append("UNKNOWN");
                else
                {
                    sb.Append("(");
                    for (int i = 0; i < ooh.Pairs.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(", ");
                        sb.Append(ooh.Pairs[i].Variable.Name);

                        // TODO: Only support value expression
                        if (ooh.Pairs[i].Value != null && ooh.Pairs[i].Value is Literal)
                        {
                            if (ooh.Pairs[i].Value is StringLiteral)
                                sb.AppendFormat(" = '{0}'", ((Literal)ooh.Pairs[i].Value).Value);
                            else
                                sb.AppendFormat(" = {0}", ((Literal)ooh.Pairs[i].Value).Value);
                        }
                        else
                            sb.Append(" UNKNOWN");
                    }
                    sb.Append(")");

                }
            }
            // Table hint
            else if (hint is TableHintsOptimizerHint)
            {
                var toh = hint as TableHintsOptimizerHint;
                sb.Append("TABLE HINT ");
                sb.Append("(");
                sb.Append(TsqlFragmentToString.SchemaObjectName(toh.ObjectName));
                for (int i = 0; i < toh.TableHints.Count; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    // TODO: Table hint in WSQL Syntax tree is incomplete
                    sb.AppendFormat("@{0}", toh.TableHints[i].HintKind.ToString());
                }
                sb.Append(")");

            }
            // Regular hint
            else
            {
                sb.Append(TsqlFragmentToString.OptimizerHintKind(hint.HintKind));
            }
            return sb.ToString();

        }

        // Tranlates optimizer hint list into string
        internal string OptimizerHintListToString(string indent="")
        {
            if (OptimizerHints == null || !OptimizerHints.Any())
                return "";
            var sb = new StringBuilder(1024);
            sb.Append("\r\n");
            sb.AppendFormat("{0}OPTION", indent);
            sb.Append("(");
            for (int i = 0; i < OptimizerHints.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.AppendFormat("{0}", OptimizerHintToString(OptimizerHints[i]));
            }
            sb.Append(")");

            return sb.ToString();
        }
    }

    /// <summary>
    /// This class represents all T-SQL statements not identified by the current parser.
    /// Unidentified statements are interpreted token by token. 
    /// </summary>
    public partial class WSqlUnknownStatement : WSqlStatement
    {
        public IList<TSqlParserToken> TokenStream { get; set; }
        public string Statement { get; set; }

        public WSqlUnknownStatement()
        {
            TokenStream = new List<TSqlParserToken>();
        }

        public WSqlUnknownStatement(string statement)
        {
            Statement = statement;
        }

        public WSqlUnknownStatement(TSqlFragment statement)
        {
            TokenStream = new List<TSqlParserToken>(statement.LastTokenIndex - statement.FirstTokenIndex + 1);

            for (var pos = statement.FirstTokenIndex; pos <= statement.LastTokenIndex; pos++)
            {
                TokenStream.Add(statement.ScriptTokenStream[pos]);
            }
        }

        internal override string ToString(string indent)
        {
            var newLine = true;

            if (Statement != null) return indent + Statement;
            var sb = new StringBuilder(TokenStream.Count * 8);
            foreach (var token in TokenStream)
            {
                if (newLine)
                {
                    sb.Append(indent);
                }
                sb.Append(token.Text);

                newLine = false;

                if (token.TokenType != TSqlTokenType.WhiteSpace)
                    continue;
                if (token.Text.Equals("\r\n") || token.Text.Equals("\n"))
                {
                    newLine = true;
                }
            }
            return sb.ToString();
        }
    }

    public partial class WBeginEndBlockStatement : WSqlStatement
    {
        internal IList<WSqlStatement> StatementList { set; get; }

        internal override bool OneLine()
        {
            return false;
        }

        internal override string ToString(string indent)
        {
            var sb = new StringBuilder(1024);

            sb.AppendFormat("{0}BEGIN\r\n", indent);
            sb.Append(StatementListToString(StatementList, indent + "    "));
            sb.Append("\r\n");
            sb.AppendFormat("{0}END", indent);

            return sb.ToString();
        }

        public override void Accept(WSqlFragmentVisitor visitor)
        {
            if (visitor != null)
                visitor.Visit(this);
        }

        public override void AcceptChildren(WSqlFragmentVisitor visitor)
        {
            if (StatementList != null)
            {
                var index = 0;
                for (var count = StatementList.Count; index < count; ++index)
                    StatementList[index].Accept(visitor);
            }
            base.AcceptChildren(visitor);
        }
    }

    public partial class WWhileStatement : WSqlStatement
    {
        internal WBooleanExpression Predicate;
        internal WSqlStatement Statement;
        internal override bool OneLine()
        {
            return false;
        }
        internal override string ToString(string indent)
        {
            var sb = new StringBuilder(1024);

            sb.AppendFormat("{0}WHILE ", indent);
            sb.Append(Predicate.ToString() + "\r\n");
            sb.Append(Statement.ToString());
            sb.Append("\r\n");

            return sb.ToString();
        }

        public override void Accept(WSqlFragmentVisitor visitor)
        {
            if (visitor != null)
                visitor.Visit(this);
        }

        public override void AcceptChildren(WSqlFragmentVisitor visitor)
        {
            if (Predicate != null)
            {
                Predicate.Accept(visitor);
            }
            if (Statement != null)
            {
                Statement.Accept(visitor);
            }
            base.AcceptChildren(visitor);
        }
    }
}
