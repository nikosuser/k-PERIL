// See https://aka.ms/new-console-template for more information
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using kPERIL_DLL;
using System.Globalization;

namespace demo
{
    class Program
    {
        static void Main(string[] args)
        {
            bool debug = true;
            Console.WriteLine("Starting kPERIL Demo for WUINITY");
            Console.WriteLine("Based on a Fort McMurray wildfire simulation");
            
            Dictionary<string, float>? headerData;

            string rootDir = "D:/OneDrive - Imperial College London/Desktop/kPerilTest/IdealCase/";

            float[,] ros = ReadAsc(rootDir + "/ros.asc", out headerData);
            float[,] azimuth = ReadAsc(rootDir + "/azimuth.asc");
            float[,] slope = ReadAsc(rootDir + "/slope.asc");
            float[,] aspect = ReadAsc(rootDir + "/aspect.asc");
            float[,] arrivalTime = ReadAsc(rootDir + "/arrivalTime.asc");
            List<Tuple<DateTime, float, float>> windData = ExtractWindData(rootDir + "/weather.wxs");

            DateTime[] rawsTimes = windData.Select(x => x.Item1).ToArray();
            float[] windMag = windData.Select(x => x.Item2).ToArray();
            float[] windDir = windData.Select(x => x.Item3).ToArray();
            int[,] wuiArea =
            {
                { 25, 24 },
                { 26, 25 },
                { 25, 26 },
                { 24, 25 }
            };
            bool isEdgeNodeList = true;
            
            float rset = 200;
            
            kPERIL peril = new kPERIL(debug);
            (float[,] windMagRaster, float[,] windDirRaster) = peril.ConvertTemporalToSpatialWind(convertRawsWindToMidflameWind(windMag,true),windDir,rawsTimes,arrivalTime);
            //if rosTheta is known: peril.setRosTheta(rosTheta); ros is not strictly needed in the next function if rosTheta is set, but for simplicity it is still asked as an input. 
            int[,] triggerBoundary = peril.CalculateBoundary(headerData["cellsize"],rset,0,windMagRaster,windDirRaster,peril.GetPolygonEdgeNodes(wuiArea), isEdgeNodeList,ros,azimuth,slope,aspect);
            
            SaveAscInt(rootDir+"triggerBoundary.asc", triggerBoundary, headerData);
        }

        public static float[,] ReadAsc(string filePath, out Dictionary<string, float>? headerData)
        {
            string[] lines = File.ReadAllLines(filePath);
            // Read header and store in dictionary
            headerData = new Dictionary<string, float>
            {
                { "ncols", float.Parse(lines[0].Split()[lines[0].Split().Length-1]) },
                { "nrows", float.Parse(lines[1].Split()[lines[1].Split().Length-1]) },
                { "xllcorner", float.Parse(lines[2].Split()[lines[2].Split().Length-1]) },
                { "yllcorner", float.Parse(lines[3].Split()[lines[3].Split().Length-1]) },
                { "cellsize", float.Parse(lines[4].Split()[lines[4].Split().Length-1]) },
                { "NODATA_value", float.Parse(lines[5].Split()[lines[5].Split().Length-1]) }
            };

            int cols = (int)headerData["ncols"];
            int rows = (int)headerData["nrows"];

            // Initialize raster array
            float[,] raster = new float[rows, cols];

            // Read raster data
            for (int i = 0; i < rows; i++)
            {
                string[] values = lines[i + 6].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < cols; j++)
                {
                    raster[j, i] = float.Parse(values[j]);
                }
            }

            return raster;
        }
        
        // Overloaded version without header output
        public static float[,] ReadAsc(string filePath)
        {
            return ReadAsc(filePath, out _); // Call main function but discard header data
        }
        
        public static List<Tuple<DateTime, float, float>> ExtractWindData(string filePath)
        {
            List<Tuple<DateTime, float, float>> windData = new List<Tuple<DateTime, float, float>>();
            string[] lines = File.ReadAllLines(filePath);

            bool dataStarted = false;

            foreach (string line in lines)
            {
                // Skip metadata lines
                if (line.StartsWith("Year")) 
                {
                    dataStarted = true;
                    continue;
                }

                if (!dataStarted || string.IsNullOrWhiteSpace(line))
                    continue;

                // Split the data row into columns
                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 9) // Ensure we have enough columns
                    continue;

                try
                {
                    // Extract values
                    int year = int.Parse(parts[0]);
                    int month = int.Parse(parts[1]);
                    int day = int.Parse(parts[2]);
                    string timeString = parts[3].PadLeft(4, '0'); // Ensure it's 4 characters
                    int hour = int.Parse(timeString.Substring(0, 2));
                    int minute = int.Parse(timeString.Substring(2, 2));

                    float windSpeed = float.Parse(parts[7], CultureInfo.InvariantCulture);
                    float windDirection = float.Parse(parts[8]);

                    // Create DateTime object
                    DateTime timestamp = new DateTime(year, month, day, hour, minute, 0);

                    // Store in list
                    windData.Add(new Tuple<DateTime, float, float>(timestamp, windSpeed, windDirection));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line: {line}. Exception: {ex.Message}");
                }
            }
            return windData;
        }
        
        public static void SaveAscInt(string filePath, int[,] raster, Dictionary<string, float> headerData)
        {
            int rows = (int)headerData["nrows"];
            int cols = (int)headerData["ncols"];
            float noDataValue = headerData["NODATA_value"];

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Write header
                writer.WriteLine($"ncols {cols}");
                writer.WriteLine($"nrows {rows}");
                writer.WriteLine($"xllcorner {headerData["xllcorner"]}");
                writer.WriteLine($"yllcorner {headerData["yllcorner"]}");
                writer.WriteLine($"cellsize {headerData["cellsize"]}");
                writer.WriteLine($"NODATA_value {noDataValue}");

                // Write raster data
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        float value = raster[j, i];

                        // Handle NoData values
                        if (float.IsNaN(value) || value == noDataValue || value == 0)
                            writer.Write($"{noDataValue.ToString(CultureInfo.InvariantCulture)} ");
                        else
                            writer.Write($"{value.ToString(CultureInfo.InvariantCulture)} ");
                    }
                    writer.WriteLine(); // New line after each row
                }
            }

            Console.WriteLine($"ASC file saved successfully at: {filePath}");
        }

        /// <summary>
        /// Method to roughly convert free wind as recorded by a weather station to midflame windspeed. For simplicity, midflame wind is assumed to be at 2m height here. 
        /// </summary>
        /// <param name="windMag">Wind Magnitude in kph or mph</param>
        /// <param name="isMetric">True if the measurements are in kph, 10m reading height, and False if it is in mph, 20ft reading height. If false, the wind will also be converted to kph</param>
        /// <returns>The approximate midflame windspeed</returns>
        public static float[] convertRawsWindToMidflameWind(float[] windMag, bool isMetric)
        {
            float midflameHeight = 6;//feet
            float readHeight = isMetric ? 33 : 20;
            float conversionFactor = (float)Math.Log((readHeight + 0.36*midflameHeight)/(0.13*midflameHeight));
            float[] midflameWind = new float[windMag.Length];
            for (int i = 0; i < windMag.Length; i++)
            {
                midflameWind[i] = windMag[i] / conversionFactor;
                if (!isMetric)
                {
                    midflameWind[i] *= 1.6f;
                }
            }
            return midflameWind;
        }
        
    }
}