﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FileProcessor;
using UIMFLibrary;
using LinReg = Statistics.LinearRegression;
using MessageEventArgs = FileProcessor.MessageEventArgs;

namespace IMSDriftTimeAligner
{
    class DriftTimeAlignmentEngine
    {
        #region "Structs"

        private struct udtFrameRange
        {
            public int Start;
            public int End;
        }

        #endregion

        #region "Classwide variables"

        private bool mSmoothedBaseFrameDataWritten;
        #endregion

        #region "Properties"

        /// <summary>
        /// List of recent error messages
        /// </summary>
        /// <remarks>Old messages are cleared when ProcessFile is called</remarks>
        // ReSharper disable once CollectionNeverQueried.Global
        public List<string> ErrorMessages { get; }

        /// <summary>
        /// Alignment options
        /// </summary>
        public FrameAlignmentOptions Options { get; set; }

        public bool ShowDebugMessages { get; set; }

        /// <summary>
        /// List of recent warning messages
        /// </summary>
        /// <remarks>Old messages are cleared when ProcessFile is called</remarks>
        // ReSharper disable once CollectionNeverQueried.Global
        public List<string> WarningMessages { get; }

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        public DriftTimeAlignmentEngine()
        {
            ErrorMessages = new List<string>();
            WarningMessages = new List<string>();
        }

        private Dictionary<int, int> AlignFrameDataCOW(
            List<ScanInfo> baseFrameScans,
            List<ScanInfo> frameScans,
            FrameAlignmentOptions alignmentOptions)
        {
            throw new NotImplementedException();
        }

        private Dictionary<int, int> AlignFrameDataLinearRegression(
            List<ScanInfo> baseFrameScans,
            List<ScanInfo> frameScans,
            FrameAlignmentOptions alignmentOptions)
        {
            // Keys are the old scan number and values are the new scan number
            var frameScanAlignmentMap = new Dictionary<int, int>();

            try
            {
                // Determine the first and last scan number with non-zero TIC values
                var nonzeroScans1 = (from item in baseFrameScans where item.TIC > 0 orderby item.Scan select item.Scan).ToList();
                var nonzeroScans2 = (from item in frameScans where item.TIC > 0 orderby item.Scan select item.Scan).ToList();

                var scanStart = Math.Min(nonzeroScans1.First(), nonzeroScans2.First());
                var scanEnd = Math.Max(nonzeroScans1.Last(), nonzeroScans2.Last());

                // Populate the arrays, storing TIC values in the appropriate index of baseFrameData and frameData
                var baseFrameData = StoreTICValues(BASE_FRAME_DESCRIPTION, scanStart, scanEnd, baseFrameScans, alignmentOptions);
                var frameData = StoreTICValues("Frame " + frameNum, scanStart, scanEnd, frameScans, alignmentOptions);

                var offset = 0;
                var correlationByOffset = new Dictionary<int, double>();

                var shiftPositive = true;

                while (true)
                {
                    var frameDataShifted = new double[baseFrameData.Length];
                    var targetIndex = 0;

                    for (var sourceIndex = offset; sourceIndex < frameData.Length; sourceIndex++)
                    {
                        if (sourceIndex >= 0)
                        {
                            frameDataShifted[targetIndex] = frameData[sourceIndex];
                        }
                        targetIndex++;
                        if (targetIndex >= frameData.Length)
                            break;
                    }

                    var coeff = MathNet.Numerics.LinearRegression.SimpleRegression.Fit(frameDataShifted, baseFrameData);

                    var slope = coeff.Item1;
                    var intercept = coeff.Item2;

                    var rSquared = MathNet.Numerics.GoodnessOfFit.RSquared(frameData.Select(x => intercept + slope * x), baseFrameData);

                    const bool TEST_LINREG = true;
                    if (TEST_LINREG)
                    {
                        var regressor = new LinReg();
                        var success = regressor.Start(baseFrameData, frameDataShifted, 1, UseLogY: false);

                        var slopeAlt = regressor.Coefficients[0];
                        var interceptAlt = regressor.Coefficients[1];
                        var rSquaredAlt = regressor.CorrelationCoefficient;

                        Console.WriteLine("Slope:     {0:0.000}  vs {1:0.000}", slope, slopeAlt);
                        Console.WriteLine("Intercept: {0:0.000}  vs {1:0.000}", intercept, interceptAlt);
                        Console.WriteLine("R-Squared: {0:0.000}  vs {1:0.000}", rSquared, rSquaredAlt);
                        Console.WriteLine();
                    }
                    var rSquared = MathNet.Numerics.GoodnessOfFit.RSquared(frameDataShifted.Select(x => intercept + slope * x), baseFrameData);

                    correlationByOffset.Add(offset, rSquared);

                    if (shiftPositive)
                    {
                        offset = Math.Abs(offset) + 1;
                        shiftPositive = false;
                    }
                    else
                    {
                        offset = -offset;
                        shiftPositive = true;
                    }

                    if (Math.Abs(offset) > alignmentOptions.MaxShiftScans)
                    {
                        // Exit the while loop
                        break;
                    }
                }

                var rankedOffsets = (from item in correlationByOffset orderby item.Value descending select item).ToList();

                if (ShowDebugMessages)
                {
                    ReportMessage("Top 5 offsets:");
                    ReportMessage(string.Format("{0,-12} {1}", "Offset_Scans", "R-Squared"));
                    for (var i = 0; i < 5; i++)
                    {
                        ReportMessage(string.Format("{0,-12} {1:0.00}", rankedOffsets[i].Key, rankedOffsets[i].Value));
                    }

                }

                var bestOffset = rankedOffsets.First().Key;

                for (var sourceScan = scanStart; sourceScan <= scanEnd; sourceScan++)
                {
                    var targetScan = sourceScan - bestOffset;
                    if (targetScan >= 0)
                    {
                        frameScanAlignmentMap.Add(sourceScan, targetScan);
                    }
                }

            }
            catch (Exception ex)
            {
                ReportError("Error in AlignFrameDataLinearRegression: " + ex.Message);
            }

            return frameScanAlignmentMap;
        }

