using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FileProcessor;

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
        public const string PROGRAM_DATE = "January 6, 2017";

        /// <summary>
        /// UIMF File to process
        /// </summary>
        private static string mInputFilePath;

        private static string mOutputFilePath;

        private static FrameAlignmentOptions mAlignmentOptions;

        private static bool mShowDebugMessages;

        static int Main(string[] args)
        {
            var objParseCommandLine = new clsParseCommandLine();

            mInputFilePath = string.Empty;
            mOutputFilePath = string.Empty;

            mAlignmentOptions = new FrameAlignmentOptions();
            mShowDebugMessages = false;

            try
            {

                var success = false;

                if (objParseCommandLine.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(objParseCommandLine))
                        success = true;
                }

                if (!success ||
                    objParseCommandLine.NeedToShowHelp ||
                    string.IsNullOrWhiteSpace(mInputFilePath))
                {
                    ShowProgramHelp();
                    return -1;
                }

                var processor = new DriftTimeAlignmentEngine
                {
                    Options = mAlignmentOptions,
                    ShowDebugMessages = mShowDebugMessages
                };

                processor.ErrorEvent += Processor_ErrorEvent;
                processor.MessageEvent += Processor_MessageEvent;
                processor.WarningEvent += Processor_WarningEvent;

                success = processor.ProcessFile(mInputFilePath, mOutputFilePath);

                if (!success)
                {
                    Thread.Sleep(1500);
                    return -1;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred in Program->Main: " + Environment.NewLine + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Thread.Sleep(1500);
                return -1;
            }

            return 0;

        }

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        private static bool RetrieveIntegerOption(clsParseCommandLine objParseCommandLine, string paramName, out int paramValue, out bool isError)
        {
            string paramText;
            paramValue = 0;
            isError = false;

            if (!objParseCommandLine.RetrieveValueForParameter(paramName, out paramText))
                return false;

            if (int.TryParse(paramText, out paramValue))
                return true;

            ShowErrorMessage($"/{paramName} must specify an integer; {paramText} is not numeric");
            isError = true;
            return false;
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine objParseCommandLine)
        {
            // Returns True if no problems; otherwise, returns false
            var lstValidParameters = new List<string> {
                "I", "O", "Merge", "Append",
                "BaseFrame", "BaseCount", "BaseStart", "BaseEnd",
                "Start", "End", "Smooth",
                "MaxShift", "Debug"};

            try
            {
                // Make sure no invalid parameters are present
                if (objParseCommandLine.InvalidParametersPresent(lstValidParameters))
                {
                    var badArguments = new List<string>();
                    foreach (var item in objParseCommandLine.InvalidParameters(lstValidParameters))
                    {
                        badArguments.Add("/" + item);
                    }

                    ShowErrorMessage("Invalid commmand line parameters", badArguments);

                    return false;
                }

                // Query objParseCommandLine to see if various parameters are present						
                if (objParseCommandLine.NonSwitchParameterCount > 0)
                {
                    mInputFilePath = objParseCommandLine.RetrieveNonSwitchParameter(0);
                }


                string paramValue;
                int paramInteger;
                bool isError;

                if (objParseCommandLine.RetrieveValueForParameter("I", out paramValue))
                {
                    mInputFilePath = string.Copy(paramValue);
                }

                if (objParseCommandLine.RetrieveValueForParameter("O", out paramValue))
                {
                    mOutputFilePath = string.Copy(paramValue);
                }


                if (objParseCommandLine.IsParameterPresent("Merge"))
                    mAlignmentOptions.MergeFrames = true;

                if (objParseCommandLine.IsParameterPresent("Append"))
                    mAlignmentOptions.AppendMergedFrame = true;

                if (RetrieveIntegerOption(objParseCommandLine, "BaseFrame", out paramInteger, out isError))
                {
                    mAlignmentOptions.BaseFrameSelectionMode = (FrameAlignmentOptions.BaseFrameSelectionModes)paramInteger;
                }
                else if (isError)
                    return false;

                if (RetrieveIntegerOption(objParseCommandLine, "BaseCount", out paramInteger, out isError))
                {
                    mAlignmentOptions.BaseFrameSumCount = paramInteger;
                }
                else if (isError)
                    return false;

                // Note that BaseStart and BaseEnd are only used when BaseFrame is UserSpecifiedFrameRange (3)
                if (RetrieveIntegerOption(objParseCommandLine, "BaseStart", out paramInteger, out isError))
                {
                    mAlignmentOptions.BaseFrameStart = paramInteger;
                }
                else if (isError)
                    return false;

                if (RetrieveIntegerOption(objParseCommandLine, "BaseEnd", out paramInteger, out isError))
                {
                    mAlignmentOptions.BaseFrameStart = paramInteger;
                }
                else if (isError)
                    return false;

                if (RetrieveIntegerOption(objParseCommandLine, "Start", out paramInteger, out isError))
                {
                    mAlignmentOptions.FrameStart = paramInteger;
                }
                else if (isError)
                    return false;

                if (RetrieveIntegerOption(objParseCommandLine, "End", out paramInteger, out isError))
                {
                    mAlignmentOptions.FrameEnd = paramInteger;
                }
                else if (isError)
                    return false;

                if (RetrieveIntegerOption(objParseCommandLine, "MaxShift", out paramInteger, out isError))
                {
                    mAlignmentOptions.MaxShiftScans = paramInteger;
                }
                else if (isError)
                    return false;

                if (RetrieveIntegerOption(objParseCommandLine, "Smooth", out paramInteger, out isError))
                {
                    mAlignmentOptions.ScanSmoothCount = paramInteger;
                }
                else if (isError)
                    return false;

                
                if (objParseCommandLine.IsParameterPresent("Debug"))
                {
                    mShowDebugMessages = true;
                }


                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters: " + Environment.NewLine + ex.Message);
            }

            return false;
        }

        private static void ShowErrorMessage(string message)
        {
            const string separator = "------------------------------------------------------------------------------";

            Console.WriteLine();
            Console.WriteLine(separator);
            Console.WriteLine(message);
            Console.WriteLine(separator);
            Console.WriteLine();

            WriteToErrorStream(message);
        }

        private static void ShowErrorMessage(string title, IEnumerable<string> items)
        {
            const string separator = "------------------------------------------------------------------------------";

            Console.WriteLine();
            Console.WriteLine(separator);
            Console.WriteLine(title);
            var message = title + ":";

            foreach (var item in items)
            {
                Console.WriteLine("   " + item);
                message += " " + item;
            }
            Console.WriteLine(separator);
            Console.WriteLine();

            WriteToErrorStream(message);
        }


        private static void ShowProgramHelp()
        {
            var exeName = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            try
            {
                Console.WriteLine();
                Console.WriteLine("This program processes IMS data in a UIMF file to align all frames to a base frame, " +
                                  "adjusting the observed drift times of each frame to align with the base frame.");
                Console.WriteLine();
                Console.WriteLine("Program syntax:" + Environment.NewLine + exeName);
                Console.WriteLine(" InputFilePath [/O:OutputFilePath] [/Merge] [/Append] ");
                Console.WriteLine(" [/BaseFrame:N] [/BaseCount:N] [/BaseStart:N] [/BaseEnd:N]");
                Console.WriteLine(" [/Start:N] [/End:N] [/MaxShift:N] [/Smooth:N] [/Debug]");
                Console.WriteLine();
                Console.WriteLine("InputFilePath is a path to the UIMF file to process");
                Console.WriteLine();
                Console.WriteLine("Use /O to specify the output file path");
                Console.WriteLine("By default the output file will be named InputFileName_new.uimf");
                Console.WriteLine("");
                Console.WriteLine("Use /Merge to specify that all of the aligned frames should be merged into a single frame by co-adding the data");
                Console.WriteLine("Use /Append to specify that the data should be merged and appended as a new frame to the output file");
                Console.WriteLine();
                Console.WriteLine("Use /BaseFrame to specify how the base frame is selected; options are:");

                foreach (var frameMode in Enum.GetValues(typeof(FrameAlignmentOptions.BaseFrameSelectionModes)))
                {
                    Console.WriteLine("  /BaseFrame:{0} for {1}", (int)frameMode, frameMode);
                }
                Console.WriteLine();
                Console.WriteLine("Use /BaseCount to specify the number or frames or percent range to use when FrameMode is NFrame or NPercent based;");
                Console.WriteLine("  default is /BaseCount:" + mAlignmentOptions.BaseFrameSumCount);
                Console.WriteLine("Use /BaseStart and /BaseEnd to specify the range of frames to use as the base when using /BaseFrame:3 (aka UserSpecifiedFrameRange)");
                Console.WriteLine("Use /FrameStart and /FrameEnd to limit the range of frames to align");
                Console.WriteLine();
                Console.WriteLine("Use /MaxShift to specify the maximum allowed shift (in scans) that scans in a frame can be adjusted;");
                Console.WriteLine("  default is /MaxShift:" + mAlignmentOptions.MaxShiftScans);
                Console.WriteLine("Use /Smooth to specify the number of data points (scans) in the TIC to use for moving average smoothing " +
                                  "prior to aligning each frame to the base frame;");
                Console.WriteLine("  default is /Smooth:" + mAlignmentOptions.ScanSmoothCount);
                Console.WriteLine();
                Console.WriteLine("Use /Debug to show additional debug messages at the console");
                Console.WriteLine();
                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2016");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com");
                Console.WriteLine("Website: http://panomics.pnnl.gov/ or http://omics.pnl.gov or http://www.sysbio.org/resources/staff/");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                Thread.Sleep(750);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error displaying the program syntax: " + ex.Message);
            }

        }

        private static void WriteToErrorStream(string strErrorMessage)
        {
            try
            {
                using (var swErrorStream = new System.IO.StreamWriter(Console.OpenStandardError()))
                {
                    swErrorStream.WriteLine(strErrorMessage);
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
                // Ignore errors here
            }
        }

        static void ShowErrorMessage(string message, bool pauseAfterError)
        {
            Console.WriteLine();
            Console.WriteLine("===============================================");

            Console.WriteLine(message);

            if (pauseAfterError)
            {
                Console.WriteLine("===============================================");
                Thread.Sleep(1500);
            }
        }


        #region "Event handlers"

        private static void Processor_ErrorEvent(object sender, MessageEventArgs e)
        {
            ShowErrorMessage(e.Message);
        }

        private static void Processor_MessageEvent(object sender, MessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        private static void Processor_WarningEvent(object sender, MessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        #endregion
    }
}
