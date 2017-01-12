
namespace IMSDriftTimeAligner
{
    public class FrameAlignmentOptions
    {

        #region "Enums"

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

        public enum AlignmentMethods
        {
            LinearRegression = 0,
            COW = 1
        }

        #endregion

        #region "Properties"

        /// <summary>
        /// Method for aligning the data for each frame to the base frame
        /// </summary>
        public AlignmentMethods AlignmentMethod { get; set; }

        /// <summary>
        /// Method for selecting the base frame to align all the other frames to
        /// </summary>
        public BaseFrameSelectionModes BaseFrameSelectionMode { get; set; }

        /// <summary>
        /// Number of frames to use, or percentage of frames to use, when the BaseFrameSelection mode is NFrames or NPercent
        /// </summary>
        /// <remarks>When specifying a percentage, must be a value between 1 and 100</remarks>
        public int BaseFrameSumCount { get; set; }

        /// <summary>
        /// First frame to use when the BaseFrameSelection mode is UserSpecifiedFrameRange
        /// </summary>
        public int BaseFrameStart { get; set; }

        /// <summary>
        /// Last frame to use when the BaseFrameSelection mode is UserSpecifiedFrameRange
        /// </summary>
        public int BaseFrameEnd { get; set; }

        /// <summary>
        /// Frame to start processing at
        /// </summary>
        /// <remarks>0 to start at the first frame</remarks>
        public int FrameStart { get; set; }

        /// <summary>
        /// Frame to stop processing at
        /// </summary>
        /// <remarks>Set FrameStart and FrameEnd to 0 to process all frames</remarks>
        public int FrameEnd { get; set; }

        /// <summary>
        /// Maximum number of scans that data in a frame is allowed to be shifted when aligning to the base frame data
        /// </summary>
        public int MaxShiftScans { get; set; }

        /// <summary>
        /// Value to multiple the maximum TIC value by to determine an intensity threshold,
        /// below which intensity values will be set to 0
        /// </summary>
        public double MinimumIntensityThresholdFraction { get; set; }

        /// <summary>
        /// Number of points to use when smoothing TICs before aligning
        /// </summary>
        public int ScanSmoothCount { get; set; }

        /// <summary>
        /// When true, the output file will have a single, merged frame
        /// When false, the output file will have all of the original frames, with their IMS drift times aligned
        /// </summary>
        public bool MergeFrames { get; set; }

        /// <summary>
        /// When true, a merged frame of data will be appended to the output file as a new frame
        /// </summary>
        /// <remarks>Option MergeFrames is ignored if option AppendMergedFrame is true</remarks>
        public bool AppendMergedFrame { get; set; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public FrameAlignmentOptions()
        {
            AlignmentMethod = AlignmentMethods.LinearRegression;
            
            BaseFrameSelectionMode = BaseFrameSelectionModes.SumMidNFrames;

            BaseFrameSumCount = 15;
            BaseFrameStart = 0;
            BaseFrameEnd = 0;

            FrameStart = 0;
            FrameEnd = 0;

            MaxShiftScans = 150;
            MinimumIntensityThresholdFraction = 0.1;
            ScanSmoothCount = 7;

            MergeFrames = false;

            AppendMergedFrame = false;
        }

        public FrameAlignmentOptions ShallowCopy()
        {
            return (FrameAlignmentOptions)this.MemberwiseClone();
        }
    }
}
