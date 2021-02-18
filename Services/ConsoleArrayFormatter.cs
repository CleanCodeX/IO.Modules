using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Common.Shared.Min.Extensions;
using IO.Services;

namespace IO.Modules.Services
{
	/// <inheritdoc/>
	public class ConsoleArrayFormatter: IArrayFormatter 
	{
		private static readonly ArrayFormattingOptions DefaultOptions = new();

		public string Format([NotNull] in Array source, ArrayFormattingOptions? options = default) => InternalFormat(source, options);

		private static string InternalFormat(in Array source, ArrayFormattingOptions? options = default)
		{
			source.ThrowIfNull(nameof(source));
			options ??= DefaultOptions;

			StringBuilder sb = new(source.Length);
			var delimiter = options.Delimiter;
			var nullValue = options.NullValue;

			for (var i = 0; i < source.Length; i++)
			{
				if (i > 0 && delimiter is not null)
					sb.Append(delimiter);

				sb = sb.Append(source.GetValue(i)?.ToString() ?? nullValue);
			}

			return $"{options.Prefix}{sb}{options.Suffix}";
		}
	}
}
