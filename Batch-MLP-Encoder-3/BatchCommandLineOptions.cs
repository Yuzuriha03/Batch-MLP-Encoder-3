using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SadPencil.BatchMLPEncoder3 {
    internal sealed class BatchCommandLineOptions {
        public string SurcodePath { get; private set; }
        public string Eac3toPath { get; private set; }
        public string TempDirectory { get; private set; }
        public string OutputDirectory { get; private set; }
        public int SampleRate { get; private set; } = 48000;
        public int Bits { get; private set; } = 24;
        public IReadOnlyList<string> InputFiles { get; private set; }

        public static bool TryParse(string[] args, out BatchCommandLineOptions options, out string error) {
            options = null;
            error = string.Empty;
            if (args == null || args.Length == 0 || !String.Equals(args[0], "--batch", StringComparison.OrdinalIgnoreCase)) {
                error = "Batch mode must start with --batch.";
                return false;
            }

            BatchCommandLineOptions parsed = new BatchCommandLineOptions();
            List<string> files = new List<string>();
            bool inputs = false;
            for (int index = 1; index < args.Length; ++index) {
                string argument = args[index];
                if (inputs) {
                    files.Add(argument);
                    continue;
                }
                if (argument == "--") {
                    inputs = true;
                    continue;
                }
                if (index + 1 >= args.Length) {
                    error = "Missing value after " + argument + ".";
                    return false;
                }
                string value = args[++index];
                switch (argument.ToLowerInvariant()) {
                    case "--surcode": parsed.SurcodePath = NormalizePath(value); break;
                    case "--eac3to": parsed.Eac3toPath = NormalizePath(value); break;
                    case "--temp": parsed.TempDirectory = NormalizePath(value); break;
                    case "--output": parsed.OutputDirectory = NormalizePath(value); break;
                    case "--sample-rate":
                        if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sampleRate)) {
                            error = "Invalid sample rate: " + value;
                            return false;
                        }
                        parsed.SampleRate = sampleRate;
                        break;
                    case "--bits":
                        if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bits)) {
                            error = "Invalid bit depth: " + value;
                            return false;
                        }
                        parsed.Bits = bits;
                        break;
                    default:
                        error = "Unknown batch option: " + argument;
                        return false;
                }
            }

            if (files.Count == 0) {
                error = "No input files were supplied after --.";
                return false;
            }
            if (String.IsNullOrWhiteSpace(parsed.TempDirectory) || String.IsNullOrWhiteSpace(parsed.OutputDirectory)) {
                error = "--temp and --output are required.";
                return false;
            }
            if (String.IsNullOrWhiteSpace(parsed.SurcodePath) || !File.Exists(parsed.SurcodePath)) {
                error = "--surcode must point to an existing surcodemlp.exe: " + parsed.SurcodePath;
                return false;
            }
            if (String.IsNullOrWhiteSpace(parsed.Eac3toPath) || !File.Exists(parsed.Eac3toPath)) {
                error = "--eac3to must point to an existing eac3to.exe: " + parsed.Eac3toPath;
                return false;
            }
            if (!IsAllowedSampleRate(parsed.SampleRate)) {
                error = "Unsupported sample rate: " + parsed.SampleRate.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            if (parsed.Bits != 16 && parsed.Bits != 20 && parsed.Bits != 24) {
                error = "Unsupported bit depth: " + parsed.Bits.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            foreach (string file in files) {
                string normalizedFile = NormalizePath(file);
                if (!File.Exists(normalizedFile)) {
                    error = "Input file does not exist: " + normalizedFile;
                    return false;
                }
            }

            Directory.CreateDirectory(parsed.TempDirectory);
            Directory.CreateDirectory(parsed.OutputDirectory);
            parsed.InputFiles = files.ConvertAll(NormalizePath);
            options = parsed;
            return true;
        }

        private static string NormalizePath(string value) {
            return Path.GetFullPath(value.Replace('/', '\\'));
        }

        private static bool IsAllowedSampleRate(int value) {
            return value == 44100 || value == 48000 || value == 88200 || value == 96000 ||
                value == 176400 || value == 192000;
        }
    }
}