        /// <summary>
        /// Align the TIC data in frameData to baseFrameData
        /// </summary>
        /// <param name="baseFrameScans"></param>
        /// <param name="frameScans"></param>
        /// <param name="alignmentOptions">Alignment options, including the alignment method</param>
        /// <returns>Dictionary where keys are the old scan number and values are the new scan number</returns>
        public Dictionary<int, int> AlignFrameTICToBase(
            List<ScanInfo> baseFrameScans,
            List<ScanInfo> frameScans,
            FrameAlignmentOptions alignmentOptions)
        {

            Dictionary<int, int> frameScanAlignmentMap;

            switch (alignmentOptions.AlignmentMethod)
            {
                case FrameAlignmentOptions.AlignmentMethods.LinearRegression:
                    frameScanAlignmentMap = AlignFrameDataLinearRegression(baseFrameScans, frameScans, alignmentOptions);
                    break;
                case FrameAlignmentOptions.AlignmentMethods.COW:
                    frameScanAlignmentMap = AlignFrameDataCOW(baseFrameScans, frameScans, alignmentOptions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(alignmentOptions.AlignmentMethod),
                        "Unrecognized alignment method");
            }


            return frameScanAlignmentMap;
        }


        /// <summary>
        /// Append a new frame using merged frame data
        /// </summary>
        /// <param name="reader">UIMF Reader</param>
        /// <param name="writer">UIMF Writer</param>
        /// <param name="referenceFrameNum">Frame number to use as reference for frame parameters</param>
        /// <param name="mergedFrameNum">Frame number to use for the merged frame data</param>
        /// <param name="mergedFrameScans">
        /// Single frame of data where scan intensities are accumulated (summed) as each frame is processed
        /// Keys are the aligned scan number, values are intensities by bin
        /// </param>
        private void AppendMergedFrame(
            DataReader reader,
            DataWriter writer,
            int referenceFrameNum,
            int mergedFrameNum,
            Dictionary<int, int[]> mergedFrameScans)
        {
            Console.WriteLine();
            ReportMessage($"Appending the merged frame data");

            var frameParams = reader.GetFrameParams(referenceFrameNum);

            writer.InsertFrame(mergedFrameNum, frameParams);

            var binWidth = reader.GetGlobalParams().BinWidth;
            var scansProcessed = 0;

            foreach (var scanItem in mergedFrameScans)
            {
                if (scansProcessed % 250 == 0)
                    ReportMessage($"  storing scan {scanItem.Key}");

                var scanNumNew = scanItem.Key;
                var intensities = scanItem.Value;
                writer.InsertScan(mergedFrameNum, frameParams, scanNumNew, intensities, binWidth);

                scansProcessed++;
            }

        }

