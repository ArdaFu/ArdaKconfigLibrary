//------------------------------------------------------------------------------
//  Copyright(C) FU Pengfei, 2007-2018.
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License along
//  with this program; if not, write to the Free Software Foundation, Inc.,
//  51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
//------------------------------------------------------------------------------
//  Project    : Arda Kconfig Library
//  Description: Kconfig UI for rendering detail view of menu entry.
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2018-06-023   first implementation
//------------------------------------------------------------------------------
//  $Id:: UcKconfigDetailView.xaml.cs 1815 2018-06-27 05:02:17Z arda           $
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;


namespace Arda.ArmDevTool.Kconfig
{
    /// <summary>
    /// Interaction logic for UcKconfigDetail.xaml
    /// </summary>
    public partial class UcKconfigDetailView
    {

        /// <summary>
        /// User provide method to jump to select MenuEntry.
        /// </summary>
        public Action<MenuEntry> ActionJumpToMenuEntry;

        public UcKconfigDetailView()
        {
            InitializeComponent();
        }

        #region DependencyProperty MenuEntry

        public static readonly DependencyProperty MenuEntryProperty =
            DependencyProperty.Register("MenuEntry", typeof(MenuEntry),
                typeof(UcKconfigDetailView), new FrameworkPropertyMetadata
                    (null, OnMenuEntryChanged));

        /// <summary>
        /// Detail view will render this menu entry.
        /// </summary>
        public MenuEntry MenuEntry
        {
            get => (MenuEntry) GetValue(MenuEntryProperty);

            set => SetValue(MenuEntryProperty, value);
        }

        public static void OnMenuEntryChanged(DependencyObject obj,
            DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue != null && obj is UcKconfigDetailView view)
            {
                view.RtBox.Document = RenderMenuEntry(args.NewValue as MenuEntry, view);
            }
        }

        #endregion //DependencyProperty MenuEntry

        #region Render MenuEntry

        /// <summary>
        /// Using this as help message when help attribute is not found.
        /// </summary>
        private const string StrNoHelp = @"There is no help available.";

        /// <summary>
        /// Render MenuEntry as FlowDocument
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="view"></param>
        /// <returns></returns>
        private static FlowDocument RenderMenuEntry(MenuEntry entry, UcKconfigDetailView view)
        {
            // prompt
            var doc = new FlowDocument();
            {
                var para = new Paragraph()
                {
                    Foreground = Brushes.DarkRed,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16
                };
                para.Inlines.Add(entry.Prompt ?? "No Prompt");
                doc.Blocks.Add(para);
            }

            // name
            if (entry.EntryType == MenuEntryType.Config || entry.EntryType == MenuEntryType.MenuConfig)
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    var para = new Paragraph() {Foreground = Brushes.DarkRed, Margin = new Thickness(5), FontSize = 14};
                    para.Inlines.Add(entry.Name?? "No Name");
                    doc.Blocks.Add(para);
                }

            // help
            {
                var para = new Paragraph() {Margin = new Thickness(20, 5, 5, 5)};

                var help = entry.Help;
                if (help == null)
                {
                    para.Inlines.Add(StrNoHelp);
                    para.Foreground = Brushes.Gray;
                }
                else
                    para.Inlines.Add(help);

                doc.Blocks.Add(para);
            }

            var propList = new List()
            {
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(5, 5, 5, 10)
            };

            // be selected by
            if (entry.BeSelectedList.Count != 0)
            {
                propList.ListItems.Add(RenderForwardDependencyList(
                    entry.BeSelectedList, "Be selected by: ".PadRight(16), view));
            }

            // be implied by
            if (entry.BeImpliedList.Count != 0)
            {
                propList.ListItems.Add(RenderForwardDependencyList(
                    entry.BeImpliedList, "Be implied by: ".PadRight(16), view));
            }

            // depends on
            if (entry.DependsOnExpr != null)
            {
                var para = new Paragraph();
                para.Inlines.Add(new Run()
                {
                    FontWeight = FontWeights.Bold,
                    Text = "Depends on: ".PadRight(12)
                });
                RenderExpression(entry.DependsOnExpr, para, view);
                propList.ListItems.Add(new ListItem(para));
            }

