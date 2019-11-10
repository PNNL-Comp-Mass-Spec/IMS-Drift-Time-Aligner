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
        /// <summary>
        /// Program date
        /// </summary>
        public const string PROGRAM_DATE = "November 7, 2019";

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
            "Method for aligning the data for each frame to the base frame")]
        public AlignmentMethods AlignmentMethod { get; set; }

        [Option("BaseFrame", "BaseFrameMode", HelpText =
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
            "List of frames to use when the BaseFrameSelection mode is 3 (UserSpecifiedFrameRange)")]
        public string BaseFrameList { get; set; }

        [Option("Debug", HelpShowsDefault = false, HelpText =
            "True to show additional debug messages at the console")]
        public bool DebugMode { get; set; }

        [Option("Start", "FrameStart", HelpShowsDefault = false, HelpText =
            "Frame to start processing at (0 to start at the first frame)")]
        public int FrameStart { get; set; }

        [Option("End", "FrameEnd", HelpShowsDefault = false, HelpText =
            "Frame to stop processing at (Set FrameStart and FrameEnd to 0 to process all frames)")]
        public int FrameEnd { get; set; }

        [Option("i", "input", ArgPosition = 1, HelpShowsDefault = false, HelpText =
            "Input file path (UIMF File)")]
        public string InputFilePath { get; set; }

        [Option("o", "output", ArgPosition = 2, HelpShowsDefault = false, HelpText =
            "Output file path")]
        public string OutputFilePath { get; set; }

        [Option("MaxShift", HelpText =
            "Maximum number of scans that data in a frame is allowed to be shifted when aligning to the base frame data")]
        public int MaxShiftScans { get; set; }

        [Option("ITF", "MinimumIntensityThresholdFraction", HelpText =
            "Value to multiply the maximum TIC value by to determine an intensity threshold, " +
            "below which intensity values will be set to 0; only used if the Alignment Method is LinearRegression (-Align:0)")]
        public double MinimumIntensityThresholdFraction { get; set; }

        public bool EnableStretch { get; private set; }

        private double mMaxExpansionPercent;

        [Option("Expand", "Grow", HelpShowsDefault = true, HelpText =
            "Maximum percentage value to expand the data when stretching the scan numbers to match the comparison frame to the base frame; value between 0 and 100")]
        public double MaxExpansionPercent
        {
            get => mMaxExpansionPercent;
            set
            {
                EnableStretch = true;
                mMaxExpansionPercent = value;
            }
        }

        private double mMaxContractionPercent;

        [Option("Contract", "Shrink", HelpShowsDefault = true, HelpText =
            "Maximum percentage value to contract the data when stretching the scan numbers to match the comparison frame to the base frame; value between 0 and 100")]
        public double MaxContractionPercent
        {
            get => mMaxContractionPercent;
            set
            {
                EnableStretch = true;
                mMaxContractionPercent = value;
            }
        }

        [Option("StretchSteps", HelpShowsDefault = true, HelpText =
            "Number of steps to try between the -Contract and -Expand limits")]
        public int ExpandContractSteps { get; set; }

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
            "(ignored if option AppendMergedFrame is true)")]
        public bool AppendMergedFrame { get; set; }

        [Option("Plot", "Vis", HelpShowsDefault = false, HelpText =
            "Visualize the dynamic time warping results")]
        public bool VisualizeDTW { get; set; }

        [Option("WO", "WriteOptions", HelpShowsDefault = true, HelpText =
            "Include the processing options at the start of the alignment stats file (Dataset_stats.txt)")]
        public bool WriteOptionsToStatsFile { get; set; } = true;

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
            BaseFrameList = "";

            FrameStart = 0;
            FrameEnd = 0;

            MaxShiftScans = DEFAULT_MAX_SHIFT_SCANS;

            MaxContractionPercent = 10;
            MaxExpansionPercent = 20;
            ExpandContractSteps = 20;

            MinimumIntensityThresholdFraction = 0.1;

            DriftScanFilterMin = 0;
            DriftScanFilterMax = 0;

            MzFilterMin = 0;
            MzFilterMax = 0;

            ScanSmoothCount = DEFAULT_SMOOTH_COUNT;

            MergeFrames = false;

            AppendMergedFrame = false;

            VisualizeDTW = false;

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

            Console.WriteLine(" Reading data from: {0}", InputFilePath);

            if (!string.IsNullOrWhiteSpace(OutputFilePath))
                Console.WriteLine(" Creating file: {0}", OutputFilePath);

            Console.WriteLine();
            Console.WriteLine(" Alignment Method: {0}", AlignmentMethod);
            Console.WriteLine(" Base Frame Selection Mode: {0} ({1})", BaseFrameSelectionMode, (int)BaseFrameSelectionMode);
            if (AlignmentMethod == AlignmentMethods.DynamicTimeWarping)
            {
                Console.WriteLine(" {0,-40}: {1}", "Visualize the DTW Path", BoolToEnabledDisabled(VisualizeDTW));

            }

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
                        Console.WriteLine(" Base Frame Start: {0}", BaseFrameStart);
                        if (BaseFrameEnd > 0)
                        {
                            Console.WriteLine(" Base Frame End: {0}", BaseFrameEnd);
                        }
                        else
                        {
                            Console.WriteLine(" Base Frame End: last frame in file");
                        }
                    }
                    else
                    {
                        Console.WriteLine(" Base Frame List: {0}", BaseFrameList);
                    }
                    break;

                case BaseFrameSelectionModes.SumFirstNFrames:
                case BaseFrameSelectionModes.SumMidNFrames:
                    Console.WriteLine(" Base Frames to Sum: {0}", BaseFrameSumCount);
                    break;

                case BaseFrameSelectionModes.SumFirstNPercent:
                case BaseFrameSelectionModes.SumMidNPercent:
                    Console.WriteLine(" Base Frames to Sum: {0}%", BaseFrameSumCount);
                    break;

            }

            Console.WriteLine();
            if (FrameStart > 0 || FrameEnd > 0)
            {
                Console.WriteLine(" First frame to process {0}", FrameStart);
                if (FrameEnd > 0)
                {
                    Console.WriteLine(" Last frame to process: {0}", FrameEnd);
                }
                else
                {
                    Console.WriteLine(" Last frame to process: last frame in file");
                }
            }

            Console.WriteLine(" Maximum shift: {0} scans", MaxShiftScans);
            Console.WriteLine(" Minimum Intensity threshold Fraction: {0}", MinimumIntensityThresholdFraction);

            if (DriftScanFilterMin > 0 || DriftScanFilterMax > 0 || MzFilterMin > 0 || MzFilterMax > 0)
            {
                Console.WriteLine();
                Console.WriteLine(" Data selection restrictions for generating the drift time TIC");

                if (DriftScanFilterMin > 0 || DriftScanFilterMax > 0)
                {
                    Console.WriteLine(" Minimum drift time scan: {0}", DriftScanFilterMin);
                    if (DriftScanFilterMax > 0)
                    {
                        Console.WriteLine(" Maximum drift time scan: {0}", DriftScanFilterMax);
                    }
                    else
                    {
                        Console.WriteLine(" Maximum drift time scan: last scan in frame");
                    }
                }

                if (MzFilterMin > 0 || MzFilterMax > 0)
                {
                    Console.WriteLine(" Minimum m/z: {0}", MzFilterMin);
                    if (MzFilterMax > 0)
                    {
                        Console.WriteLine(" Maximum m/z: {0}", MzFilterMax);
                    }
                    else
                    {
                        Console.WriteLine(" Maximum m/z: highest observed m/z (no filter)");
                    }
                }
            }

            Console.WriteLine(" Scans to smooth: {0}", ScanSmoothCount);
            if (DebugMode)
                Console.WriteLine(" Showing debug messages");

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

        }

        /// <summary>
        /// Copy the options with a Memberwise Clone
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
