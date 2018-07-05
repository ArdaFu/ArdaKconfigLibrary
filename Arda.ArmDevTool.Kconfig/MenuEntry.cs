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
//  Description: Kconfig element
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: MenuEntry.cs 1817 2018-07-05 06:45:58Z fupengfei                     $
//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Arda.ArmDevTool.Kconfig
{

    /// <summary>
    /// Kconfig menu entry
    /// </summary>
    public class MenuEntry : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        /// <summary>
        /// Entry type
        /// <para>Can be "Config\MenuConfig\Choice\Comment\Menu\If\MainMenu"</para>
        /// </summary>
        public MenuEntryType EntryType { get; set; }

        /// <summary>
        /// Entry's attribute list
        /// </summary>
        public List<MenuAttribute> Attributes { get; set; }

        /// <summary>
        /// Entry's child entry list
        /// </summary>
        public List<MenuEntry> ChildEntries { get; set; }

        /// <summary>
        /// Entry's parent entry
        /// </summary>
        public MenuEntry ParentEntry { get; set; }

        /// <summary>
        /// Entry location information
        /// </summary>
        public EntryLocation Location { get; set; }

        /// <summary>
        /// Entry name 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Entry value
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;
                ProcessValueChange(value);
            }
        }

        /// <summary>
        /// Check if value is valid and per-process config under choice.
        /// </summary>
        /// <param name="value"></param>
        private void ProcessValueChange(string value)
        {
            if (!CheckValueValid(value, out var error))
            {
                _value = value;
                SetErrors(nameof(Value), new List<string>() {error});
                return;
            }

            ClearErrors(nameof(Value));

            if ((ParentEntry == null)
                || (ParentEntry.EntryType != MenuEntryType.Choice))
            {
                _value = value;
                Calculate(this);
                OnPropertyChanged(nameof(Value));
                return;
            }

            // when change configure value under choice, 
            // we should also modify choice value 
            Enum.TryParse(value, true, out TristateValue val);
            switch (val)
            {
                case TristateValue.Y:
                    // choice's calculate will handling this.
                    ParentEntry.Value = Name;
                    return;
                case TristateValue.N:
                    if (ParentEntry.Value == Name &&
                        ParentEntry.IsHaveOptionalOption)
                        ParentEntry.Value = null;
                    return;
                default: // "M"
                    _value = value;
                    Calculate(this);
                    OnPropertyChanged(nameof(Value));
                    break;
            }
        }

        /// <summary>
        /// Constant symbols are only part of expressions. 
        /// Constant symbols are always surrounded by single or double quotes.
        /// Within the quote, any other character is allowed and the quotes can be escaped using '\'.
        /// </summary>
        public bool IsConst { get; set; }

        /// <summary>
        /// Create a kconfig menu entry
        /// </summary>
        public MenuEntry()
        {
            Attributes = new List<MenuAttribute>();
            ChildEntries = new List<MenuEntry>();
            ValueType = MenuAttributeType.Invalid;

            _errors = new Dictionary<string, List<string>>();

            DependsOnList = new HashSet<MenuEntry>();
            BeSelectedList = new HashSet<MenuEntry>();
            BeImpliedList = new HashSet<MenuEntry>();
        }

        /// <summary>
        /// Format is "[&lt;EntryType&gt;] &lt;Name&gt; = &lt;Value&gt;"
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Value)
                ? $"[{EntryType}] {Name}"
                : $"[{EntryType}] {Name} = {Value}";
        }

        /// <summary>
        /// Prompt after calculate
        /// </summary>
        public string Prompt
        {
            get => _prompt;
            set => SetProperty(ref _prompt, value);
        }

        /// <summary>
        /// ValueType after calculate
        /// </summary>
        public MenuAttributeType ValueType { get; set; }

        /// <summary>
        /// Default value after calculate
        /// </summary>
        public string Default
        {
            get => _default;
            set=>SetProperty(ref _default, value);
        }

        /// <summary>
        /// indicate entry is visible.
        /// This property is used with IsFiltered to control the visibility on UI 
        /// </summary>
        public bool IsVisible
        {
            get => _isVisable;
            set => SetProperty(ref _isVisable, value);
        }

        /// <summary>
        /// Indicate entry is filtered when search filter is enable.
        /// This property is used with IsVisible to control the visibility on UI 
        /// </summary>
        public bool IsFiltered
        {
            get => _isFiltered;
            set => SetProperty(ref _isFiltered, value);
        }


        /// <summary>
        /// indicate entry is enable in .config file
        /// </summary>
        public bool IsEnable
        {
            get => _isEnable;
            set => SetProperty(ref _isEnable, value);
        }

        /// <summary>
        /// Select in tree view
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// Expanded in tree view
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// Nest depends on expression this is the "AND" of high level IF condition.
        /// </summary>
        public string NestDependsOnExpression;

        /// <summary>
        /// Depends on expression. This is the AND expression of nest depends on and depends on attribute.
        /// </summary>
        public Expression DependsOnExpr;

        /// <summary>
        /// result of depends on. calculate each time
        /// </summary>
        public TristateValue DependsOnResult;

        /// <summary>
        /// For level control
        /// </summary>
        public HashSet<MenuEntry> DependsOnList;

        /// <summary>
        /// Entires which are controlled by this entry.
        /// </summary>
        public List<HashSet<MenuEntry>> ControlsList;

        /// <summary>
        /// for reverse dependency calculate
        /// </summary>
        public int DependsOnLevel { get; set; }

        /// <summary>
        /// entries which select current entry.
        /// </summary>
        public HashSet<MenuEntry> BeSelectedList;

        /// <summary>
        /// entries which imply current entry.
        /// </summary>
        public HashSet<MenuEntry> BeImpliedList;

        private readonly Dictionary<string, List<string>> _errors;

        private bool _isEnable;
        private bool _isVisable;
        private string _value;
        private string _prompt;
        private string _default;
        private bool _isSelected;
        private bool _isExpanded;
        private bool _isFiltered;


        #region check value in range

        protected static readonly Regex ValidBoolValueRegex =
            new Regex(@"^[n|y]$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        protected static readonly Regex ValidTristateValueRegex =
            new Regex(@"^[n|y|m]$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        protected static readonly Regex ValidIntValueRegex =
            new Regex(@"^\-?\d+$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        protected static readonly Regex ValidHexValueRegex =
            new Regex(@"^0x[0-9a-fA-F]+$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        private bool CheckValueValid(string str, out string error)
        {
            error = null;
            if (EntryType == MenuEntryType.Choice)
            {
                if (IsHaveOptionalOption && str == null)
                    return true;
                if (ChildEntries.Exists(entry => entry.Name == str))
                    return true;
                error = "Choice value is not a valid child entry";
                return false;
            }

            if (EntryType != MenuEntryType.Config && EntryType != MenuEntryType.MenuConfig)
                return true;
            switch (ValueType)
            {
                case MenuAttributeType.String:
                    return true;
                case MenuAttributeType.Bool:
                    if (ValidBoolValueRegex.IsMatch(str))
                        return true;
                    error = "Bool value should be \"n\" or \"y\".";
                    return false;
                case MenuAttributeType.Tristate:
                    if (ValidTristateValueRegex.IsMatch(str))
                        return true;
                    error = "Tristate value should be \"n\", \"m\" or \"y\".";
                    return false;
                case MenuAttributeType.Int:
                    if (ValidIntValueRegex.IsMatch(str))
                        return CheckInRange(str, false, out error);
                    error = "Int value format invalid.";
                    return false;
                case MenuAttributeType.Hex:
                    if (ValidHexValueRegex.IsMatch(str))
                        return CheckInRange(str, true, out error);
                    error = "Hex value format invalid.";
                    return false;
                default:
                    error = $"unsupported type {ValueType}.";
                    return false;
            }
        }

        private bool CheckInRange(string str, bool isHex, out string error)
        {

            error = null;
            var rangeAttr = FindFirstAvailable(MenuAttributeType.Range);
            if (rangeAttr == null)
                return true;
            var rangeStrs = rangeAttr.SymbolValue.Split(',');

            var style = isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.AllowLeadingSign;
            var value = long.Parse(str, style);
            var min = long.Parse(rangeStrs[0], style);
            var max = long.Parse(rangeStrs[1], style);
            if (min <= value && value <= max)
                return true;
            error = $"Value out of range. range is {min} to {max}.";
            return false;
        }

        #endregion //check value in range

        /// <summary>
        /// find first available attribute of given type.
        /// </summary>
        /// <param name="type">attribute type</param>
        /// <returns>null when do not find</returns>
        private MenuAttribute FindFirstAvailable(MenuAttributeType type)
        {
            return Attributes.FirstOrDefault(attribute =>
                attribute.AttributeType == type
                && attribute.ConditionResult == TristateValue.Y);
        }

        /// <summary>
        /// A choice accepts another option "optional", which allows to set the
        /// choice to 'n' and no entry needs to be selected.
        /// </summary>
        public bool IsHaveOptionalOption =>
             Attributes.Exists(attr => attr.AttributeType == MenuAttributeType.Optional);

        /// <summary>
        /// First available help string
        /// </summary>
        public string Help => FindFirstAvailable(MenuAttributeType.Help)?.SymbolValue;

        /// <summary>
        /// Load default and check value valid.
        /// null for bool and tristate which is same as "n".
        /// "0" or range min for int or hex.
        /// null for choice with Optional option
        /// first child entry name for choice without Optional option
        /// </summary>
        private void LoadDefaultValue(bool isProcessValueChange)
        {
            var defaultValue = CalculateDefaultValue();
            if (isProcessValueChange)
                Value = defaultValue;
            else
                _value = defaultValue;
        }

        private string CalculateDefaultValue()
        {
            var value = Default;
            if (ValueType == MenuAttributeType.Int || ValueType == MenuAttributeType.Hex)
            {
                if (value == null)
                {
                    var rangeAttr = FindFirstAvailable(MenuAttributeType.Range);
                    if (rangeAttr == null)
                    {
                        return "0";
                    }
                    // rangeStrs[0] is min, rangeStrs[1] is max
                    value = rangeAttr.SymbolValue.Split(',')[0];
                }
                return value;
            }
            // Set first entry as default
            // when choice do not has "optional" and "Default" option.
            if (EntryType != MenuEntryType.Choice)
                return value;
            // do not modify value when value is null and has optional option.
            if (value == null)
            {
                if (IsHaveOptionalOption)
                    return null;
            }
            else
            {
                // check if value is a valid child entry name
                if (ChildEntries.Exists(entry => entry.Name == value))
                    return value;
                if (IsHaveOptionalOption)
                    return null;
            }
            return ChildEntries.First()?.Name;
        }

        private static TristateValue CalculateValueMin(IEnumerable<MenuEntry> list)
        {
            var result = TristateValue.N;

            foreach (var entry in list)
            {
                if (!entry.IsEnable)
                    continue;
                if (!Enum.TryParse<TristateValue>(entry.Value,
                    true, out var compareVal))
                    continue;
                if (compareVal > result)
                    result = compareVal;
            }

            return result;
        }

        private void PrecessReverseDependency(MenuEntry sourceEntry)
        {
            if (!Enum.TryParse<TristateValue>(Value, true, out var val))
                return;
            if (BeSelectedList.Count == 0 && BeImpliedList.Count == 0)
                return;
            var min = CalculateValueMin(BeSelectedList);
            if (sourceEntry != this)
            {
                var min2 = CalculateValueMin(BeImpliedList);
                if (min2 > min)
                    min = min2;
            }

            if (val < min)
                val = min;
            if ((ValueType == MenuAttributeType.Bool)
                && val == TristateValue.M)
                val = TristateValue.Y;
            _value = val.ToString().ToLower();
            if (sourceEntry != this)
                OnPropertyChanged(nameof(Value));
        }

        /// <summary>
        /// Calculate the value. 
        /// </summary>
        /// <param name="sourceEntry">Reverse dependency entries</param>
        /// <param name="isCalculateControls">Whether calculate child entries. 
        /// This should not be used with <see cref="isLoadDefault"/></param>
        /// <param name="isLoadDefault">Whether load default value.
        /// This should not be used with <see cref="isCalculateControls"/></param>
        public void Calculate(MenuEntry sourceEntry,
            bool isCalculateControls = true, bool isLoadDefault = false)
        {
            // calculate depends on and all attribute.
            DependsOnResult = DependsOnExpr?.Calculate() ?? TristateValue.Y;
            foreach (var attribute in Attributes)
                attribute.Calculate();
            IsEnable = DependsOnResult != TristateValue.N;

            // calculate all notify property
            switch (EntryType)
            {
                case MenuEntryType.Menu:
                case MenuEntryType.MainMenu:
                    Prompt = Name;
                    if (!IsEnable)
                    {
                        IsVisible = false;
                        break;
                    }

                    var attr = FindFirstAvailable(MenuAttributeType.VisibleIf);
                    IsVisible = attr == null || attr.ConditionResult != TristateValue.N;
                    break;
                case MenuEntryType.Comment:
                    Prompt = Name;
                    IsVisible = IsEnable;
                    break;
                case MenuEntryType.Config:
                case MenuEntryType.MenuConfig:
                case MenuEntryType.Choice:
                    Prompt = FindFirstAvailable(MenuAttributeType.Prompt)?.SymbolValue;
                    Default = FindFirstAvailable(MenuAttributeType.Default)?.SymbolValue;


                    // For those hidden entries which prompt is null. 
                    // Since user could not modify them, they should be calculated automatically
                    // by auto load default value.
                    if (isLoadDefault)
                        LoadDefaultValue(false);
                    else if (Prompt == null)
                        LoadDefaultValue(true);

                    if (!IsEnable)
                    {
                        IsVisible = false;
                        break;
                    }

                    IsVisible = !string.IsNullOrEmpty(Prompt);
                    if (ParentEntry.EntryType == MenuEntryType.Choice)
                        CalculateConfigInChoice();
                    PrecessReverseDependency(sourceEntry);
                    break;
                default:
                    Console.WriteLine(
                        $"Could not calculate entry, entry type is {EntryType}. {Location}",
                        Brushes.Red);
                    break;
            }

            // calculate all controlled entries
            if (!isCalculateControls || ControlsList == null)
                return;


            var exceptions = new ConcurrentQueue<Exception>();
            foreach (var entries in ControlsList)
                Parallel.ForEach(entries,
                    entry =>
                    {
                        try
                        {
                            entry.Calculate(sourceEntry, true, isLoadDefault);
                        }
                        catch (Exception e)
                        {
                            exceptions.Enqueue(e);
                        }
                    });
            if (exceptions.Count > 0)
                throw new AggregateException(exceptions);
        }

        private void CalculateConfigInChoice()
        {
            //_value = Default;
            if ((EntryType != MenuEntryType.Config)
                || (ParentEntry.EntryType != MenuEntryType.Choice))
                return;

            string result;
            if (ParentEntry.Value == Name)
                result = "y";
            else if (ParentEntry.ValueType != MenuAttributeType.Tristate)
                result = "n";
            else
                result = _value == "n" ? "n" : "m";

            if (result == _value)
                return;
            _value = result;
            OnPropertyChanged(nameof(Value));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        protected virtual void OnPropertyChanged(
            [CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, 
                new PropertyChangedEventArgs(propertyName));

        protected void SetProperty<T>(ref T storage, T value,
            [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return;
            storage = value;
            OnPropertyChanged(propertyName);
        }

        public bool HasErrors => _errors.Count > 0;

        private void SetErrors(string propertyName, List<string> propertyErrors)
        {
            _errors.Remove(propertyName);

            _errors.Add(propertyName, propertyErrors);

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private void ClearErrors(string propertyName)
        {
            _errors.Remove(propertyName);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return _errors.Values;

            return _errors.ContainsKey(propertyName) ? _errors[propertyName] : null;
        }
    }
}
