using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PHP.Core;
using PHP.Core.Reflection;
using umbraco;
using umbraco.cms.businesslogic.macro;
using umbraco.interfaces;
using umbraco.IO;

namespace Our.Umbraco
{
	public class PhpMacroEngine : IMacroEngine
	{
		public string Execute(MacroModel macro, INode currentPage)
		{
			var fileLocation = string.Empty;

			if (!string.IsNullOrEmpty(macro.ScriptName))
			{
				fileLocation = macro.ScriptName;
			}
			else if (!string.IsNullOrEmpty(macro.ScriptCode))
			{
				var code = macro.ScriptCode.Trim();
				var md5 = library.md5(code);
				var filename = string.Concat("inline-", md5, ".php");

				fileLocation = this.CreateTemporaryFile(code, filename, true);
			}

			if (string.IsNullOrEmpty(fileLocation))
			{
				return string.Empty;
			}

			var builder = new StringBuilder();

			using (var writer = new StringWriter(builder))
			{
				var contents = File.ReadAllText(IOHelper.MapPath(fileLocation));

				var context = ScriptContext.CurrentContext;
				context.Output = writer;

				Operators.SetVariable(context, null, "model", PhpSafeType(currentPage));

				PhpEval(context, Parse(contents));
			}

			return builder.ToString();
		}

		public string Name
		{
			get
			{
				return "PHP Macro Engine";
			}
		}

		public string TempDirectory
		{
			get
			{
				return "~/App_Data/TEMP/Php/";
			}
		}

		public IEnumerable<string> SupportedExtensions
		{
			get
			{
				return new string[] { "php" };
			}
		}

		public Dictionary<string, IMacroGuiRendering> SupportedProperties
		{
			get { throw new NotImplementedException(); }
		}

		public IEnumerable<string> SupportedUIExtensions
		{
			get
			{
				return new string[] { "php" };
			}
		}

		public bool Validate(string code, string tempFileName, INode currentPage, out string errorMessage)
		{
			errorMessage = string.Empty;
			return true; // throw new NotImplementedException();
		}

		private string CreateTemporaryFile(string code, string filename, bool skipExists)
		{
			var relativePath = string.Concat(this.TempDirectory, filename);
			var physicalPath = IOHelper.MapPath(relativePath);
			var physicalDirectoryPath = IOHelper.MapPath(this.TempDirectory);

			if (skipExists && File.Exists(physicalPath))
			{
				return relativePath;
			}

			if (File.Exists(physicalPath))
			{
				File.Delete(physicalPath);
			}

			if (!Directory.Exists(physicalDirectoryPath))
			{
				Directory.CreateDirectory(physicalDirectoryPath);
			}

			using (var file = new StreamWriter(physicalPath, false, Encoding.UTF8))
			{
				file.Write(code);
			}

			return relativePath;
		}



		/// <remarks>
		/// The following code has been taken from PHP View Engine.
		/// http://phpviewengine.codeplex.com/license
		/// </remarks>
		private object PhpSafeType(object o)
		{
			// PHP can handle bool, int, double, and long
			if ((o is int) || (o is double) || (o is long) || (o is bool))
			{
				return o;
			}
			// Upcast other integer types so PHP can use them
			// TODO: What to do about System.UInt64 and byte?
			else if (o is short)
			{
				return (int)(short)o;
			}
			else if (o is ushort)
			{
				return (int)(ushort)o;
			}
			else if (o is uint)
			{
				return (long)(uint)o;
			}
			else if (o is ulong)
			{
				ulong u = (ulong)o;
				if (u <= Int64.MaxValue)
				{
					return System.Convert.ToInt64(u);
				}
				else
				{
					return u.ToString();
				}
			}
			// Convert System.Single to a string
			// to reduce rounding errors
			// TODO: Figure out why I need to do this
			else if (o is float)
			{
				return Double.Parse(o.ToString());
			}
			// Really not sure what the best thing is to do with 'System.Decimal'
			// TODO: Review this decision
			else if (o is decimal)
			{
				return o.ToString();
			}
			// Strings and byte arrays require special handling
			else if (o is string)
			{
				return new PhpString((string)o);
			}
			else if (o is byte[])
			{
				return new PhpBytes((byte[])o);
			}
			// Convert .NET collections into PHP arrays
			else if (o is ICollection)
			{
				var ca = new PhpArray();
				if (o is IDictionary)
				{
					var dict = o as IDictionary;
					foreach (var key in dict.Keys)
					{
						var val = PhpSafeType(dict[key]);
						ca.SetArrayItem(PhpSafeType(key), val);
					}
				}
				else
				{
					foreach (var item in (ICollection)o)
					{
						ca.Add(PhpSafeType(item));
					}
				}
				return ca;
			}

			// PHP types are obviously ok and can just move along
			if (o is DObject)
			{
				return o;
			}

			// Wrap all remaining CLR types so that PHP can handle tham
			return ClrObject.WrapRealObject(o);
		}

		private object PhpEval(ScriptContext context, string code)
		{
			return DynamicCode.Eval(
				code,
				false, // phalanger internal stuff
				context,
				null, // local variables
				null, // reference to "$this"
				null, // current class context
				"default", // file name, used for debug and cache key
				1, 1, // position in the file used for debug and cache key
				-1, // something internal
				null // current namespace, used in CLR mode
			);
		}

		// Brute force parser
		// TODO: Allow <? and ?> to be escaped in HTML
		// TODO: Ignore <? and ?> when in single or double quoted strings in code
		// TODO: Give this some Pratt parser love
		private string Parse(string input)
		{
			var sb = new StringBuilder();
			var codeBuilder = new StringBuilder();
			int p = 0;
			char c;
			bool incode = false;
			int line = 1;
			int startline = 0;
			while (p < input.Length)
			{
				c = input[p];
				switch (c)
				{
					case '\n':
						line++;
						goto default;
					case '"':
						if (incode)
							goto default;
						sb.Append("\\\""); // We need to escape out double quotes for use in 'echo'
						p++;
						break;
					case '<':
						p++;
						if (p < input.Length)
						{
							if ((input[p] == '?'))
							{
								if (incode)
									throw new Exception("Nested PHP code block at line " + line);
								incode = true;
								startline = line;
								p++; // Skip the ?

								// check for opening <?php
								if ((input[p] == 'p'))
									p += 3; // Skip the 'php' opening

								// Convert HTML string into an echo statement
								codeBuilder.AppendFormat("echo \"{0}\";", sb.ToString());
								sb = new StringBuilder();
							}
							else
							{
								sb.Append('<');
							}
						}
						else
						{
							sb.Append('<');
						}
						break;
					case '?':
						if (incode)
						{
							p++;
							if (p < input.Length)
							{
								if (input[p] == '>')
								{
									codeBuilder.AppendLine(sb.ToString());
									incode = false;
									sb = new StringBuilder();
									p++; // Skip the >
								}
							}
							else
							{
								sb.Append('?');
							}
						}
						else
						{
							sb.Append('?');
							p++;
						}
						break;
					default:
						sb.Append(c);
						p++;
						break;
				}
			}

			// Ensure that all PHP code blocks were properly closed
			if (incode)
				throw new Exception("Unclosed PHP code block at line " + startline);

			// Flush any remaining HTML
			codeBuilder.AppendFormat("echo \"{0}\";", sb.ToString());

			return codeBuilder.ToString();
		}
	}
}