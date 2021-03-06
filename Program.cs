﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using PRISM;
using PRISM.Logging;

namespace IMSDriftTimeAligner
{
    /// <summary>
    /// This program processes IMS data in a UIMF file to align all frames to a base frame,
    /// adjusting the observed drift times of each frame to align with the base frame
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)</para>
    /// <para>
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://panomics.pnnl.gov/ or https://omics.pnl.gov
    /// </para>
    /// </remarks>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Ignore Spelling: conf

            var asmName = typeof(Program).GetTypeInfo().Assembly.GetName();
            var exeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);       // Alternatively: System.AppDomain.CurrentDomain.FriendlyName
            var version = FrameAlignmentOptions.GetAppVersion();

            var parser = new CommandLineParser<FrameAlignmentOptions>(asmName.Name, version)
            {
                ProgramInfo = "This program processes IMS data in a UIMF file to align all frames " +
                              "to a base frame, adjusting the observed drift times of each frame " +
                              "to align with the base frame. The input file can alternatively be " +
                              "a tab-delimited text file with two columns of data to align. " +
                              "Intensity values in the first column will be aligned to intensity values in the second column.",

                ContactInfo = "Program written by Matthew Monroe for PNNL (Richland, WA) in 2017" +
                              Environment.NewLine +
                              "E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov" + Environment.NewLine +
                              "Website: https://panomics.pnnl.gov/ or https://omics.pnl.gov",

                UsageExamples = {
                    exeName + " DataFile.UIMF",
                    exeName + " NamePrefix*.UIMF",
                    exeName + " DataFile.UIMF -Start:2",
                    exeName + " DataFile.UIMF -Start:2 -BaseFrame:5 -BaseCount:7"
                },
                ParamKeysFieldWidth = 25,
                ParamDescriptionFieldWidth = 70
            };

            parser.AddParamFileKey("Conf");

            var parseResults = parser.ParseArgs(args);
            var options = parseResults.ParsedResults;

            try
            {
                if (!parseResults.Success)
                {
                    // Error messages should have already been shown to the user
                    Thread.Sleep(1500);
                    return -1;
                }

                if (!options.ValidateArgs(out var errorMessage))
                {
                    parser.PrintHelp();

                    Console.WriteLine();
                    ConsoleMsgUtils.ShowWarning("Validation error:");
                    ConsoleMsgUtils.ShowWarning(errorMessage);

                    Thread.Sleep(1500);
                    return -1;
                }

                options.OutputSetOptions();
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.Write($"Error running {exeName}");
                Console.WriteLine(e.Message);
                Console.WriteLine($"See help with {exeName} --help");
                return -1;
            }

            try
            {
                var commandLine = exeName + " " + string.Join(" ", args);
                var processor = new DriftTimeAlignmentEngine(options, commandLine);
                RegisterEvents(processor);

                int returnCode;

                if (options.PathHasWildcard(options.InputFilePath) || options.RecurseDirectories)
                {
                    returnCode = ProcessFilesWildcard(processor, options);
                }
                else
                {
                    var success = processor.ProcessFile(options.InputFilePath, options.OutputFilePath);
                    returnCode = success ? 0 : -1;
                }

                if (returnCode == 0)
                {
                    return 0;
                }

                Thread.Sleep(1500);
                return returnCode;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error occurred in Program->Main", ex);
                Thread.Sleep(1500);
                return -1;
            }
        }

        /// <summary>
        /// Process files matching the given file path spec, specified by options.InputFilePath
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="options"></param>
        /// <returns>0 if success, error code if an error</returns>
        /// <remarks>This method ignores options.OutputFilePath</remarks>
        private static int ProcessFilesWildcard(DriftTimeAlignmentEngine processor, FrameAlignmentOptions options)
        {
            var filesToProcess = PathUtils.FindFilesWildcard(options.InputFilePath, options.RecurseDirectories);

            var successCount = 0;
            var failureCount = 0;

            var suffixesToSkip = new List<string>
                    {
                        DriftTimeAlignmentEngine.OUTPUT_FILENAME_SUFFIX,
                        DriftTimeAlignmentEngine.BACKUP_FILENAME_SUFFIX
                    };

            foreach (var fileToProcess in filesToProcess)
            {
                var baseName = Path.GetFileNameWithoutExtension(fileToProcess.Name);
                var skipFile = false;
                foreach (var suffix in suffixesToSkip)
                {
                    if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine();
                        Console.WriteLine("Skipping file with suffix {0}: {1}",
                                          suffix,
                                          PathUtils.CompactPathString(fileToProcess.FullName, 70));
                        skipFile = true;
                        break;
                    }
                }

                if (skipFile)
                    continue;

                if (!options.PreviewMode)
                {
                    ConsoleMsgUtils.ShowDebug("Processing " + PathUtils.CompactPathString(fileToProcess.FullName, 70));
                    Console.WriteLine();
                }

                var successOneFile = processor.ProcessFile(fileToProcess.FullName, string.Empty);

                if (successOneFile)
                {
                    successCount++;
                    continue;
                }

                failureCount++;
                ConsoleMsgUtils.ShowWarning("Error processing " + fileToProcess.Name);
            }

            if (successCount == 0 && failureCount == 0)
            {
                ConsoleMsgUtils.ShowWarning("No files were found with file spec " + options.InputFilePath);
                return -2;
            }

            if (failureCount == 0)
            {
                return 0;
            }

            return -3;
        }

        #region "Event handlers"

        /// <summary>Use this method to chain events between classes</summary>
        /// <param name="sourceClass"></param>
        private static void RegisterEvents(IEventNotifier sourceClass)
        {
            // Ignore: sourceClass.DebugEvent += OnDebugEvent;
            sourceClass.StatusEvent += OnStatusEvent;
            sourceClass.ErrorEvent += OnErrorEvent;
            sourceClass.WarningEvent += OnWarningEvent;
            // Ignore: sourceClass.ProgressUpdate += OnProgressUpdate;
        }

        private static void OnErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void OnStatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void OnWarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        #endregion
    }
}
