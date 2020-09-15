using System;
using System.Reflection;
using PRISM;

namespace IMSDriftTimeAligner
{
    /// <summary>
    /// Frame alignment options
    /// </summary>
    public class FrameAlignmentOptions
    {
        // Ignore Spelling: Sakoe

        /// <summary>
        /// Program date
        /// </summary>
        public const string PROGRAM_DATE = "September 14, 2020";

        /// <summary>
        /// Default frame selection mode
        /// </summary>
        public const BaseFrameSelectionModes DEFAULT_FRAME_SELECTION_MODE = BaseFrameSelectionModes.SumMidNFrames;

        /// <summary>
        /// Default number of frames to sum
        /// </summary>
        public const int DEFAULT_FRAME_SUM_COUNT = 15;

        /// <summary>
        /// Default maximum scans to shift data when aligning
        /// </summary>
        public const int DEFAULT_MAX_SHIFT_SCANS = 150;

        /// <summary>
        /// Default number of data points for smoothing
        /// </summary>
        public const int DEFAULT_SMOOTH_COUNT = 7;

#pragma warning disable 1591

        #region "Enums"

        /// <summary>
        /// Base frame selection modes
        /// </summary>
        public enum BaseFrameSelectionModes
        {
            FirstFrame = 0,
            MidpointFrame = 1,
            MaxTICFrame = 2,
            UserSpecifiedFrameRange = 3,
            SumFirstNFrames = 4,
            SumMidNFrames = 5,
            SumFirstNPercent = 6,
            SumMidNPercent = 7,
            SumAll = 8
        }

        /// <summary>
        /// Alignment methods
        /// </summary>
        public enum AlignmentMethods
        {
            LinearRegression = 0,
            DynamicTimeWarping = 1
        }

        #endregion

        #region "Properties"

        [Option("Align", DoNotListEnumValues = false, HelpShowsDefault = false, HelpText =
            "Method for aligning the data for each frame to the base frame; can be LinearRegression (default) or DynamicTimeWarping")]
        public AlignmentMethods AlignmentMethod { get; set; }

        [Option("BaseFrameMode", "BaseFrame", HelpText =
            "Method for selecting the base frame to align all the other frames to")]
        public BaseFrameSelectionModes BaseFrameSelectionMode { get; set; }

        [Option("BaseCount", HelpText =
            "Number of frames to use, or percentage of frames to use, when the BaseFrameSelection mode is NFrames or NPercent. " +
            "When specifying a percentage, must be a value between 1 and 100")]
        public int BaseFrameSumCount { get; set; }

        [Option("BaseStart", HelpShowsDefault = false, HelpText =
            "First frame to use when the BaseFrameSelection mode is 3 (UserSpecifiedFrameRange); ignored if -BaseFrameList is defined")]
        public int BaseFrameStart { get; set; }

        [Option("BaseEnd", HelpShowsDefault = false, HelpText =
            "Last frame to use when the BaseFrameSelection mode is 3 (UserSpecifiedFrameRange); ignored if -BaseFrameList is defined")]
        public int BaseFrameEnd { get; set; }

        [Option("BaseFrameList", "BaseFrames", HelpShowsDefault = false, HelpText =
            "List of frames to use when the BaseFrameMode is 3 (UserSpecifiedFrameRange)")]
        public string BaseFrameList { get; set; }

        [Option("FrameStart", "Start", HelpShowsDefault = false, HelpText =
            "Frame to start processing at (0 to start at the first frame)")]
        public int FrameStart { get; set; }

        [Option("FrameEnd", "End", HelpShowsDefault = false, HelpText =
            "Frame to stop processing at (Set FrameStart and FrameEnd to 0 to process all frames)")]
        public int FrameEnd { get; set; }

        [Option("InputFile", "InputFilePath", "i", "input", ArgPosition = 1, HelpShowsDefault = false, IsInputFilePath = true,
            HelpText = "Input file path (UIMF File);\nsupports wildcards, e.g. *.uimf")]
        public string InputFilePath { get; set; }

        [Option("OutputFile", "OutputFilePath", "o", "output", ArgPosition = 2, HelpShowsDefault = false, HelpText =
            "Output file path; ignored if the input file path has a wildcard or if /S was used (or a parameter file has Recurse=True)")]
        public string OutputFilePath { get; set; }

        [Option("Recurse", "S", HelpShowsDefault = false, HelpText =
            "Process files in the current directory and all subdirectories")]
        public bool RecurseDirectories { get; set; }