        /// <summary>
        /// Compute the IMS-based TIC value across scans
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="baseFrameRange"></param>
        /// <param name="frameScansSummed">Scan info from the first frame, but with NonZeroCount and TIC values summed across the frames</param>
        private void GetSummedFrameScans(DataReader reader, udtFrameRange baseFrameRange, out List<ScanInfo> frameScansSummed)
        {

            frameScansSummed = reader.GetFrameScans(baseFrameRange.Start);
            if (baseFrameRange.Start == baseFrameRange.End)
                return;

            // Summing multiple frames
            // Frames can have different scans, so use a dictionary to keep track of data on a per-scan basis
            // Keys are scan number, values are the ScanInfo for that scan
            var scanData = new Dictionary<int, ScanInfo>(frameScansSummed.Count);
            foreach (var scanItem in frameScansSummed)
            {
                scanData.Add(scanItem.Scan, scanItem);
            }

            int frameMin;
            int frameMax;
            LookupValidFrameRange(reader, out frameMin, out frameMax);

            // Sum the TIC values by IMS frame
            for (var frameNum = baseFrameRange.Start + 1; frameNum <= baseFrameRange.End; frameNum++)
            {
                if (frameNum < frameMin || frameNum > frameMax)
                    continue;

                var frameScans = reader.GetFrameScans(frameNum);

                foreach (var sourceScanInfo in frameScans)
                {
                    ScanInfo targetScanInfo;
                    if (scanData.TryGetValue(sourceScanInfo.Scan, out targetScanInfo))
                    {
                        targetScanInfo.NonZeroCount += sourceScanInfo.NonZeroCount;
                        targetScanInfo.TIC += sourceScanInfo.TIC;
                    } else
                    {
                        scanData.Add(sourceScanInfo.Scan, sourceScanInfo);
                    }
                }

            }

            frameScansSummed.Clear();
            frameScansSummed.AddRange(from item in scanData orderby item.Key select item.Value);

        }

        /// <summary>
        /// Determine the base frame range
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>Pair of integers representing the start frame and end frame to use for the base frame</returns>
        /// <remarks>Uses Options.BaseFrameSelectionMode</remarks>
        private udtFrameRange GetBaseFrameRange(DataReader reader)
        {
            return GetBaseFrameRange(reader, Options);
        }

