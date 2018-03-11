using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mobile.BuildTools.Logging;
using Newtonsoft.Json.Linq;

namespace Mobile.BuildTools.Generators
{
    public class SecretsClassGenerator
    {
        private const string AutoGeneratedMessage =
@"// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by Mobile.BuildTools. For more information or to
//      file an issue please see https://github.com/dansiegel/Mobile.BuildTools
//
//      Changes to this file may cause incorrect behavior and will be lost when 
//      the code is regenerated. 
//
//      NOTE: This file should be excluded from source control.
//  </autogenerated>
// ------------------------------------------------------------------------------

";

        private const string SafePlaceholder = "*****";

        private const string TabSpace = "    ";

        public string ProjectBasePath { get; set; }

        public string SecretsClassName { get; set; }

        public string SecretsJsonFilePath { get; set; }

        public string BaseNamespace { get; set; }

        public string OutputPath { get; set; }

        public string IntermediateOutputPath { get; set; }

        public bool? DebugOutput { get; set; }

        public ILog Log { get; set; }

        public ITaskItem[] GeneratedFiles { get; private set; }

        public void Execute()
        {
            if (DebugOutput == null)
            {
                DebugOutput = false;
            }

            var json = File.ReadAllText(SecretsJsonFilePath);
            var secrets = JObject.Parse(json);

            string replacement = string.Empty;
            string safeReplacement = string.Empty;

            foreach (var secret in secrets)
            {
                replacement += ProcessSecret(secret);
                safeReplacement += ProcessSecret(secret, true);
            }

            replacement = Regex.Replace(replacement, "\n\n$", "");

            var secretsClass = GenerateClass(replacement);
            Log.LogMessage((bool)DebugOutput ? secretsClass : GenerateClass(Regex.Replace(safeReplacement, "\n\n$", "")));

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }

            var outputFile = Path.Combine(OutputPath, $"{SecretsClassName}.cs");
            var intermediateFile = Path.Combine(IntermediateOutputPath, $"{SecretsClassName}.cs");
            Log.LogMessage($"Writing Secrets Class to: '{outputFile}'");
            GeneratedFiles = File.Exists(outputFile) ? new ITaskItem[0] : new ITaskItem[] {
                new TaskItem(ProjectCollection.Escape(outputFile))
            };
            File.WriteAllText(outputFile, secretsClass);
            File.WriteAllText(intermediateFile, secretsClass);
        }

        internal string GenerateClass(string replacement) =>
            $"{AutoGeneratedMessage}namespace {GetNamespace()}\n{{\n{TabSpace}internal static class {SecretsClassName}\n{TabSpace}{{\n{replacement}\n{TabSpace}}}\n}}\n";

        internal string ProcessSecret(KeyValuePair<string, JToken> secret, bool safeOutput = false)
        {
            var value = secret.Value.ToString();
            var outputValue = safeOutput ? SafePlaceholder : value;
            if (bool.TryParse(value, out bool b))
            {
                return $"{TabSpace}{TabSpace}internal const bool {secret.Key} = {outputValue};\n\n";
            }
            else if (double.TryParse(value, out double d))
            {
                if (double.TryParse(secret.Value.ToString(), out double val))
                {
                    var type = Regex.IsMatch(secret.Value.ToString(), @"^\d+$") ? "int" : "double";
                    return $"{TabSpace}{TabSpace}internal const {type} {secret.Key} = {outputValue};\n\n";
                }
                else
                {
                    throw new NotSupportedException($"Unable to parse value for {secret.Key} - {outputValue}");
                }
            }
            else
            {
                return $"{TabSpace}{TabSpace}internal const string {secret.Key} = \"{outputValue}\";\n\n";
            }
        }

        private string GetNamespace()
        {
            Uri file = new Uri(OutputPath);
            // Must end in a slash to indicate folder
            Uri folder = new Uri(ProjectBasePath);
            string relativePath =
            Uri.UnescapeDataString(
                folder.MakeRelativeUri(file)
                    .ToString()
                    .Replace('/', '.')
                );

            return relativePath;
        }
    }
}