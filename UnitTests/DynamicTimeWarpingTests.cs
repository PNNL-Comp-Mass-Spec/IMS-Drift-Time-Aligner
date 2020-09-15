using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using IMSDriftTimeAligner;
using NUnit.Framework;
using PRISM;
using PRISM.Logging;

namespace IMSDriftTimeAligner_UnitTests
{
    [TestFixture]
    public class DynamicTimeWarpingTests
    {
        // Ignore Spelling: Sakoe

        /// <summary>
        /// Load a tab-delimited file with two columns of data, then align the data
        /// </summary>
        /// <param name="dataFileName"></param>
        /// <param name="dtwSakoeChibaMaxShiftPercent">
        /// Maximum Sakoe Chiba Shift, as a percentage of the number of points used for Dynamic Time Warping
        /// Should be a value between 0.01 and 100
        /// </param>
        /// <param name="expectedCost"></param>
        /// <param name="expectedAverageScanShifts"></param>
        /// <remarks>
        /// Plot generation requires a single-thread apartment, thus the use of ApartmentState.STA;
        /// see also https://stackoverflow.com/a/35587531/1179467
        /// </remarks>
        [Test]
        [Apartment(ApartmentState.STA)]
        [TestCase("AlignmentTestData1.txt", 5, 19.2, "0", "43", "50", "72", "100", "100", "100", "100", "100")]
        public void TestSimpleAlignment(string dataFileName, double dtwSakoeChibaMaxShiftPercent, double expectedCost, params string[] expectedAverageScanShifts)
        {
            try
            {
                var expectedAverageScanShiftByDriftTimeScanPercentile = new List<int>();

                foreach (var item in expectedAverageScanShifts)
                {
                    if (!int.TryParse(item, out var scanShift))
                    {
                        Assert.Fail("Value in expectedAverageScanShifts is not an integer: " + item);
                    }

                    expectedAverageScanShiftByDriftTimeScanPercentile.Add(scanShift);
                }
                var dataFile = FindUnitTestFile(dataFileName);
                if (dataFile == null)
                    return;

                var baseFrameData = new List<double>();
                var comparisonFrameData = new List<double>();

                using (var reader = new StreamReader(new FileStream(dataFile.FullName, FileMode.Open, FileAccess.ReadWrite)))
                {
                    var lineCount = 0;
                    while (!reader.EndOfStream)
                    {
                        lineCount++;
                        var dataLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        var dataValues = dataLine.Split('\t');
                        if (dataValues.Length < 2)
                        {
                            Console.WriteLine("Skipping line {0} since it does not have 2 columns", lineCount);
                            continue;
                        }

                        if (!double.TryParse(dataValues[0], out var dataPointA))
                        {
                            Console.WriteLine("Value in column 1 of line {0} is not numeric; skipping", lineCount);
                            continue;
                        }

                        if (!double.TryParse(dataValues[1], out var dataPointB))
                        {
                            Console.WriteLine("Value in column 2 of line {0} is not numeric; skipping", lineCount);
                            continue;
                        }

                        baseFrameData.Add(dataPointA);
                        comparisonFrameData.Add(dataPointB);
                    }
                }

                var scanNumsInFrame = new List<int>();
                for (var i = 0; i < baseFrameData.Count; i++)
                {
                    scanNumsInFrame.Add(i + 1);
                }

                var scanStart = scanNumsInFrame.First();
                var scanEnd = scanNumsInFrame.Last();

                var options = new FrameAlignmentOptions
                {
                    AlignmentMethod = FrameAlignmentOptions.AlignmentMethods.DynamicTimeWarping,
                    DTWSakoeChibaMaxShiftPercent = dtwSakoeChibaMaxShiftPercent,
                    SaveDTWPlots = true,
                    SavePlotData = true
                };

                var alignmentEngine = new DriftTimeAlignmentEngine(options);
                RegisterEvents(alignmentEngine);

                var outputDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "DynamicTimeWarpingTests"));
                if (!outputDirectory.Exists)
                {
                    Console.WriteLine("Creating output directory at " + outputDirectory.FullName);
                    outputDirectory.Create();
                }

                var datasetName = "TestSimpleAlignment";

                var statsFile = new FileInfo(Path.Combine(outputDirectory.FullName, string.Format("DynamicTimeWarping_{0}.txt", datasetName)));
                Console.WriteLine("Creating stats file at " + statsFile.FullName);

                var pngFileInfo = new FileInfo(Path.Combine(outputDirectory.FullName, datasetName + "_Frame1.png"));

                if (statsFile.Exists)
                {
                    statsFile.Delete();
                }

                if (pngFileInfo.Exists)
                {
                    pngFileInfo.Delete();
                }

                var success = true;

