using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using PRISM;

namespace IMSDriftTimeAligner
{
    /// <summary>
    /// This program processes IMS data in a UIMF file to align all frames to a base frame,
    /// adjusting the observed drift times of each frame to align with the base frame
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    ///
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://panomics.pnnl.gov/ or https://omics.pnl.gov
    /// </remarks>
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            var asmName = typeof(Program).GetTypeInfo().Assembly.GetName();
            var exeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);       // Alternatively: System.AppDomain.CurrentDomain.FriendlyName
            var version = FrameAlignmentOptions.GetAppVersion();

            var parser = new CommandLineParser<FrameAlignmentOptions>(asmName.Name, version)
            {
                ProgramInfo = "This program processes IMS data in a UIMF file to align all frames " +
                              "to a base frame, adjusting the observed drift times of each frame " +
                              "to align with the base frame.",

                ContactInfo = "Program written by Matthew Monroe for the Department of Energy" + Environment.NewLine +
                              "(PNNL, Richland, WA) in 2017" +
                              Environment.NewLine + Environment.NewLine +
                              "E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov" + Environment.NewLine +
                              "Website: https://panomics.pnnl.gov/ or https://omics.pnl.gov",

                UsageExamples = {
                    exeName + " DataFile.UIMF",
                    exeName + " NamePrefix*.UIMF",
                    exeName + " DataFile.UIMF -Start:2",
                    exeName + " DataFile.UIMF -Start:2 -BaseFrame:5 -BaseCount:7"
                }
            };

            var parseResults = parser.ParseArgs(args);
            var options = parseResults.ParsedResults;

            try
            {
                if (!parseResults.Success)
                {
                    var firstMessage = parseResults.ParseErrors.FirstOrDefault().Message;
                    if (firstMessage.StartsWith("Created example parameter file") ||
                        firstMessage.StartsWith("-CreateParamFile provided"))
                    {
                        // This is not an error
                        // The user should have already been shown the default parameter file content (or the name of the parameter file)
                        return 0;
                    }

                    ConsoleMsgUtils.ShowWarning("Error processing the command line arguments");
                    foreach (var item in parseResults.ParseErrors)
                    {
                        ConsoleMsgUtils.ShowWarning(item.Message);
                    }

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

                processor.ErrorEvent += Processor_ErrorEvent;
                processor.StatusEvent += Processor_StatusEvent;
                processor.WarningEvent += Processor_WarningEvent;

                int returnCode;

                if (options.InputFilePath.Contains("*") || options.InputFilePath.Contains("?"))
                {
                    returnCode = ProcessFilesWildcard(processor, options);
                }
                else
                {
                    var success = processor.ProcessFile(options.InputFilePath, options.OutputFilePath);
                    returnCode = success ? 0 : -1;
                }

                if (returnCode == 0)
                    return 0;

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
        /// Process files matching the given file path spec
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="options"></param>
        /// <returns>0 if success, error code if an error</returns>
        private static int ProcessFilesWildcard(DriftTimeAlignmentEngine processor, FrameAlignmentOptions options)
        {

            // FileSpec has a wildcard

            var filesToProcess = PathUtils.FindFilesWildcard(options.InputFilePath);

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

                    if (string.IsNullOrWhiteSpace(options.OutputFilePath) && baseName.ToLower().EndsWith(suffix.ToLower()))
                    {
                        Console.WriteLine();
                        Console.WriteLine("Skipping file with suffix {0}: {1}",
                                          suffix,
                                          PathUtils.CompactPathString(fileToProcess.FullName, 70));
                        Console.WriteLine();
                        skipFile = true;
                        break;
                    }
                }

                if (skipFile)
                    continue;

                ConsoleMsgUtils.ShowDebug("Processing " + PathUtils.CompactPathString(fileToProcess.FullName, 70));
                Console.WriteLine();

                var successOneFile = processor.ProcessFile(fileToProcess.FullName, options.OutputFilePath);

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


        private static void Processor_ErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void Processor_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void Processor_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        #endregion
    }
}
