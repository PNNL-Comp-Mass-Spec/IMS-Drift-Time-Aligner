using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using FileProcessor;
using PRISM;
using clsParseCommandLine = FileProcessor.clsParseCommandLine;

namespace IMSDriftTimeAligner
{
    /// <summary>
    /// This program processes IMS data in a UIMF file to align all frames to a base frame,
    /// adjusting the observed drift times of each frame to align with the base frame
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    ///
    /// E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
    /// Website: http://panomics.pnnl.gov/ or http://omics.pnl.gov or http://www.sysbio.org/resources/staff/
    /// </remarks>
    internal static class Program
    {
        static int Main(string[] args)
        {
            var asmName = typeof(Program).GetTypeInfo().Assembly.GetName();
            var exeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);       // Alternatively: System.AppDomain.CurrentDomain.FriendlyName
            var version = FrameAlignmentOptions.GetAppVersion();

            var parser = new CommandLineParser<FrameAlignmentOptions>(asmName.Name, version)
            {
                ProgramInfo = "This program processes IMS data in a UIMF file to align all frames " +
                              "to a base frame, adjusting the observed drift times of each frame " +
                              "to align with the base frame. \n\n Options for BaseFrame are:\n" +
                              " 0 for FirstFrame \n" +
                              " 1 for MidpointFrame \n" +
                              " 2 for MaxTICFrame \n" +
                              " 3 for UserSpecifiedFrameRange \n" +
                              " 4 for SumFirstNFrames \n" +
                              " 5 for SumMidNFrames \n" +
                              " 6 for SumFirstNPercent \n" +
                              " 7 for SumMidNPercent \n" +
                              " 8 for SumAll",

                ContactInfo = "Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2017" +
                              Environment.NewLine + Environment.NewLine +
                              "E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov" + Environment.NewLine +
                              "Website: http://panomics.pnnl.gov/ or http://omics.pnl.gov or http://www.sysbio.org/resources/staff/",

                UsageExamples = {
                    exeName + " DataFile.UIMF",
                    exeName + " DataFile.UIM /Start:2",
                    exeName + " DataFile.UIM /Start:2 /BaseFrame:5 /BaseCount:7"
                }
            };

            var parseResults = parser.ParseArgs(args);
            var options = parseResults.ParsedResults;

            try
            {
                if (!parseResults.Success)
                {
                    Thread.Sleep(1500);
                    return -1;
                }

                if (!options.ValidateArgs())
                {
                    parser.PrintHelp();
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
                var processor = new DriftTimeAlignmentEngine(options);

                processor.ErrorEvent += Processor_ErrorEvent;
                processor.MessageEvent += Processor_MessageEvent;
                processor.WarningEvent += Processor_WarningEvent;

                var success = processor.ProcessFile(options.InputFilePath, options.OutputFilePath);

                if (!success)
                {
                    Thread.Sleep(1500);
                    return -1;
                }

            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error occurred in Program->Main", ex);
                Thread.Sleep(1500);
                return -1;
            }

            return 0;

        }

        #region "Event handlers"

        private static void Processor_ErrorEvent(object sender, MessageEventArgs e)
        {
            ConsoleMsgUtils.ShowError(e.Message);
        }

        private static void Processor_MessageEvent(object sender, MessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        private static void Processor_WarningEvent(object sender, MessageEventArgs e)
        {
            ConsoleMsgUtils.ShowWarning(e.Message);
        }

        #endregion
    }
}
