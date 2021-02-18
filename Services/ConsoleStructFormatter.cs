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
			var ident = " ".Repeat(identSize);
			var isGroupingType = source.GetType().IsDefined<UseMemberGroupingAttribute>();
			var fieldInfos = source.GetType().GetPublicInstanceFields().ToArray();
			var showTypeNames = options.ShowTypeNames;

			StringBuilder sb = new(fieldInfos.Length * 10);

			if (isGroupingType)
			{
				ident = string.Empty;
				sb.AppendLine(options.GroupingMemberTemplate.InsertArgs(source.GetType().GetDisplayName()));
			}

			foreach (var fieldInfo in fieldInfos)
			{
				var fieldType = fieldInfo.FieldType;
				var value = fieldInfo.GetValue(source)!;
				var fieldName = fieldInfo.GetDisplayName();
				var showType = showTypeNames == ShowTypeNamesOption.AllMembers
				               || (showTypeNames == ShowTypeNamesOption.GroupingMembers) 
				               && (isGroupingType 
				                   || fieldInfo.IsDefined<UseMemberGroupingAttribute>());

				//System.Diagnostics.Debug.Assert(fieldInfo.Name != "CharmsAndRareItems");

				if (options.TypeNameTemplate is not null && !fieldType.IsArray && showType)
					fieldName = options.TypeNameTemplate.InsertArgs(fieldType.GetDisplayName(), fieldName);

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
							sb.Append(memberDelimiter + $"{ident} {options.ArrayElementPrefix} ({elementSize}) {fieldName}[{i}] {complexValue}");
							++i;
						}
					}
					else
					{
						size = elementSize * length;
						sb.Append(memberDelimiter + $"{ident} {memberPrefix} ({size}) {fieldName}: {elementType.GetDisplayName()}[] ");
						sb.Append(elementType.IsPrimitive
							? ((Array)value).Format().TruncateAt(options.ArrayStringMaxLength)!.Replace("…", options.Ellipsis)
							: "{N/A}");

					}
				}
				else if (Type.GetTypeCode(fieldType) == TypeCode.Object)
				{
					sb.Append(memberDelimiter);
					
					var hasToStringOverride = fieldType.HasOverride(nameof(ToString));
					var memberFieldCount = fieldType.GetPublicInstanceFields().Count();

					if (hasToStringOverride)
						sb.Append($"{ident} {memberPrefix} ({size}) {fieldName}: {value}");
					else
					{
						var complexValue = InternalFormat(value, identSize, options);
						sb.Append($"{ident} {options.ComplexMemberPrefix} ({size}) {fieldName} {complexValue}");
					}
				}
				else if (fieldType.IsEnum)
				{
					var underLyingType = fieldType.GetEnumUnderlyingType();
					size = Marshal.SizeOf(underLyingType);
					var underlyingValue = Convert.ChangeType(value, underLyingType).ToString();

					var enumFlags = fieldType.IsDefined<FlagsAttribute>()
						? ((Enum) value).GetSetFlags().Join()
						: value.ToString();

					if (enumFlags != underlyingValue)
						enumFlags = $"{{{enumFlags}}}";
					else
						enumFlags = string.Empty;

					sb.Append(memberDelimiter + $"{ident} {memberPrefix} ({size}) {fieldName}:{options.EnumTypeSuffix.InsertArgs(fieldType.Name)} {underlyingValue} {enumFlags}");
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
