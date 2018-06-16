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
//  Description: Kconfig UI for rendering tree view of menu entry.
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2018-06-05   first implementation
//------------------------------------------------------------------------------
//  $Id:: UcKconfigTreeView.xaml.cs 1806 2018-06-16 07:49:08Z fupengfei        $
//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace Arda.ArmDevTool.Kconfig
{
    /// <summary>
    /// Interaction logic for UcKconfigTreeView.xaml
    /// </summary>
    public partial class UcKconfigTreeView : INotifyPropertyChanged
    {
        public UcKconfigTreeView()
        {
            InitializeComponent();
            TreeView.SelectedItemChanged += (sender,e)=> OnPropertyChanged(nameof(SelectedItem)); 
        }

        public object SelectedItem => TreeView.SelectedItem;

        public IEnumerable ItemsSource
        {
            get => TreeView.ItemsSource;
            set
            {
                TreeView.ItemsSource = value;
                OnPropertyChanged(nameof(ItemsSource));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    [ValueConversion(typeof(MenuEntryType), typeof(string))]
    public class EntryTypeToIconPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "";
            var entryType = (MenuEntryType)value;
            switch (entryType)
            {
                case MenuEntryType.MainMenu:
                    return "Resources/mainmenu.png";
                case MenuEntryType.Menu:
                    return "Resources/menu.png";
                case MenuEntryType.Config:
                    return "Resources/config.png";
                case MenuEntryType.MenuConfig:
                    return "Resources/menuconfig.png";
                case MenuEntryType.Comment:
                    return "Resources/comment.png";
                case MenuEntryType.If:
                    return "Resources/if.png";
                case MenuEntryType.Choice:
                    return "Resources/choice.png";
                case MenuEntryType.Source:
                    return "Resources/source.png";
                default:
                    return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(string), typeof(bool?))]
    public class ValueToTristateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;
            switch (value.ToString())
            {
                case "y":
                    return true;
                case "m":
                    return null;
                default:
                    return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "m";
            return (bool)value ? "y" : "n";
        }
    }

    [ValueConversion(typeof(string), typeof(bool?))]
    public class ValueTypeToIsTristateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "";
            var valueType = (MenuAttributeType)value;
            return valueType == MenuAttributeType.Tristate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(MenuEntry), typeof(string))]
    public class MenuEntryToToolTipConverter : IValueConverter
    {
        internal static readonly Regex RemoveEndBlankRegex =
            new Regex(@"^(?<string>(.|\n)*?)\s*$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is MenuEntry entry))
            {
                return null;
            }
            var sb = new StringBuilder();
            sb.AppendLine(string.IsNullOrEmpty(entry.Name)
                ? $"[{entry.EntryType}] {entry.Prompt}"
                : $"[{entry.EntryType}] {entry.Name}");
            sb.AppendLine($"{entry.Location}");
            if (entry.NestDependsOnExpression != null)
                sb.AppendLine($"DependsOn = {entry.NestDependsOnExpression}");
            foreach (var menuEntry in entry.BeSelectedList)
                sb.AppendLine($"[Be selected by] {menuEntry.Name}");
            foreach (var menuEntry in entry.BeImpliedList)
                sb.AppendLine($"[Be implied by] {menuEntry.Name}");

            foreach (var attr in entry.Attributes)
                sb.AppendLine(attr.ToString());
            return RemoveEndBlankRegex.Match(sb.ToString()).Groups["string"].Value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ChoiceToValuePromptConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value.Length != 2)
                return null;
            if (!(value[0] is string selectName) || !(value[1] is List<MenuEntry> children))
            {
                return null;
            }

            var selectedChildEntry = children.First(child => child.Name == selectName);

            return selectedChildEntry?.Prompt;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class MenuEntryDataTemplateSelector : DataTemplateSelector
    {
        private static readonly UcKconfigTreeView Parent= new UcKconfigTreeView();

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (!(item is MenuEntry entry))
                return null;

            switch (entry.EntryType)
            {
                case MenuEntryType.Config:
                    switch (entry.ValueType)
                    {
                        case MenuAttributeType.Bool:
                        case MenuAttributeType.Tristate:
                            return Parent.FindResource("ConfigTristateTemplate") as DataTemplate;

                        case MenuAttributeType.Hex:
                        case MenuAttributeType.Int:
                        case MenuAttributeType.String:
                            return Parent.FindResource("ConfigStringTemplate") as DataTemplate;
                        default:
                            return null;
                    }

                case MenuEntryType.MenuConfig:
                    return Parent.FindResource("MenuConfigTemplate") as DataTemplate;

                case MenuEntryType.Choice:
                    return Parent.FindResource("ChoiceTemplate") as DataTemplate;

                default:
                    return Parent.FindResource("MenuEntryTemplate") as DataTemplate;
            }
        }
    }
}
