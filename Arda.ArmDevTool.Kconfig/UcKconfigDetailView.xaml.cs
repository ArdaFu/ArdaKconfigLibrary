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
//  $Id:: UcKconfigDetailView.xaml.cs 1810 2018-06-23 10:04:42Z fupengfei      $
//------------------------------------------------------------------------------

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
                view.RtBox.Document = Convert(args.NewValue as MenuEntry);
            }
        }

        public UcKconfigDetailView()
        {
            InitializeComponent();
        }

        private const string StrNoHelp = "There is no help available for this option.";

        private static FlowDocument Convert(MenuEntry entry)
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
                para.Inlines.Add(entry.Prompt);
                doc.Blocks.Add(para);
            }

            // name
            if (entry.EntryType == MenuEntryType.Config || entry.EntryType == MenuEntryType.MenuConfig)
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    var para = new Paragraph() {Foreground = Brushes.DarkRed, Margin = new Thickness(5), FontSize = 14};
                    para.Inlines.Add(entry.Name);
                    doc.Blocks.Add(para);
                }

            // help
            {
                var para = new Paragraph() {Margin = new Thickness(5)};

                var attr = entry.FindFirstAvaliable(MenuAttributeType.Help);
                if (attr == null)
                {
                    para.Inlines.Add(StrNoHelp);
                    para.Foreground = Brushes.Gray;
                }
                else
                    para.Inlines.Add(attr.SymbolValue);

                doc.Blocks.Add(para);
            }

            // location
            if (entry.Location != null)
            {
                var para = new Paragraph()
                {
                    Foreground = Brushes.Green,
                    Margin = new Thickness(5, 0, 5, 0),
                };
                para.Inlines.Add(entry.Location.ToString());
                doc.Blocks.Add(para);
            }

            // depends on
            var propList = new List() {FontFamily = new FontFamily("Consolas"), Margin = new Thickness(5, 5, 5, 10)};
            if (entry.DependsOnExpr != null)
            {
                var para = new Paragraph();
                para.Inlines.Add(new Run() {FontWeight = FontWeights.Bold, Text = "Depends on: "});
                AddExpressionHyperlinks(entry.DependsOnExpr, para);
                propList.ListItems.Add(new ListItem(para));
            }

            // selected by
            if (entry.BeSelectedList.Count != 0)
            {
                propList.ListItems.Add(
                    GenerateForwardDepenceyAttribute(entry.BeSelectedList, "Be selected by: "));
            }

            // implied by
            if (entry.BeImpliedList.Count != 0)
            {
                propList.ListItems.Add(
                    GenerateForwardDepenceyAttribute(entry.BeImpliedList, "Be implied by: "));
            }

            // Attributes
            if (entry.Attributes.Count != 0)
            {

                foreach (var attr in entry.Attributes)
                {
                    if (attr.AttributeType == MenuAttributeType.Help || attr.AttributeType == MenuAttributeType.Prompt)
                        if (string.IsNullOrEmpty(attr.Condition))
                            continue;

                    var para = new Paragraph();
                    para.Inlines.Add(new Run() {FontWeight = FontWeights.Bold, Text = $"{attr.AttributeType}: "});

                    if (attr.ExpressionType != MenuAttributeType.Invalid)
                        para.Inlines.Add($"{attr.ExpressionType}");

                    if (attr.AttributeType == MenuAttributeType.Imply || attr.AttributeType == MenuAttributeType.Select)
                    {
                        if (attr.ReverseDependency != null)
                            para.Inlines.Add(CreateHyperlink(attr.ReverseDependency));
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(attr.SymbolValue))
                            para.Inlines.Add($"{attr.SymbolValue}");
                    }

                    if (attr.ConditionExpr != null)
                    {
                        para.Inlines.Add(attr.AttributeType == MenuAttributeType.DependsOn
                            ? " "
                            : " if ");
                        AddExpressionHyperlinks(attr.ConditionExpr, para);
                    }

                    propList.ListItems.Add(new ListItem(para));
                }

            }

            doc.Blocks.Add(propList);
            return doc;
        }

        private static ListItem GenerateForwardDepenceyAttribute(IEnumerable<MenuEntry> entries, string title)
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
                para.Inlines.Add(CreateHyperlink(menuEntry));
            }

            return new ListItem(para);
        }

        private static void AddExpressionHyperlinks(Expression expr, Paragraph para, bool isHaveBrackets = false)
        {
            switch (expr.Type)
            {
                case ExpressionType.And:
                    if (isHaveBrackets)
                        para.Inlines.Add("(");
                    AddExpressionHyperlinks(expr.Left, para);
                    para.Inlines.Add(" && ");
                    AddExpressionHyperlinks(expr.Right, para);
                    if (isHaveBrackets)
                        para.Inlines.Add(")");
                    return;
                // return $"({Left} && {Right})";
                case ExpressionType.Or:
                    if (isHaveBrackets)
                        para.Inlines.Add("(");
                    AddExpressionHyperlinks(expr.Left, para);
                    para.Inlines.Add(" || ");
                    AddExpressionHyperlinks(expr.Right, para);
                    if (isHaveBrackets)
                        para.Inlines.Add(")");
                    return;
                case ExpressionType.Equal:
                    if (isHaveBrackets)
                        para.Inlines.Add("(");
                    AddExpressionHyperlinks(expr.Left, para);
                    para.Inlines.Add(" = ");
                    AddExpressionHyperlinks(expr.Right, para);
                    if (isHaveBrackets)
                        para.Inlines.Add(")");
                    return;
                case ExpressionType.NoEuqal:
                    if (isHaveBrackets)
                        para.Inlines.Add("(");
                    AddExpressionHyperlinks(expr.Left, para);
                    para.Inlines.Add(" != ");
                    AddExpressionHyperlinks(expr.Right, para);
                    if (isHaveBrackets)
                        para.Inlines.Add(")");
                    return;
                case ExpressionType.Not:
                    para.Inlines.Add(isHaveBrackets ? "(! " : "! ");
                    AddExpressionHyperlinks(expr.Right, para);
                    if (isHaveBrackets)
                        para.Inlines.Add(")");
                    return;
                case ExpressionType.None:
                    AddExpressionHyperlinks(expr.Right, para);
                    return;
                case ExpressionType.N:
                    para.Inlines.Add(new Run($"n") {Foreground = Brushes.Brown});
                    return;

                case ExpressionType.M:
                    para.Inlines.Add(new Run($"m") {Foreground = Brushes.Brown});
                    return;

                case ExpressionType.Y:
                    para.Inlines.Add(new Run($"y") {Foreground = Brushes.Brown});
                    return;

                default:
                    para.Inlines.Add("");
                    return;
            }
        }

        private static void AddExpressionHyperlinks(ExpressionData exprData, Paragraph para)
        {
            if (exprData.Expr == null)
            {
                if (exprData.Symbol.IsConst)
                    para.Inlines.Add(new Run($"\"{exprData.Symbol.Name}\"") {Foreground = Brushes.DarkGreen});
                else
                    para.Inlines.Add(CreateHyperlink(exprData.Symbol));
                return;
            }

            AddExpressionHyperlinks(exprData.Expr, para, true);
        }

        /// <summary>
        /// Create hyperlink for each menuentry. Jump to the menuebtry when user LMB click on it.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private static Hyperlink CreateHyperlink(MenuEntry entry)
        {
            var hl = new Hyperlink() {Inlines = {new Run($"{entry.Name}")}};
            hl.Inlines.Add(new Run($"(={entry.Value})") {Foreground = Brushes.Brown});
            hl.MouseLeftButtonDown += (sender, e) =>
            {
                // jump to seletced meny entry
                var selectEntry = entry;
                var parent = entry.ParentEntry;
                while (parent != null)
                {
                    if (!selectEntry.IsVisible)
                        selectEntry = parent;
                    parent.IsExpanded = true;
                    parent = parent.ParentEntry;
                }
                selectEntry.IsSelected = true;
            };
            return hl;
        }
    }
}
