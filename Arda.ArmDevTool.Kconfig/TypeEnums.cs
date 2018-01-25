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
//  Description: Kconfig element type and value enum
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: TypeEnums.cs 1679 2018-01-25 04:00:30Z fupengfei                     $
//------------------------------------------------------------------------------
namespace Arda.ArmDevTool.Kconfig
{
    /// <summary>
    /// Menu entry type
    /// </summary>
    public enum MenuEntryType
    {
        /// <summary>
        /// <code>config:
        ///  "config" &lt;symbol&gt;
        ///  &lt;config options&gt;</code>
        /// </summary>
        Config,

        /// <summary>
        /// menuconfig:
        /// "menuconfig" &lt;symbol&gt;
        /// &lt;config options&gt;
        /// </summary>
        MenuConfig,

        /// <summary>
        /// Choice/EndChoice pair.
        /// </summary>
        Choice,

        /// <summary>
        /// Comment line to write to .config file.
        /// </summary>
        Comment,

        /// <summary>
        /// Menu/EndMenu pair.
        /// </summary>
        Menu,

        /// <summary>
        /// If/EndIf pair.
        /// </summary>
        If,

        /// <summary>
        /// Source file
        /// </summary>
        Source,

        /// <summary>
        /// Menu/EndMenu pair.
        /// </summary>
        EndMenu,

        /// <summary>
        /// Choice/EndChoice pair.
        /// </summary>
        EndChoice,

        /// <summary>
        /// If/EndIf pair.
        /// </summary>
        EndIf,

        /// <summary>
        /// kconfig entry point
        /// </summary>
        MainMenu
    }

    /// <summary>
    /// Menu attributes type.
    /// </summary>
    public enum MenuAttributeType
    {
        /// <summary>
        /// Invalid type, only use for compare.
        /// </summary>
        Invalid,

        /// <summary>
        /// ValueType "bool"
        /// </summary>
        Bool,

        /// <summary>
        /// ValueType "tristate"
        /// </summary>
        Tristate,

        /// <summary>
        /// ValueType "string"
        /// </summary>
        String,

        /// <summary>
        /// ValueType "hex"
        /// </summary>
        Hex,

        /// <summary>
        /// ValueType "int"
        /// </summary>
        Int,

        /// <summary>
        /// "prompt" &lt;prompt&gt; ["if" &lt;expr&gt;]
        /// </summary>
        Prompt,

        /// <summary>
        /// "default" &lt;expr&gt; ["if" &lt;expr&gt;]
        /// </summary>
        Default,

        /// <summary>
        /// "def_bool" &lt;expr&gt; ["if" &lt;expr&gt;]
        /// </summary>
        DefBool,

        /// <summary>
        /// "def_tristate" &lt;expr&gt; ["if" &lt;expr&gt;]
        /// </summary>
        DefTristate,

        /// <summary>
        /// "depends on" &lt;expr&gt;
        /// </summary>
        DependsOn,

        /// <summary>
        /// "select" &lt;symbol&gt; ["if" &lt;expr&gt;]
        /// </summary>
        Select,

        /// <summary>
        /// "imply" &lt;symbol&gt; ["if" &lt;expr&gt;]
        /// </summary>
        Imply,

        /// <summary>
        /// "visible if" &lt;expr&gt; (use for menu only!)
        /// </summary>
        VisibleIf,

        /// <summary>
        /// "range" &lt;symbol&gt; &lt;symbol&gt; ["if" &lt;expr&gt;]
        /// </summary>
        Range,

        /// <summary>
        /// "help" or "---help---"
        /// </summary>
        Help,

        /// <summary>
        /// "option" &lt;symbol&gt;[=&lt;value&gt;]
        /// <para>Set AttributeType = Option, ExpressionType = DefConfigList/Modules/Env/AllNoConfigY/Optional</para>
        /// </summary>
        Option,

        /// <summary>
        /// Storage entry value type
        /// <para>Set AttributeType = ValueType, ExpressionType = Bool/Tristate/String/Int/Hex</para>
        /// </summary>
        ValueType,

        /// <summary>
        /// OptionType "defconfig_list"
        /// <para>e.g. Set AttributeType = Option,  ExpressionType = DefConfigList</para>
        /// </summary>
        DefConfigList,

        /// <summary>
        /// OptionType "modules"
        /// <para>e.g. Set AttributeType = Option,  ExpressionType = Modules</para>
        /// </summary>
        Modules,

        /// <summary>
        /// OptionType "env"=&lt;value&gt;
        /// <para>e.g. Set AttributeType = Option,  ExpressionType = Env, Expression = &lt;value&gt;</para>
        /// </summary>
        Env,

        /// <summary>
        /// OptionType "allnoconfig_y"
        /// <para>e.g. Set AttributeType = Option,  ExpressionType = AllNoConfigY</para>
        /// </summary>
        AllNoConfigY,

        /// <summary>
        /// choice's special option. (use for choice only!)
        /// <para>Allows to set the choice to 'n' and no entry needs to be selected.</para>
        /// </summary>
        Optional,
    }

    /// <summary>
    /// Expression type
    /// </summary>
    public enum ExpressionType
    {
        /// <summary>
        /// Expression = [right]
        /// </summary>
        None,

        /// <summary>
        /// Expression = "N" (constant)
        /// </summary>
        N,

        /// <summary>
        /// Expression = "M" (constant)
        /// </summary>
        M,

        /// <summary>
        /// Expression = "Y" (constant)
        /// </summary>
        Y,

        /// <summary>
        /// Expression = ([left] == [right])?
        /// </summary>
        Equal,

        /// <summary>
        /// Expression = ([left] != [right])?
        /// </summary>
        NoEuqal,

        /// <summary>
        /// Expression = ([left] == [right])?
        /// </summary>
        Not,

        /// <summary>
        /// Expression = ([left] && [right])?
        /// </summary>
        And,

        /// <summary>
        /// Expression = ([left] || [right])?
        /// </summary>
        Or,
    }

    /// <summary>
    /// Tristate and bool values
    /// </summary>
    public enum TristateValue
    {
        /// <summary>
        /// no
        /// </summary>
        N = 0,

        /// <summary>
        /// module
        /// </summary>
        M = 1,

        /// <summary>
        /// yes
        /// </summary>
        Y = 2,
    }
}