            // Attributes
            if (entry.Attributes.Count != 0)
            {
                foreach (var attr in entry.Attributes)
                {
                    // skip depends on
                    if (attr.AttributeType == MenuAttributeType.DependsOn)
                        continue;

                    // skip help and prompt without condition.
                    if (attr.AttributeType == MenuAttributeType.Help
                        || attr.AttributeType == MenuAttributeType.Prompt)
                        if (string.IsNullOrEmpty(attr.Condition))
                            continue;

                    var para = new Paragraph();
                    // title
                    para.Inlines.Add(new Run()
                    {
                        FontWeight = FontWeights.Bold,
                        Text = $"{attr.AttributeType}: ".PadRight(12)
                    });

                    // type
                    if (attr.ExpressionType != MenuAttributeType.Invalid)
                        para.Inlines.Add($"{attr.ExpressionType}");

                    // Reverse Dependency HyperLink
                    if (attr.AttributeType == MenuAttributeType.Imply
                        || attr.AttributeType == MenuAttributeType.Select)
                    {
                        if (attr.ReverseDependency != null)
                            para.Inlines.Add(CreateHyperlink(attr.ReverseDependency, view));
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(attr.SymbolValue))
                            para.Inlines.Add(attr.SymbolValue);
                    }

                    // Condition
                    if (attr.ConditionExpr != null)
                    {
                        para.Inlines.Add(attr.AttributeType == MenuAttributeType.DependsOn
                            ? " "
                            : " if ");
                        RenderExpression(attr.ConditionExpr, para, view);
                    }

                    propList.ListItems.Add(new ListItem(para));
                }

            }

            doc.Blocks.Add(propList);

            // location
            if (entry.Location != null)
            {
                var para = new Paragraph()
                {
                    Foreground = Brushes.Green,
                    Margin = new Thickness(5),
                };
                para.Inlines.Add(entry.Location.ToString());
                doc.Blocks.Add(para);
            }

            return doc;
        }

        /// <summary>
        /// Render Forward Dependency list to a paragraph with given attribute title.
        /// </summary>
        /// <param name="entries"></param>
        /// <param name="title"></param>
        /// <param name="view"></param>
        /// <returns></returns>
        private static ListItem RenderForwardDependencyList(IEnumerable<MenuEntry> entries,
            string title, UcKconfigDetailView view)
        {
            var para = new Paragraph();
            para.Inlines.Add(new Run() {FontWeight = FontWeights.Bold, Text = title});
            var isFirst = true;
            foreach (var menuEntry in entries)
            {
                if (isFirst)
                    isFirst = false;
                else
                    para.Inlines.Add(", ");
                para.Inlines.Add(CreateHyperlink(menuEntry, view));
            }

            return new ListItem(para);
        }

        /// <summary>
        /// Render standard 2 elements expression.
        /// </summary>
        /// <param name="para"></param>
        /// <param name="expr"></param>
        /// <param name="operatorStr"></param>
        /// <param name="view"></param>
        /// <param name="isHaveBrackets"></param>
        private static void RenderExpression(Expression expr, Paragraph para,
            string operatorStr, UcKconfigDetailView view, bool isHaveBrackets)
        {
            if (isHaveBrackets)
                para.Inlines.Add("(");
            RenderExpression(expr.Left, para, view);
            para.Inlines.Add($" {operatorStr} ");
            RenderExpression(expr.Right, para, view);
            if (isHaveBrackets)
                para.Inlines.Add(")");
        }

        /// <summary>
        /// Render expression data.
        /// </summary>
        /// <param name="exprData"></param>
        /// <param name="para"></param>
        /// <param name="view"></param>
        private static void RenderExpression(ExpressionData exprData,
            Paragraph para, UcKconfigDetailView view)
        {
            if (exprData.Expr == null)
            {
                if (exprData.Symbol.IsConst)
                    para.Inlines.Add(new Run($"\"{exprData.Symbol.Name}\"")
                    {
                        Foreground = Brushes.DarkGreen
                    });
                else
                    para.Inlines.Add(CreateHyperlink(exprData.Symbol, view));
                return;
            }

            RenderExpression(exprData.Expr, para, view, true);
        }

        /// <summary>
        /// Render expression.
        /// </summary>
        /// <param name="expr"></param>
        /// <param name="para"></param>
        /// <param name="view"></param>
        /// <param name="isHaveBrackets"></param>
        private static void RenderExpression(Expression expr, Paragraph para,
            UcKconfigDetailView view, bool isHaveBrackets = false)
        {
            switch (expr.Type)
            {
                case ExpressionType.And:
                    RenderExpression(expr, para, "&&", view, isHaveBrackets);
                    return;
                case ExpressionType.Or:
                    RenderExpression(expr, para, "||", view, isHaveBrackets);
                    return;
                case ExpressionType.Equal:
                    RenderExpression(expr, para, "==", view, isHaveBrackets);
                    return;
                case ExpressionType.NoEuqal:
                    RenderExpression(expr, para, "!=", view, isHaveBrackets);
                    return;
                case ExpressionType.Not:
                    para.Inlines.Add(isHaveBrackets ? "(! " : "! ");
                    RenderExpression(expr.Right, para, view);
                    if (isHaveBrackets)
                        para.Inlines.Add(")");
                    return;
                case ExpressionType.None:
                    RenderExpression(expr.Right, para, view);
                    return;
                case ExpressionType.N:
                    para.Inlines.Add(new Run("n") {Foreground = Brushes.Brown});
                    return;

                case ExpressionType.M:
                    para.Inlines.Add(new Run("m") {Foreground = Brushes.Brown});
                    return;

                case ExpressionType.Y:
                    para.Inlines.Add(new Run("y") {Foreground = Brushes.Brown});
                    return;

                default:
                    return;
            }
        }

        /// <summary>
        /// Create hyper link for each menu entry.
        /// Jump to the MenuEntry when user LMB click on it.
        /// </summary>
        /// <param name="entry">jump target</param>
        /// <param name="view">UI element to provide ActionJumpToMenuEntry</param>
        /// <returns></returns>
        private static Hyperlink CreateHyperlink(MenuEntry entry, UcKconfigDetailView view)
        {
            var hl = new Hyperlink()
            {
                Inlines = {new Run($"{entry.Name}")},
                Foreground = entry.IsVisible ? Brushes.Blue : Brushes.DarkMagenta,
                Focusable = true,
            };
            hl.Inlines.Add(new Run($"(={entry.Value})") {Foreground = Brushes.Brown});

            if (entry.IsVisible && view.ActionJumpToMenuEntry != null)
                hl.MouseLeftButtonDown += (sender, e) => view.ActionJumpToMenuEntry(entry);
            return hl;
        }

        #endregion Render MenuEntry
    }
}