        [Option("DTWPoints", "DTWMaxPoints", HelpShowsDefault = true, Min = 100, Max = 10000, HelpText =
            "Maximum number of points to use for Dynamic Time Warping")]
        public int DTWMaxPoints { get; set; }

        [Option("DTWShift", "DTWMaxShift", "DTWMaxShiftPercent", HelpShowsDefault = true, Min = 0.01, Max = 100, HelpText =
            "Maximum Sakoe Chiba Shift, as a percentage of the number of points used for Dynamic Time Warping")]
        public double DTWSakoeChibaMaxShiftPercent { get; set; }

        [Option("MaxShift", HelpShowsDefault = true, HelpText =
            "Maximum number of scans that data in a frame is allowed to be shifted when aligning to the base frame data; " +
            "ignored if the alignment method is DynamicTimeWarping")]
        public int MaxShiftScans { get; set; }

        private double? mMinimumIntensityThresholdFraction;

        public double MinimumIntensityThresholdFraction
        {
            get
            {
                if (mMinimumIntensityThresholdFraction.HasValue)
                {
                    return mMinimumIntensityThresholdFraction.Value;
                }

                return AlignmentMethod == AlignmentMethods.LinearRegression ? 0.1 : 0.025;
            }
            set => mMinimumIntensityThresholdFraction = value;
        }

        [Option("ITF", "MinimumIntensityThresholdFraction", HelpShowsDefault = false, HelpText =
            "Value to multiply the maximum TIC value by to determine an intensity threshold, " +
            "below which intensity values will be set to 0.  Defaults to 0.1 (aka 10% of the max) for -Align:0 and 0.025 for -Align:1")]
        // ReSharper disable once UnusedMember.Global
        public double UserDefinedMinimumIntensityThresholdFraction
        {
            get => MinimumIntensityThresholdFraction;
            set => MinimumIntensityThresholdFraction = value;
        }

        [Option("ScanMin", "MinScan", "DriftScanFilterMin", HelpShowsDefault = false, HelpText =
                        "Optional minimum drift scan number to filter data by when obtaining data to align")]
        public int DriftScanFilterMin { get; set; }

        [Option("ScanMax", "MaxScan", "DriftScanFilterMax", HelpShowsDefault = false, HelpText =
            "Optional maximum drift scan number to filter data by when obtaining data to align")]
        public int DriftScanFilterMax { get; set; }

        [Option("MzMin", "MinMZ", "MzFilterMin", HelpShowsDefault = false, HelpText =
            "Optional minimum m/z to filter data by when obtaining data to align")]
        public int MzFilterMin { get; set; }

        [Option("MzMax", "MaxMZ", "MzFilterMax", HelpShowsDefault = false, HelpText =
            "Optional maximum m/z to filter data by when obtaining data to align")]
        public int MzFilterMax { get; set; }

        [Option("Smooth", "ScanSmoothCount", HelpText =
            "Number of points to use when smoothing TICs before aligning. " +
            "If 0 or 1; no smoothing is applied.")]
        public int ScanSmoothCount { get; set; }

        [Option("Merge", "MergeFrames", HelpText =
            "When true, the output file will have a single, merged frame. " +
            "When false, the output file will have all of the original frames, with their IMS drift times aligned")]
        public bool MergeFrames { get; set; }

        [Option("Append", "AppendMergedFrame", HelpText =
            "When true, a merged frame of data will be appended to the output file as a new frame " +
            "(ignored if option MergeFrames is true)")]
        public bool AppendMergedFrame { get; set; }

        [Option("Plot", "Vis", HelpShowsDefault = false, HelpText =
            "Visualize the dynamic time warping results for each aligned frame; shows interactive plots in a new window")]
        public bool VisualizeDTW { get; set; }

        [Option("SavePlot", "SavePlots", HelpShowsDefault = false, HelpText =
            "Save a dynamic time warping plot for each aligned frame")]
        public bool SaveDTWPlots { get; set; }

        [Option("SavePlotData", HelpShowsDefault = false, HelpText =
            "Create tab-delimited text files of the dynamic time warping plot data")]
        public bool SavePlotData { get; set; }

        [Option("Debug", HelpShowsDefault = false, HelpText =
            "True to show additional debug messages at the console")]
        public bool DebugMode { get; set; }

        [Option("Preview", HelpShowsDefault = false, HelpText =
            "Preview the file (or files) that would be processed")]
        public bool PreviewMode { get; set; }

