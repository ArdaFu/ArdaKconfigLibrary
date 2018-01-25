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
//  Description: Kconfig element attribution
//  Author     : Fu Pengfei
//------------------------------------------------------------------------------
//  Change Logs:
//  Date         Notes
//  2015-09-15   first implementation
//------------------------------------------------------------------------------
//  $Id:: MenuAttribute.cs 1679 2018-01-25 04:00:30Z fupengfei                 $
//------------------------------------------------------------------------------
using System.Text;

namespace Arda.ArmDevTool.Kconfig
{


    /// <summary>
    /// Menu entry attribute
    /// </summary>
    public class MenuAttribute
    {
        /// <summary>
        /// Attribute type.
        /// <para>Can be "ValueType\Prompt\Default\DependsOn\Select\Imply\VisibleIf(menu only)\Range\Help\Option\Optional(choice only)"</para>
        /// <para>if set to <c>ValueType</c>, then <c>ExpressionType</c> should be Bool/Tristate/String/Int/Hex. That is the entry value type of the config/menuconfig</para>
        /// <para>if set to <c>Option</c>, then <c>ExpressionType</c> should be DefConfigList/Modules/Env/AllNoConfigY/Optional. That is the option type</para>
        /// <para>if set to <c>Choice</c>, then <c>ExpressionType</c> should be Bool/Tristate. That is the entry value type of the choice</para>
        /// </summary>
        public MenuAttributeType AttributeType { get; set; }

        /// <summary>
        /// Expression type
        /// <para>if AttributeType = ValueType, this should be Bool/Tristate/String/Int/Hex. That is the entry value type of the config/menuconfig</para>
        /// <para>if AttributeType = Option, this should be DefConfigList/Modules/Env/AllNoConfigY/Optional. That is the option type</para>
        /// <para>if AttributeType = Choice, this should be Bool/Tristate. That is the entry value type of the choice</para>
        /// </summary>
        public MenuAttributeType ExpressionType { get; set; }

        /// <summary>
        /// Symbol or value expression.
        /// <para>For AttributeType = Range, the format is "&lt;min&gt;,&lt;max&gt;"</para>
        /// <para>For AttributeType = Option, ExpressionType = Env, this is the environment variable name which should be read from register"</para>
        /// <para>For AttributeType = Default, this is the value expression</para>
        /// </summary>
        public string SymbolValue { get; set; }

        /// <summary>
        /// Condition expression string. Used to generate <see cref="ConditionExpr"/>
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Condition expression. for calculate <see cref="ConditionResult"/>.
        /// </summary>
        public Expression ConditionExpr;

        /// <summary>
        /// The result of <see cref="ConditionExpr"/>
        /// </summary>
        public TristateValue ConditionResult;

        /// <summary>
        /// This is the reverse dependency entry
        /// <para>reverse dependencies: "select" &lt;symbol&gt; ["if" &lt;expr&gt;]</para>
        /// <para>weak reverse dependencies: "imply" &lt;symbol&gt; ["if" &lt;expr&gt;]</para>
        /// </summary>
        public MenuEntry ReverseDependency;

        public void Calculate() =>
            ConditionResult = ConditionExpr?.Calculate() ?? TristateValue.Y;

        /// <summary>
        /// Format is "[&lt;AttributeType&gt;][&lt;ExpressionType&gt;] = &lt;Expression&gt; if &lt;Condition&gt;
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var sb = new StringBuilder($"[{AttributeType}]");
            if (ExpressionType != MenuAttributeType.Invalid)
                sb.Append($" [{ExpressionType}]");
            if (!string.IsNullOrEmpty(SymbolValue))
                sb.Append($" = {SymbolValue}");
            if (string.IsNullOrEmpty(Condition))
                return sb.ToString();
            var ifStr = AttributeType == MenuAttributeType.DependsOn 
                ? " " 
                : " if ";
            sb.Append($"{ifStr}{Condition}");
            return sb.ToString();
        }
    }
}