        /// <summary>
        /// Determine the base frame range
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="frameAlignmentOptions">Base frame alignment options</param>
        /// <returns>Pair of integers representing the start frame and end frame to use for the base frame</returns>
        private udtFrameRange GetBaseFrameRange(DataReader reader, FrameAlignmentOptions frameAlignmentOptions)
        {
            int baseFrameStart;
            int baseFrameEnd;

            var masterFrameList = reader.GetMasterFrameList();

            var frameMin = masterFrameList.Keys.Min();
            var frameMax = masterFrameList.Keys.Max();

            var baseFrameSumCount = frameAlignmentOptions.BaseFrameSumCount;
            if (baseFrameSumCount < 1)
                baseFrameSumCount = 1;

            var frameCountFromPercent = (int)Math.Floor(baseFrameSumCount / 100.0 * (frameMax - frameMin));
            if (frameCountFromPercent < 1)
                frameCountFromPercent = 1;

            switch (frameAlignmentOptions.BaseFrameSelectionMode)
            {
                case FrameAlignmentOptions.BaseFrameSelectionModes.FirstFrame:
                    baseFrameStart = frameMin;
                    baseFrameEnd = frameMin;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.MidpointFrame:
                    baseFrameStart = frameMin + (frameMax - frameMin) / 2;
                    if (baseFrameStart > frameMax)
                    {
                        baseFrameStart = frameMax;
                    }
                    baseFrameEnd = baseFrameStart;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.MaxTICFrame:
                    var ticByFrame = reader.GetTICByFrame(frameMin, frameMax, 0, 0);
                    var frameMaxTIC = (from item in ticByFrame orderby item.Value descending select item).First();
                    baseFrameStart = frameMaxTIC.Key;
                    baseFrameEnd = frameMaxTIC.Key;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.UserSpecifiedFrameRange:
                    baseFrameStart = frameAlignmentOptions.BaseFrameStart;
                    baseFrameEnd = frameAlignmentOptions.BaseFrameEnd;
                    if (baseFrameStart > 0 && (baseFrameEnd <= 0 || baseFrameEnd < baseFrameStart))
                    {
                        baseFrameEnd = baseFrameStart;
                    }

                    if (baseFrameStart <= 0 && baseFrameEnd <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(frameAlignmentOptions.BaseFrameEnd),
                            "BaseFrameEnd must be non-zero when the BaseFrameSelectionMode is UserSpecifiedFrameRange");
                    }
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumFirstNFrames:
                    baseFrameStart = frameMin;
                    baseFrameEnd = frameMin + baseFrameSumCount - 1;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumFirstNPercent:
                    baseFrameStart = frameMin;
                    baseFrameEnd = frameMin + frameCountFromPercent - 1;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumMidNFrames:
                case FrameAlignmentOptions.BaseFrameSelectionModes.SumMidNPercent:

                    // First call this function to determine the midpoint frame number 
                    // (Start and End in midpointFrameRange will be identical)
                    var tempOptions = frameAlignmentOptions.ShallowCopy();
                    tempOptions.BaseFrameSelectionMode = FrameAlignmentOptions.BaseFrameSelectionModes.MidpointFrame;
                    var midpointFrameRange = GetBaseFrameRange(reader, tempOptions);

                    int leftCount;
                    int rightCount;

                    if (frameAlignmentOptions.BaseFrameSelectionMode == FrameAlignmentOptions.BaseFrameSelectionModes.SumMidNFrames)
                    {
                        // Mid frames, by frame count
                        leftCount = (int)Math.Floor((baseFrameSumCount - 1) / 2.0);
                        rightCount = baseFrameSumCount - leftCount - 1;
                    }
                    else
                    {
                        // Mid frames, by percentage
                        leftCount = (int)Math.Floor((frameCountFromPercent - 1) / 2.0);
                        rightCount = frameCountFromPercent - leftCount - 1;
                    }

                    baseFrameStart = midpointFrameRange.Start - leftCount;
                    baseFrameEnd = midpointFrameRange.Start + rightCount;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumAll:
                    baseFrameStart = frameMin;
                    baseFrameEnd = frameMax;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(frameAlignmentOptions.BaseFrameSelectionMode),
                        "Unrecognized value for BaseFrameSelectionMode");
            }

            if (baseFrameEnd < baseFrameStart)
                baseFrameEnd = baseFrameStart;

            var frameRange = new udtFrameRange
            {
                Start = baseFrameStart,
                End = baseFrameEnd
            };

