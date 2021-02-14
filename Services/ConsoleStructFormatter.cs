using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Common.Shared.Min.Extensions;
using IO.Extensions;
using IO.Helpers;
using IO.Services;

namespace IO.Modules.Services
{
	/// <inheritdoc/>
	public class ConsoleStructFormatter: IStructFormatter
	{
		private static readonly StructFormattingOptions DefaultOptions = new();
	
		public string Format<T>(T source, StructFormattingOptions? options = default)
			where T : struct => InternalFormat(source, 0, options);

		private static string InternalFormat(in object source, int currentIdentSize, StructFormattingOptions? options = default)
		{
			options ??= DefaultOptions;

			var identSize = options.IdentSize + currentIdentSize;
			var memberDelimiter = options.MemberDelimiter;
			var memberPrefix = options.SimpleMemberPrefix;
			var complexMemberPrefix = options.ComplexMemberPrefix;
			var ident = " ".Repeat(identSize);
			var isGroupingType = source.GetType().IsDefined<UseMemberGroupingAttribute>();
			var fieldInfos = source.GetType().GetPublicInstanceFields().ToArray();
			var showTypeNames = options.ShowTypeNames;

			StringBuilder sb = new(fieldInfos.Length * 10);

			if (isGroupingType)
			{
				ident = string.Empty;
				sb.AppendLine($"»{source.GetType().GetDisplayName()}«");
			}

			foreach (var fieldInfo in fieldInfos)
			{
				var fieldType = fieldInfo.FieldType;
				var value = fieldInfo.GetValue(source)!;
				var fieldName = fieldInfo.GetDisplayName();
				var useGrouping = isGroupingType || fieldInfo.IsDefined<UseMemberGroupingAttribute>();

				//Debug.Assert(fieldName != "Chunk04");

				if (options.TypeNameTemplate is not null && 
				    (useGrouping && showTypeNames == ShowTypeNamesOption.GroupingMembers 
				     || showTypeNames == ShowTypeNamesOption.NonGroupingMembers) && 
				    !fieldType.IsArray && fieldType.Name != fieldName)
						fieldName = fieldType.Name.InsertArgs(fieldName);

				var size = 0;

				if (!fieldType.IsEnum && !fieldType.IsArray)
				{
					try
					{
						size = Marshal.SizeOf(fieldType);
					}
					catch
					{
						// Ignore
					}
				}
				
				if (fieldType.IsArray)
				{
					var elementType = fieldType.GetElementType()!;
					var length = ((Array)value).Length;
					var elementSize = Marshal.SizeOf(elementType);
					
					if (Type.GetTypeCode(elementType) == TypeCode.Object)
					{
						var i = 0;
						foreach (var element in (Array)value)
						{
							var complexValue = InternalFormat(element, identSize, options);
							sb.Append(memberDelimiter + $"{ident} ¬ ({elementSize}) {fieldName}[{i}] {complexValue}");
							++i;
						}
					}
					else
					{
						size = elementSize * length;
						sb.Append(memberDelimiter + $"{ident} {memberPrefix} ({size}) {fieldName}: ");
						sb.Append(elementType.IsPrimitive
							? $"{elementType.Name}[{length}] {((Array)value).Format().TruncateAt(options.ArrayStringMaxLength)!.Replace("…", options.Ellipsis)}"
							: $"{elementType.Name}[{length}]");

					}
				}
				else if (Type.GetTypeCode(fieldType) == TypeCode.Object)
				{
					sb.Append(memberDelimiter);
					
					var isComplexMember = fieldType.IsDefined<HasComplexMembersAttribute>();
					var hasToStringOverride = fieldType.IsDefined<HasToStringOverrideAttribute>();
					var memberFieldCount = fieldType.GetPublicInstanceFields().Count();

					if (!hasToStringOverride && (isComplexMember || memberFieldCount > 1))
					{
						var complexValue = InternalFormat(value, identSize, options);
						sb.Append($"{ident} {complexMemberPrefix} ({size}) {fieldName} {complexValue}");
					}
					else
						sb.Append($"{ident} {memberPrefix} ({size}) {fieldName}: {value}");
				}
				else if (fieldType.IsEnum)
				{
					fieldType = fieldType.GetEnumUnderlyingType();
					size = Marshal.SizeOf(fieldType);
					var underlyingValue = Convert.ChangeType(value, fieldType);
					var enumFlags = ((Enum)value).GetSetFlags().Join();

					sb.Append(memberDelimiter + $"{ident} {memberPrefix} ({size}) {fieldName} (Enum): {underlyingValue} {{{enumFlags}}}");
				}
				else
					sb.Append(memberDelimiter + $"{ident} {memberPrefix} ({size}) {fieldName}: {value}");

				if (isGroupingType)
					sb.Append(memberDelimiter);
			}

			return sb.ToString();
		}
	}
}