        [Option("WriteOptions", "WO", HelpShowsDefault = true, HelpText =
            "Include the processing options at the start of the alignment stats file (Dataset_stats.txt)")]
        public bool WriteOptionsToStatsFile { get; set; }

        #endregion

#pragma warning restore 1591

        /// <summary>
        /// Constructor
        /// </summary>
        public FrameAlignmentOptions()
        {
            InputFilePath = string.Empty;
            OutputFilePath = string.Empty;

            AlignmentMethod = AlignmentMethods.LinearRegression;

            BaseFrameSelectionMode = DEFAULT_FRAME_SELECTION_MODE;

            BaseFrameSumCount = DEFAULT_FRAME_SUM_COUNT;
            BaseFrameStart = 0;
            BaseFrameEnd = 0;
            BaseFrameList = string.Empty;

            FrameStart = 0;
            FrameEnd = 0;

            DTWMaxPoints = 7500;

            DTWSakoeChibaMaxShiftPercent = 5;

            MaxShiftScans = DEFAULT_MAX_SHIFT_SCANS;

            mMinimumIntensityThresholdFraction = null;

            DriftScanFilterMin = 0;
            DriftScanFilterMax = 0;

            MzFilterMin = 0;
            MzFilterMax = 0;

            ScanSmoothCount = DEFAULT_SMOOTH_COUNT;

            MergeFrames = false;

            AppendMergedFrame = false;

            VisualizeDTW = false;

            WriteOptionsToStatsFile = true;
        }

        /// <summary>
        /// Return Enabled if value is true
        /// Return Disabled if value is false
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string BoolToEnabledDisabled(bool value)
        {
            return value ? "Enabled" : "Disabled";
        }

        /// <summary>
        /// Get the program version
        /// </summary>
        /// <returns></returns>
        public static string GetAppVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";

            return version;
        }

