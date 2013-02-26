#region License

// Copyright (C) 2012-2013 Kazunori Sakamoto
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Paraiba.Core;

namespace TestManipulator {
	public class ProgramForRemovingAssertions {
		public static void Do(string[] args) {
			var count = args.Length > 0 ? args[0].ToInt() : 50;
			var paths =
					Directory.EnumerateFiles(
							@"src\test", "*.java", SearchOption.AllDirectories).ToList();
			while (true) {
				// Clean up
				var sources = GetCleanedSources(paths);

				// Count assert methods
				var allPathAndIndicies = paths.SelectMany(
						path => sources[path].IndicesOf("assert")
								.Select(i => Tuple.Create(path, i)))
						.Select((t, index) => Tuple.Create(t.Item1, t.Item2, index))
						.ToList();
				var pathAndIndicies = allPathAndIndicies
						.Take((int)(allPathAndIndicies.Count * count / 100.0 + 0.5))
						.ToList();

				// Remove @Test
				foreach (var pathAndIndex in pathAndIndicies) {
					var path = pathAndIndex.Item1;
					var sourceIndex = pathAndIndex.Item2;
					var source = sources[path];
					var testIndex = source.IndexOf("assert", sourceIndex);
					sources[path] = source.Substring(0, testIndex) + "//"
							+ source.Substring(testIndex);
				}

				foreach (var path in paths) {
					File.WriteAllText(path, sources[path]);
				}
				WriteRemovedTestCases(allPathAndIndicies, pathAndIndicies);
				RunJester();
				WriteJesterResult();
			}
		}

		private static Dictionary<string, string> GetCleanedSources(List<string> paths) {
			var sources = paths.Select(
					path => {
						var source = File.ReadAllText(path);
						source = source.Replace("//assert", "assert");
						return Tuple.Create(path, source);
					})
					.ToDictionary(
							pathAndSource => pathAndSource.Item1,
							pathAndSource => pathAndSource.Item2);
			return sources;
		}

		private static void RunJester() {
			var process = Process.Start("experiment.bat");
			process.WaitForExit();
		}

		private static void WriteJesterResult() {
			var dateTime = DateTime.Now;
			var date =
					new[] { dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute }.
							JoinString(", ");
			File.Move("jesterReport.xml", "jesterReport" + date + ".xml");

			var coverageText = File.ReadAllText("coverage.log");
			var xdoc = XDocument.Load("jesterReport" + date + ".xml");
			var ret = ParseJesterResult(xdoc);
			var content = new StringBuilder();
			content.AppendLine(coverageText);
			content.AppendLine(
					"Mutation score: " + ret.Item1 + " / " + ret.Item2 + "( "
							+ ret.Item1 * 100.0 / ret.Item2 + " )");
			File.WriteAllText("result" + date + ".log", content.ToString());
		}

		private static Tuple<int, int> ParseJesterResult(XDocument xdoc) {
			var elements = xdoc.Descendants("JestedFile");
			var all = 0;
			var fail = 0;
			foreach (var element in elements) {
				all += element.Attribute("numberOfChanges").Value.ToInt();
				fail += element.Attribute("numberOfChangesThatDidNotCauseTestsToFail").
						Value.ToInt();
			}
			return Tuple.Create(all - fail, all);
		}

		private static void WriteRemovedTestCases(
				List<Tuple<string, int, int>> allPathAndIndicies,
				List<Tuple<string, int, int>> pathAndIndicies) {
			var content = new StringBuilder();
			var all = allPathAndIndicies.Count;
			var rest = all - pathAndIndicies.Count;
			content.AppendLine(
					"Test cases: " + rest + " / " + all + "(" + (rest * 100.0 / all) + "%)");
			content.AppendLine(
					"removed test cases: "
							+ pathAndIndicies.Select(p => p.Item3).JoinString(", "));
			File.WriteAllText("testcases.log", content.ToString());
		}
	}
}