                using (var statsWriter = new StatsWriter(statsFile.FullName, options, string.Empty))
                {
                    RegisterEvents(statsWriter);

                    statsWriter.WriteHeader();

                    var frameScanAlignmentMap = alignmentEngine.AlignFrameDataDTW(
                        1, comparisonFrameData.ToArray(),
                        baseFrameData.ToArray(), scanNumsInFrame,
                        statsWriter, scanStart, scanEnd,
                        datasetName,
                        outputDirectory);

                    Console.WriteLine();

                    statsFile.Refresh();
                    if (!statsFile.Exists)
                    {
                        ConsoleMsgUtils.ShowWarning("The stats file was not created: " + statsFile.FullName);
                        success = false;
                    }
                    else
                    {
                        Console.WriteLine("Stats file created at " + statsFile.FullName);
                    }

                    pngFileInfo.Refresh();
                    if (!pngFileInfo.Exists)
                    {
                        ConsoleMsgUtils.ShowWarning("The plot file was not created: " + pngFileInfo.FullName);
                        success = false;
                    }
                    else
                    {
                        Console.WriteLine("Plot file created at " + pngFileInfo.FullName);
                    }

                    Console.WriteLine();
                    Console.WriteLine("{0,-15} {1,-12}", "Original Scan", "New Scan");

                    foreach (var keyName in (from item in frameScanAlignmentMap.Keys orderby item select item))
                    {
                        if (keyName % 100 == 0)
                        {
                            Console.WriteLine("{0,-15} {1,-12}", keyName, frameScanAlignmentMap[keyName]);
                        }
                    }
                }

                Console.WriteLine();
                var validStats = ValidateStats(statsFile, expectedCost, expectedAverageScanShiftByDriftTimeScanPercentile);
                if (!validStats)
                    success = false;

                Assert.True(success, "Processing error");

            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in TestSimpleAlignment", ex);
                Assert.Fail("Exception: " + ex.Message);
            }
        }

        private FileInfo FindUnitTestFile(string dataFileName)
        {
            var localDirPath = Path.Combine("..", "..", "Data");
            var remoteDirPath = @"\\proto-2\UnitTest_Files\IMSDriftTimeAligner";

            var localFile = new FileInfo(Path.Combine(localDirPath, dataFileName));

            if (localFile.Exists)
            {
                return localFile;
            }

            // Look for the file on Proto-2
            var remoteFile = new FileInfo(Path.Combine(remoteDirPath, dataFileName));
            if (remoteFile.Exists)
            {
                return remoteFile;
            }

            var msg = string.Format("File not found: {0}; checked in both {1} and {2}", dataFileName, localDirPath, remoteDirPath);

            Console.WriteLine(msg);
            Assert.Fail(msg);

            return null;
        }


        private bool ValidateStats(FileSystemInfo statsFile, double expectedCost, IReadOnlyList<int> expectedAverageScanShiftByDriftTimeScanPercentile)
        {
            using (var reader = new StreamReader(new FileStream(statsFile.FullName, FileMode.Open, FileAccess.ReadWrite)))
            {
                while (!reader.EndOfStream)
                {
                    var dataLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    if (!dataLine.StartsWith("Frame    Cost"))
                        continue;

                    if (reader.EndOfStream)
                    {
                        ConsoleMsgUtils.ShowWarning("Corrupt stats file; missing the stats line: " + statsFile.FullName);
                        return false;
                    }
                    var statsLine = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(statsLine))
                    {
                        ConsoleMsgUtils.ShowWarning("Corrupt stats file; empty stats line: " + statsFile.FullName);
                        return false;
                    }

                    var dataColumnsWithSpaces = statsLine.Split(' ');
                    var dataColumns = dataColumnsWithSpaces.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();

                    if (!double.TryParse(dataColumns[1], out var cost))
                    {
                        ConsoleMsgUtils.ShowWarning("Corrupt stats file; could not extract alignment cost: " + statsFile.FullName);
                        return false;
                    }

                    var scanShiftByDriftTimeScanPercentile = new List<int>();
                    for (var i = 2; i < dataColumns.Count; i++)
                    {
                        if (!int.TryParse(dataColumns[i], out var averageShift))
                        {
                            ConsoleMsgUtils.ShowWarning("Corrupt stats file; {0} in column {1} is not numeric: {2}",
                                dataColumns[i], i + 1, statsFile.FullName);

                            return false;
                        }

                        scanShiftByDriftTimeScanPercentile.Add(averageShift);
                    }

                    Console.WriteLine("Alignment cost: {0}", cost);

                    Assert.AreEqual(expectedCost, cost, 0.05, "Cost mismatch");

                    if (expectedAverageScanShiftByDriftTimeScanPercentile.Count < scanShiftByDriftTimeScanPercentile.Count)
                    {
                        Assert.Fail(
                            "List expectedAverageScanShiftByDriftTimeScanPercentile should have {0} items but it actually has {1} items",
                            scanShiftByDriftTimeScanPercentile.Count,
                            expectedAverageScanShiftByDriftTimeScanPercentile.Count);
                    }

                    for (var i = 0; i < scanShiftByDriftTimeScanPercentile.Count; i++)
                    {
                        var percentile = (i + 1) * 10;

                        Console.WriteLine("Average scan shift for {0}th percentile: {1}", percentile, scanShiftByDriftTimeScanPercentile[i]);

                        Assert.AreEqual(
                            expectedAverageScanShiftByDriftTimeScanPercentile[i],
                            scanShiftByDriftTimeScanPercentile[i],
                            "Average shift mismatch for {0}%", percentile);
                    }
                }
            }

            return true;
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
