using System;
using System.Collections.Generic;
using System.Linq;

namespace IMSDriftTimeAligner
{
    internal class clsBinarySearchFindNearest
    {
        /// <summary>
        /// Tracks X and Y values
        /// </summary>
        /// <remarks>Sorted ascending on X</remarks>
        private readonly List<KeyValuePair<double, double>> mXYMapping = new List<KeyValuePair<double, double>>();

        /// <summary>
        /// This list is used to find the closest matching data point in mXYMapping based on an input X value
        /// </summary>
        private readonly List<double> mValueKeys = new List<double>();

        /// <summary>
        /// This list is parallel to mValueKeys
        /// </summary>
        private readonly List<int> mValueKeyIndices = new List<int>();

        /// <summary>
        /// Add data using a dictionary where keys and values are integers
        /// </summary>
        /// <param name="values"></param>
        public void AddData(Dictionary<int, int> values)
        {
            var xyValues = values.Select(item => new KeyValuePair<double, double>(item.Key, item.Value));

            AddData(xyValues);
        }

        /// <summary>
        /// Add data using parallel lists of doubles
        /// </summary>
        /// <param name="xValues"></param>
        /// <param name="yValues"></param>
        // ReSharper disable once UnusedMember.Global
        public void AddData(List<double> xValues, List<double> yValues)
        {
            if (xValues.Count != yValues.Count)
            {
                throw new InvalidOperationException(
                    "Length of xValues and yValues should be identical; " +
                    "currently " + xValues.Count + " and " + yValues.Count);
            }

            // Convert the parallel lists to a single list
            var xyValues = ConvertParallelLists(xValues, yValues);

            AddData(xyValues);
        }


        /// <summary>
        /// Add data using a list of KeyValuePairs
        /// </summary>
        /// <param name="xyValues"></param>
        public void AddData(IEnumerable<KeyValuePair<double, double>> xyValues)
        {

            // Assure that the items are sorted ascending
            var query = (from item in xyValues orderby item.Key select item);

            mXYMapping.Clear();
            mXYMapping.AddRange(query);

            // Populate the parallel lists used by GetYForX
            mValueKeys.Clear();
            mValueKeyIndices.Clear();

            var lastValue = double.MinValue;

            for (var i = 0; i < mXYMapping.Count; i++)
            {
                var valueToAdd = mXYMapping[i].Key;

                if (Math.Abs(valueToAdd - lastValue) < float.Epsilon)
                    continue;

                mValueKeys.Add(valueToAdd);
                mValueKeyIndices.Add(i);

                lastValue = valueToAdd;
            }

        }

        private IEnumerable<KeyValuePair<double, double>> ConvertParallelLists(IReadOnlyList<double> list1, IReadOnlyList<double> list2)
        {
            var dataPoints = new List<KeyValuePair<double, double>>();
            for (var i = 0; i < list1.Count; i++)
            {
                if (i >= list2.Count)
                    break;
                dataPoints.Add(new KeyValuePair<double, double>(list1[i], list2[i]));
            }

            return dataPoints;
        }

        /// <summary>
        /// Given the X value, find the closest X value in mXYMapping then return the interpolated Y value
        /// </summary>
        /// <param name="xValue">The X value to find</param>
        /// <returns>Corresponding Y value</returns>
        public double GetYForX(double xValue)
        {
            // Find the closest data point in mValueKeys

            var matchIndex = mValueKeys.BinarySearch(xValue);

            if (matchIndex >= 0)
            {
                // Exact match was found
                var resultIndex = mValueKeyIndices[matchIndex];
                return mXYMapping[resultIndex].Value;
            }

            if (mValueKeyIndices.Count < 1)
            {
                throw new Exception("Cannot interpolate the X value since mValueKeyIndices does not contain multiple entries");
            }

            var closestMatch = ~matchIndex - 1;
            if (closestMatch < 0)
                closestMatch = 0;

            if (closestMatch == mValueKeyIndices.Count - 1)
            {
                // Matched the last entry in mValueKeyIndices
                // Decrement by one so that we can interpolate
                closestMatch--;
            }
            var resultIndex1 = mValueKeyIndices[closestMatch];
            var resultIndex2 = mValueKeyIndices[closestMatch + 1];

            var x1 = mXYMapping[resultIndex1].Key;
            var x2 = mXYMapping[resultIndex2].Key;

            var y1 = mXYMapping[resultIndex1].Value;
            var y2 = mXYMapping[resultIndex2].Value;

            var closestY = InterpolateY(x1, x2, y1, y2, xValue);
            return closestY;
        }

        /// <summary>
        /// Given two X,Y coordinates interpolate or extrapolate to determine the Y value that would be seen for a given X value
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="xValueToInterpolate"></param>
        /// <returns></returns>
        private double InterpolateY(double x1, double x2, double y1, double y2, double xValueToInterpolate)
        {

            var xDifference = x2 - x1;

            if (Math.Abs(xDifference) < double.Epsilon)
                throw new ArgumentException("x1 and x2 are identical; cannot interpolate");

            var interpolatedValue = y1 + (y2 - y1) * ((xValueToInterpolate - x1) / xDifference);
            return interpolatedValue;
        }

    }
}