        /// <summary>
        /// Show the options at the console
        /// </summary>
        public void OutputSetOptions()
        {
            Console.WriteLine("Options:");


            if (PathHasWildcard(InputFilePath))
            {
                Console.WriteLine(" {0,-40} {1}", "Finding files that match:", InputFilePath);
                Console.WriteLine(" {0,-40} {1}", "Find files in subdirectories:", BoolToEnabledDisabled(RecurseDirectories));
            }
            else
            {
                Console.WriteLine(" {0,-15} {1}", "Reading data from:", InputFilePath);

                if (RecurseDirectories)
                {
                    Console.WriteLine(" {0,-40} {1}", "Also search subdirectories:", BoolToEnabledDisabled(RecurseDirectories));
                }
                else if (!string.IsNullOrWhiteSpace(OutputFilePath))
                {
                    Console.WriteLine(" {0,-15} {1}", "Creating file:", OutputFilePath);
                }
            }

            Console.WriteLine();
            Console.WriteLine(" {0,-40} {1}", "Alignment Method:", AlignmentMethod);
            if (AlignmentMethod == AlignmentMethods.DynamicTimeWarping)
            {
                Console.WriteLine(" {0,-40} {1}", "Visualize the DTW path:", BoolToEnabledDisabled(VisualizeDTW));
                Console.WriteLine(" {0,-40} {1}", "Save DTW plot for each frame:", BoolToEnabledDisabled(SaveDTWPlots));
                Console.WriteLine(" {0,-40} {1}", "Save DTW plot data text files:", BoolToEnabledDisabled(SavePlotData));

                Console.WriteLine(" {0,-40} {1}", "Max points for DTW:", DTWMaxPoints);

                Console.WriteLine(" {0,-40} {1}%", "Sakoe Chiba Max Shift:", StringUtilities.ValueToString(DTWSakoeChibaMaxShiftPercent, 3));
            }

            Console.WriteLine(" {0,-40} {1} (mode {2})", "Base Frame Selection Mode:", BaseFrameSelectionMode, (int)BaseFrameSelectionMode);

            switch (BaseFrameSelectionMode)
            {
                case BaseFrameSelectionModes.FirstFrame:
                case BaseFrameSelectionModes.MidpointFrame:
                case BaseFrameSelectionModes.MaxTICFrame:
                    // Only a single frame is chosen; do not show BaseFrameSumCount, BaseFrameStart, or BaseFrameEnd
                    break;

                case BaseFrameSelectionModes.UserSpecifiedFrameRange:

                    if (string.IsNullOrWhiteSpace(BaseFrameList))
                    {
                        Console.WriteLine(" {0,-40} {1}", "Base Frame Start:", BaseFrameStart);
                        if (BaseFrameEnd > 0)
                        {
                            Console.WriteLine(" {0,-40} {1}", "Base Frame End:", BaseFrameEnd);
                        }
                        else
                        {
                            Console.WriteLine(" {0,-40} {1}", "Base Frame End:", "Last frame in file");
                        }
                    }
                    else
                    {
                        Console.WriteLine(" {0,-40} {1}", "Base Frame List:", BaseFrameList);
                    }
                    break;

                case BaseFrameSelectionModes.SumFirstNFrames:
                case BaseFrameSelectionModes.SumMidNFrames:
                    Console.WriteLine(" {0,-40} {1}", "Base Frames to Sum:", BaseFrameSumCount);
                    break;

                case BaseFrameSelectionModes.SumFirstNPercent:
                case BaseFrameSelectionModes.SumMidNPercent:
                    Console.WriteLine(" {0,-40} {1}%", "Base Frames to Sum:", BaseFrameSumCount);
                    break;

            }

            Console.WriteLine();
            if (FrameStart > 0 || FrameEnd > 0)
            {
                Console.WriteLine(" {0,-40} {1}", "First frame to process:", FrameStart);
                if (FrameEnd > 0)
                {
                    Console.WriteLine(" {0,-40} {1}", "Last frame to process:", FrameEnd);
                }
                else
                {
                    Console.WriteLine(" {0,-40} {1}", "Last frame to process:", "Last frame in file");
                }
            }

            Console.WriteLine(" {0,-40} {1} scans", "Maximum shift:", MaxShiftScans);
            Console.WriteLine(" {0,-40} {1}", "Minimum Intensity threshold Fraction:", MinimumIntensityThresholdFraction);

            if (DriftScanFilterMin > 0 || DriftScanFilterMax > 0 || MzFilterMin > 0 || MzFilterMax > 0)
            {
                Console.WriteLine();
                Console.WriteLine(" Data selection restrictions for generating the drift time TIC");

                if (DriftScanFilterMin > 0 || DriftScanFilterMax > 0)
                {
                    Console.WriteLine(" {0,-40} {1}", "Minimum drift time scan:", DriftScanFilterMin);
                    if (DriftScanFilterMax > 0)
                    {
                        Console.WriteLine(" {0,-40} {1}", " Maximum drift time scan:", DriftScanFilterMax);
                    }
                    else
                    {
                        Console.WriteLine(" {0,-40} {1}", "Maximum drift time scan:", "Last scan in frame");
                    }
                }

                if (MzFilterMin > 0 || MzFilterMax > 0)
                {
                    Console.WriteLine(" {0,-40} {1}", "Minimum m/z:", MzFilterMin);
                    if (MzFilterMax > 0)
                    {
                        Console.WriteLine(" {0,-40} {1}", "Maximum m/z:", MzFilterMax);
                    }
                    else
                    {
                        Console.WriteLine(" {0,-40} {1}", "Maximum m/z:", "Highest observed m/z (no filter)");
                    }
                }

            }

            Console.WriteLine(" {0,-40} {1}", "Scans to smooth:", ScanSmoothCount);

            Console.WriteLine();
            if (MergeFrames)
            {
                Console.WriteLine(" The output file will have a single smoothed frame");
            }
            else
            {
                if (AppendMergedFrame)
                {
                    Console.WriteLine(" The output file will have aligned frames, plus a merged frame appended to the end");
                }
                else
                {
                    Console.WriteLine(" The output file will have aligned frames");
                }
            }

            Console.WriteLine();
            if (DebugMode)
                Console.WriteLine(" Showing debug messages");

            if (PreviewMode)
                Console.WriteLine(" Previewing files that would be processed");

            Console.WriteLine();

        }

        /// <summary>
        /// True if the path has a * or ?
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool PathHasWildcard(string filePath)
        {
            return filePath.Contains("*") || filePath.Contains("?");
        }

        /// <summary>
        /// Copy the options with a member-wise Clone
        /// </summary>
        /// <returns></returns>
        public FrameAlignmentOptions ShallowCopy()
        {
            // ReSharper disable once ArrangeThisQualifier
            return (FrameAlignmentOptions)this.MemberwiseClone();
        }

        /// <summary>
        /// Validate the options
        /// </summary>
        /// <returns></returns>
        public bool ValidateArgs(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(InputFilePath))
            {
                errorMessage = "You must specify an input file";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