            return frameRange;
        }

        private bool CloneUimf(DataReader reader, FileInfo sourceFile, FileInfo outputFile)
        {
            var tablesToSkip = new List<string> {
                        "Frame_Parameters",
                        "Frame_Params",
                        "Frame_Scans" };

            var frameTypesToAlwaysCopy = new List<UIMFLibrary.DataReader.FrameType>();

            var success = reader.CloneUIMF(outputFile.FullName, tablesToSkip, frameTypesToAlwaysCopy);

            if (success)
                return true;

            ReportError("Error cloning the source UIMF file " + sourceFile.Name);
            return false;
        }

        private void GetFrameRangeToProcess(DataReader reader, out int frameStart, out int frameEnd)
        {
            var masterFrameList = reader.GetMasterFrameList();

            frameStart = masterFrameList.Keys.Min();
            frameEnd = masterFrameList.Keys.Max();

            if (Options.FrameStart > 0 && Options.FrameStart >= masterFrameList.Keys.Min())
                frameStart = Options.FrameStart;

            if (Options.FrameEnd > 0 && Options.FrameEnd <= masterFrameList.Keys.Max())
                frameEnd = Options.FrameEnd;
        }

        private FileInfo InitializeOutputFile(FileInfo sourceFile, string outputFilePath)
        {

            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                var outputFileName = Path.GetFileNameWithoutExtension(sourceFile.Name) + "_new" + Path.GetExtension(sourceFile.Name);
                if (sourceFile.DirectoryName == null)
                {
                    outputFilePath = Path.Combine(outputFileName);
                }
                else
                {
                    outputFilePath = Path.Combine(sourceFile.DirectoryName, outputFileName);
                }
            }

            var outputFile = new FileInfo(outputFilePath);
            if (outputFile.Directory != null && !outputFile.Directory.Exists)
            {
                outputFile.Directory.Create();
            }

            if (outputFile.Exists)
            {
                outputFile.Delete();
            }

            return outputFile;
        }

        private void LookupValidFrameRange(DataReader reader, out int frameMin, out int frameMax)
        {
            var masterFrameList = reader.GetMasterFrameList();

            frameMin = masterFrameList.Keys.Min();
            frameMax = masterFrameList.Keys.Max();
        }

        public bool ProcessFile(string inputFilePath, string outputFilePath)
        {

            try
            {
                var sourceFile = new FileInfo(inputFilePath);
                if (!sourceFile.Exists)
                {
                    ReportError("Source file not found: " + inputFilePath);
                    return false;
                }

                mSmoothedBaseFrameDataWritten = false;
                if (ShowDebugMessages)
                {
                    var debugDataFile = new FileInfo(DEBUG_DATA_FILE);
                    if (debugDataFile.Exists)
                        debugDataFile.Delete();
                }

                ReportMessage(string.Format("Opening {0}\n in folder {1}", sourceFile.Name, sourceFile.Directory));
                var outputFile = InitializeOutputFile(sourceFile, outputFilePath);

                using (var reader = new UIMFLibrary.DataReader(sourceFile.FullName))
                {
                    reader.ErrorEvent += UIMFReader_ErrorEvent;

                    ReportMessage(string.Format("Cloning the .UIMF file"));
                    var success = CloneUimf(reader, sourceFile, outputFile);
                    if (!success)
                    {
                        // The error message has already been shown
                        return false;
                    }

                    int frameStart;
                    int frameEnd;

                    GetFrameRangeToProcess(reader, out frameStart, out frameEnd);

                    var baseFrameRange = GetBaseFrameRange(reader);

                    List<ScanInfo> baseFrameScans;
                    GetSummedFrameScans(reader, baseFrameRange, out baseFrameScans);

                    var mergedFrameScans = new Dictionary<int, double[]>();

                    using (var writer = new UIMFLibrary.DataWriter(outputFile.FullName))
                    {
                        for (var frameNum = frameStart; frameNum <= frameEnd; frameNum++)
                        {
                            ProcessFrame(reader, writer, frameNum, baseFrameScans, mergedFrameScans);
                        }

                        if (Options.AppendMergedFrame || Options.MergeFrames)
                        {
                            const int referenceFrameNum = 1;

                            var mergedFrameNum = 0;
                            
                            if (Options.AppendMergedFrame)
                            {
                                // Append a merged frame
                                mergedFrameNum = frameEnd + 1;
                            }
                            else if (Options.MergeFrames)
                            {
                                // The output file will only have a single, merged frame
                                mergedFrameNum = 1;
                            }

                            AppendMergedFrame(reader, writer, referenceFrameNum, mergedFrameNum, mergedFrameScans);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Error in ProcessFile: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Read the frame data from the source file, align it to baseFramedata, and write to the output file
        /// </summary>
        /// <param name="reader">UIMF Reader</param>
        /// <param name="writer">UIMF Writer</param>
        /// <param name="frameNum">Frame Number</param>
        /// <param name="baseFrameScans">Base frame scan data (most importantly, TIC by scan number)</param>
        /// <param name="mergedFrameScans">
        /// Single frame of data where scan intensities are accumulated (summed) as each frame is processed
        /// Keys are the aligned scan number, values are intensities by bin
        /// </param>
        private void ProcessFrame(
            DataReader reader,
            DataWriter writer,
            int frameNum,
            List<ScanInfo> baseFrameScans,
            Dictionary<int, double[]> mergedFrameScans)
        {
            ReportMessage($"Process frame {frameNum}");

            try
            {
                var udtCurrentFrameRange = new udtFrameRange
                {
                    Start = frameNum,
                    End = frameNum
                };

                List<ScanInfo> frameScans;
                GetSummedFrameScans(reader, udtCurrentFrameRange, out frameScans);

                // Dictionary where keys are the old scan number and values are the new scan number
                var frameScanAlignmentMap = AlignFrameTICToBase(baseFrameScans, frameScans, Options);

                var frameParams = reader.GetFrameParams(frameNum);
                bool insertFrame;

                if (Options.MergeFrames && !Options.AppendMergedFrame)
                {
                    // The output file will only have a single, merged frame
                    insertFrame = false;
                }
                else
                {
                    insertFrame = true;
                }

                if (insertFrame)
                {
                    writer.InsertFrame(frameNum, frameParams);
                }

                var binWidth = reader.GetGlobalParams().BinWidth;
                var scansProcessed = 0;

                foreach (var scanItem in frameScans)
                {
                    if (scansProcessed % 250 == 0)
                        ReportMessage($"  storing scan {scanItem.Scan}");

                    var scanNumOld = scanItem.Scan;
                    int scanNumNew;

                    if (!frameScanAlignmentMap.TryGetValue(scanNumOld, out scanNumNew))
                        continue;

                    int[] intensities;
                    try
                    {
                        intensities = reader.GetSpectrumAsBins(frameNum, frameParams.FrameType, scanNumOld);
                    }
                    catch (Exception ex)
                    {
                        ReportError($"Error retrieving data for frame {frameNum}, scan {scanNumOld}: {ex.Message}");
                        continue;
                    }


                    if (insertFrame)
                    {
                        writer.InsertScan(frameNum, frameParams, scanNumNew, intensities, binWidth);
                    }

                    int[] summedIntensities;

                    // Dictionary where Keys are the aligned scan number and values are intensities by bin
                    if (mergedFrameScans.TryGetValue(scanNumNew, out summedIntensities))
                    {
                        for (var i = 0; i < summedIntensities.Length; i++)
                        {
                            if (i >= intensities.Length)
                                break;

                            if (summedIntensities[i] + (long)intensities[i] > int.MaxValue)
                                summedIntensities[i] = int.MaxValue;
                            else
                                summedIntensities[i] += intensities[i];
                        }
                    }
                    else
                    {
                        mergedFrameScans.Add(scanNumNew, intensities);
                    }

                    scansProcessed++;
                }


            }
            catch (Exception ex)
            {
                ReportError("Error in ProcessFrame: " + ex.Message);
            }
        }

        private void ReportError(string message)
        {
            OnErrorMessage(new MessageEventArgs(message));
            ErrorMessages.Add(message);
        }

        private void ReportMessage(string message)
        {
            OnMessage(new MessageEventArgs(message));
        }

        private void ReportWarning(string message)
        {
            OnWarningMessage(new MessageEventArgs(message));
            WarningMessages.Add(message);
        }


        private void SaveSmoothedDataForDebug(
            string frameDescription, 
            int scanStart, 
            IReadOnlyList<double> frameData, 
            IReadOnlyList<double> frameDataSmoothed)
        {
            try
            {
                var debugDataFile = new FileInfo(DEBUG_DATA_FILE);

                using (var writer = new StreamWriter(new FileStream(debugDataFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    writer.WriteLine("Data smoothing comparison for " + frameDescription);
                    writer.WriteLine("{0}\t{1}\t{2}", "Scan", "TIC_Original", "TIC_Smoothed");

                    for (var i = 0; i < frameData.Count; i++)
                    {
                        writer.WriteLine("{0}\t{1}\t{2}", scanStart + i, frameData[i], frameDataSmoothed[i]);
                    }
                    writer.WriteLine();
                }

            }
            catch (Exception ex)
            {
                ReportError("Error in SaveSmoothedDataForDebug: " + ex.Message);
            }
        }

        private double[] StoreTICValues(
            string frameDescription, 
            int scanStart, 
            int scanEnd, 
            IEnumerable<ScanInfo> baseFrameScans,
            FrameAlignmentOptions alignmentOptions)
        {
            var scanCount = scanEnd - scanStart + 1;
            var frameData = new double[scanCount];

            foreach (var scan in baseFrameScans)
            {
                if (scan.Scan == 0)
                {
                    ReportWarning("Skipping scan 0 in " + frameDescription);
                    continue;
                }

                if (scan.Scan < scanStart || scan.Scan > scanEnd)
                {
                    continue;
                }

                frameData[scan.Scan - scanStart] = scan.TIC;
            }

            if (alignmentOptions.ScanSmoothCount <= 1)
                return frameData;

            // Apply a moving average smooth
            var frameDataSmoothed = MathNet.Numerics.Statistics.Statistics.MovingAverage(frameData, alignmentOptions.ScanSmoothCount).ToArray();

            if (!ShowDebugMessages)
                return frameDataSmoothed;

            var writeData = true;
            if (frameDescription == BASE_FRAME_DESCRIPTION)
            {
                if (mSmoothedBaseFrameDataWritten)
                {
                    writeData = false;
                }
                else
                {
                    mSmoothedBaseFrameDataWritten = true;
                }
            }

            if (writeData)
            {
                SaveSmoothedDataForDebug(frameDescription, scanStart, frameData, frameDataSmoothed);
            }

            return frameDataSmoothed;
        }
       

        #endregion

        #region "Events"

        public event MessageEventHandler ErrorEvent;
        public event MessageEventHandler MessageEvent;
        public event MessageEventHandler WarningEvent;

        #endregion

        #region "Event Handlers"

        private void OnErrorMessage(MessageEventArgs e)
        {
            ErrorEvent?.Invoke(this, e);
        }

        private void OnMessage(MessageEventArgs e)
        {
            MessageEvent?.Invoke(this, e);
        }

        private void OnWarningMessage(MessageEventArgs e)
        {
            WarningEvent?.Invoke(this, e);
        }

        private void UIMFReader_ErrorEvent(object sender, UIMFLibrary.MessageEventArgs e)
        {
            ReportError("UIMFReader error: " + e.Message);
        }
        #endregion
    }
}
