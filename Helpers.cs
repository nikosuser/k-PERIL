﻿/* 

<><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><>
     k-PERIL (Population Evacuation Trigger Algorithm)
                    Helper Functions
<><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><>

================================================================
CODE WRITTEN AND EDITED BY NIKOLAOS KALOGEROPOULOS
ORIGINALLY CREATED BY HARRY MITCHELL
================================================================

----------------------------------------------------------------
DEPARTMENT OF MECHANICAL ENGINEERING
IMPERIAL COLLEGE LONDON
----------------------------------------------------------------
These functions prepare the input data for k-PERIL and the associated 
fire models. Currently, those are Farsite and Flammap. Most of the 
methods here are parsing data from files to memory and vice versa,
writing information to console, or perform other calculations used 
often in the rest of the program.

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using k_PERIL_DLL;

using NetTopologySuite;
using NetTopologySuite.IO;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using OSGeo.GDAL;
using System.Reflection;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;
using System.Data;
using BitMiracle.LibTiff.Classic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
using NetTopologySuite.Index.Strtree;
using System.Text.RegularExpressions;
using System.Threading;
using DotSpatial.Data;
using MaxRev.Gdal.Core;
using NetTopologySuite.Noding;
using OSGeo.OGR;
using OSGeo.OSR;
using Feature = NetTopologySuite.Features.Feature;
using IFeature = NetTopologySuite.Features.IFeature;
using Shapefile = NetTopologySuite.IO.Shapefile;
using static NetTopologySuite.IO.Esri.Shapefile;
using GDAL_Driver = OSGeo.GDAL.Driver;
using OGR_Driver = OSGeo.GDAL.Driver;
using Geometry = NetTopologySuite.Geometries.Geometry;


namespace RoxCaseGen
{
    public class ModelSetup
    {
        int maxTemp;
        int minTemp;
        int maxTime;
        int minTime;
        int maxHumid;
        int minHumid;

        int varMaxTemp;
        int varMinTemp;
        int varMaxTime;
        int varMinTime;
        int varMaxHumid;
        int varMinHumid;

        int avgWindMag;
        int varAvgWindMag;

        public int totalRAWSdays;

        float actualMaxTemp;
        float actualMinTemp;
        float actualMaxTime;
        float actualMinTime;
        float actualMaxHumid;
        float actualMinHumid;

        public float actualASET;

        public float windMag;
        public int windDir;

        public float[] humidProfile;
        public float[] tempProfile;

        public string year;
        public string month;

        public int xIgnition_raster;
        public int yIgnition_raster;
        public int xIgnition_prj;
        public int yIgnition_prj;

        int IgnitionFuelModel;

        public int ASET;
        public int varASET;

        public int[,] fuelMap;

        public int burnDuration;
        
        public int[] fuelMoisture;

        public int cellsize;

        public List<PointF> WUI;

        public int[,] fmc;
        public int[,] fmc_A13;

        public void setValues(string Path)                                              //Reads the VARS.txt input file and passes the input variables to the class.
        {
            float[,] fileInput = parseInputFilesToMemory(Path + "/Input/VARS.txt");      //read the file contents and store each line in an array element

            //set all the class variables below
            this.maxTemp = (int)fileInput[0, 0];
            this.minTemp = (int)fileInput[1, 0];
            this.maxTime = (int)fileInput[2, 0];
            this.minTime = (int)fileInput[3, 0];
            this.maxHumid = (int)fileInput[4, 0];
            this.minHumid = (int)fileInput[5, 0];

            this.varMaxTemp = (int)fileInput[6, 0];
            this.varMinTemp = (int)fileInput[7, 0];
            this.varMaxTime = (int)fileInput[8, 0];
            this.varMinTime = (int)fileInput[9, 0];
            this.varMaxHumid = (int)fileInput[10, 0];
            this.varMinHumid = (int)fileInput[11, 0];

            this.avgWindMag = (int)fileInput[12, 0];
            this.varAvgWindMag = (int)fileInput[13, 0];

            this.totalRAWSdays = (int)fileInput[14, 0];
            this.ASET = (int)fileInput[fileInput.GetLength(0) - 2, 0];
            this.varASET = (int)fileInput[fileInput.GetLength(0) - 1, 0];
            //Console.WriteLine("Values Parsed!");
        }

        public void randomizeValues()                                               //create the set of simulation parameters
        {
            this.actualMaxTemp = getRandNormal(maxTemp, varMaxTemp);
            this.actualMinTemp = getRandNormal(minTemp, varMinTemp);
            this.actualMaxTime = (int)getRandNormal(maxTime, varMaxTime);
            this.actualMinTime = (int)getRandNormal(minTime, varMinTime);
            if (this.actualMinTime > 23) this.actualMinTime = 0;                    //Minimum time values default to zero if the random generator returned values of more than 24 or less than 0
            this.actualMaxHumid = getRandNormal(maxHumid, varMaxHumid);
            this.actualMinHumid = getRandNormal(minHumid, varMinHumid);

            while (this.actualMaxTemp < this.actualMinTemp)                         //make sure the Maximum temperature is larger than the minimum.
            {
                this.actualMaxTemp = getRandNormal(maxTemp, varMaxTemp);
                this.actualMinTemp = getRandNormal(minTemp, varMinTemp);
            }

            while (this.actualMaxHumid < this.actualMinHumid)                       //make sure the maximum humidity is larger than the minimum
            {
                this.actualMaxHumid = getRandNormal(maxHumid, varMaxHumid);
                this.actualMinHumid = getRandNormal(minHumid, varMinHumid);
            }

            this.windMag = getRandNormal(avgWindMag, varAvgWindMag);
            this.actualASET=getRandNormal(ASET, varASET);

            //Console.WriteLine("Input Values Ransomised!");
        }
        private float getRandNormal(float mean, float stdDev)                          //get random value based on standard deviation. This algorithm was shamelessly stolen from the internet, and is based on ____________ which gives you a random distribution using two completley random values.
        {
            Random rand = new Random(); //reuse this if you are generating many
            float u1 = (float)(1.0 - rand.NextDouble()); //uniform(0,1] random doubles
            float u2 = (float)(1.0 - rand.NextDouble());
            float randStdNormal = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2)); //random normal(0,1)
            float randNormal = mean + stdDev * randStdNormal; //random normal(mean,stdDev^2)
            while (randNormal < 0)                                                      //no negative value is expected in this program, so if the returned value is negative, the code runs again.
            {
                u1 = (float)(1.0 - rand.NextDouble()); //uniform(0,1] random doubles
                u2 = (float)(1.0 - rand.NextDouble());
                randStdNormal = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2)); //random normal(0,1)
                randNormal = mean + stdDev * randStdNormal; //random normal(mean,stdDev^2)
            }
            return randNormal;
        }
        public void createTempAndHumidProfile()                                 //extrapolate the diurnal temperature and humidity profiles.
        {
            int maxTime = (int)this.actualMaxTime;                              //get all the variables from the class (mostly because it is easier to write this way)
            int minTime = (int)this.actualMinTime;
            float maxTemp = this.actualMaxTemp;
            float minTemp = this.actualMinTemp;
            float maxHumid = this.actualMaxHumid;
            float minHumid = this.actualMinHumid;

            float[] tempProfile = new float[24];                                //declare output arrays of hourly temperature and humidity readings.
            float[] humidProfile = new float[24];

            //this code takes the max and min values, and extrapolates half-cosine waves between them. The wave between the highest and lowest temps is different to the one outside this range. Hence why the three parts of the below code: one part up to the first extreme, then between the two, then from the second to 2300.

            if (maxTime < minTime)                                              //if the coldest temp happens before midnight
            {
                for (int i = maxTime; i <= minTime; i++)
                {
                    tempProfile[i] = (float)(minTemp + (maxTemp - minTemp) * (0.5 + 0.5 * (float)Math.Cos((maxTime - i) * Math.PI / (maxTime - minTime))));                 //this equation is easier if you write it out.
                    humidProfile[i] = (float)(maxHumid - (maxHumid - minHumid) * (0.5 + 0.5 * (float)Math.Cos((maxTime - i) * Math.PI / (maxTime - minTime))));
                }
                for (int j = 0; j < maxTime; j++)
                {
                    tempProfile[j] = (float)(maxTemp - (maxTemp - minTemp) * (0.5 + 0.5 * (float)Math.Cos((j + 24 - minTime) * Math.PI / (24 + maxTime - minTime))));
                    humidProfile[j] = (float)(minHumid + (maxHumid - minHumid) * (0.5 + 0.5 * (float)Math.Cos((j + 24 - minTime) * Math.PI / (24 + maxTime - minTime))));
                }
                for (int k = minTime + 1; k < 24; k++)
                {
                    tempProfile[k] = (float)(maxTemp - (maxTemp - minTemp) * (0.5 + 0.5 * (float)Math.Cos((k - minTime) * Math.PI / (24 + maxTime - minTime))));
                    humidProfile[k] = (float)(minHumid + (maxHumid - minHumid) * (0.5 + 0.5 * (float)Math.Cos((k - minTime) * Math.PI / (24 + maxTime - minTime))));
                }
            }
            else                                                                //if the coldest temperature happens after midnight
            {
                for (int i = minTime; i < maxTime; i++)
                {
                    tempProfile[i] = (float)(minTemp + (maxTemp - minTemp) *
                        (0.5 + 0.5 * (float)Math.Cos((maxTime - i) * Math.PI / (maxTime - minTime))));
                    //tempProfile[i] = (float)(minTemp + (maxTemp - minTemp) * (0.5 + 0.5 * (float)Math.Cos((maxTime - i) * Math.PI / (maxTime - minTime))));
                    humidProfile[i] = (float)(maxHumid - (maxHumid - minHumid) * (0.5 + 0.5 * (float)Math.Cos((maxTime - i) * Math.PI / (maxTime - minTime))));
                }
                for (int j = 0; j < minTime; j++)
                {
                    tempProfile[j] = (float)(minTemp + (maxTemp - minTemp) *
                        (0.5 + 0.5 * (float)Math.Cos(Math.PI + (j - minTime) * Math.PI / (24 - maxTime + minTime))));
                    //tempProfile[j] = (float)(minTemp + (maxTemp - minTemp) * (0.5 + 0.5 * (float)Math.Cos((j + 24 - maxTime) * Math.PI / (24 + maxTime - minTime))));
                    humidProfile[j] = (float)(maxHumid - (maxHumid - minHumid) * (0.5 + 0.5 * (float)Math.Cos((j + 24 - maxTime) * Math.PI / (24 - maxTime + minTime))));
                }
                for (int k = maxTime; k < 24; k++)
                {
                    tempProfile[k] = (float)(minTemp + (maxTemp - minTemp) * (0.5 + 0.5 *
                        (float)Math.Cos(Math.PI + (k - 24 - minTime) * Math.PI / (24 - maxTime + minTime))));
                    //tempProfile[k] = (float)(maxTemp - (maxTemp - minTemp) * (0.5 + 0.5 * (float)Math.Cos((k - minTime) * Math.PI / (24 - maxTime + minTime))));
                    humidProfile[k] = (float)(maxHumid - (maxHumid - minHumid) * (0.5 + 0.5 * (float)Math.Cos((k - maxTime) * Math.PI / (24 - maxTime + minTime))));
                }
            }

            for (int k = 0; k < tempProfile.Length; k++)                //make sure any negative values are defaulted to zero to avoid errors. this should never occur but the above code does not work perfectly yet, this is to eliminate crash issues with farsite.
            {
                if (tempProfile[k] < 0) { tempProfile[k] = 0; }
                if (humidProfile[k] < 0) { humidProfile[k] = 0; }
            }


            this.tempProfile = tempProfile;                             //parse variables to class
            this.humidProfile = humidProfile;

            //Console.WriteLine("Temperature and Humidity Profiles Created!");
        }
        public void createAndWriteFileFARSITE(string Path)                     //create the input file required by the FARSITE console application.
        {
            List<string> outputFile = new List<string>(); //[15 + 256 + this.totalRAWSdays * 24 + 20];           //instantiate the output array
            this.year = "2022";                                                                     //date of starting data, doesnt really matter.
            this.month = "9";
            outputFile.Add($"CONDITIONING_PERIOD_END: {this.month} {this.totalRAWSdays} 2300");
            outputFile.Add($"FUEL_MOISTURES_DATA: {this.fmc.GetLength(0)+1}");                                             //all the fuel moistures are the same and hardcoded below for now.
            outputFile.Add("0 6 7 8 60 90");
            for (int i = 0; i < this.fmc.GetLength(0); i++)
            {
                outputFile.Add($"{this.fmc[i,0]} {this.fmc[i,1]} {this.fmc[i,2]} {this.fmc[i,3]} {this.fmc[i,4]} {this.fmc[i,5]}");
            }
            outputFile.Add("RAWS_ELEVATION: 10");                                               //random value added for now. Maybe it is too high?
            outputFile.Add("RAWS_UNITS: Metric");                                                 //All values declared metric
            outputFile.Add($"RAWS: {(this.totalRAWSdays * 24).ToString()}");                   //Calculate and declare how many weather points will follow
            for (int i = 0; i < this.totalRAWSdays; i++)                                          //Output all the weather data in FARSITE flavor. Precipitation is defaulted to 20%
            {
                for (int j = 0; j < 24; j++)
                {
                    outputFile.Add($"{year} {month} {i + 1} {j * 100} {(int)this.tempProfile[j]} {(int)this.humidProfile[j]} 0.00 {(int)this.windMag} {this.windDir} 0");
                }
            }
            //THe FARSITE simulations will happen with the below parameters. They are currently hardcoded. Notice how GRIDDED WIND is being used here.
            outputFile.Add("FOLIAR_MOISTURE_CONTENT: 60");
            outputFile.Add("CROWN_FIRE_METHOD: Finney");
            outputFile.Add("NUMBER_PROCESSORS: 32");
            outputFile.Add("GRIDDED_WINDS_GENERATE: Yes");
            outputFile.Add("GRIDDED_WINDS_RESOLUTION: 10.0");
            outputFile.Add($"FARSITE_START_TIME: {this.month} {this.totalRAWSdays-this.burnDuration/24} {23-this.burnDuration%24}00"); 
            outputFile.Add($"FARSITE_END_TIME: {this.month} {this.totalRAWSdays} 2300");
            outputFile.Add("FARSITE_TIMESTEP: 60");
            outputFile.Add("FARSITE_DISTANCE_RES: 60.0");
            outputFile.Add("FARSITE_PERIMETER_RES: 60.0");
            outputFile.Add("FARSITE_MIN_IGNITION_VERTEX_DISTANCE: 15.0");
            outputFile.Add("FARSITE_SPOT_GRID_RESOLUTION: 30.0");
            outputFile.Add("FARSITE_SPOT_PROBABILITY: 0.05");
            outputFile.Add("FARSITE_SPOT_IGNITION_DELAY: 0");
            outputFile.Add("FARSITE_MINIMUM_SPOT_DISTANCE: 60");
            outputFile.Add("FARSITE_ACCELERATION_ON: 1");

            File.WriteAllLines(Path + "ROX.input", outputFile.ToArray());

            //Console.WriteLine("Input File Created!");
        }

        public void createAndWriteFileFLAMMAP(string Path)                     //create the input file required by the FARSITE console application.
        {
            List<string> outputFile = new List<string>(); //[15 + 256 + this.totalRAWSdays * 24 + 20];           //instantiate the output array
            this.year = "2022";                                                                     //date of starting data, doesnt really matter.
            this.month = "6";

            outputFile.Add($"CONDITIONING_PERIOD_END: {this.month} {this.totalRAWSdays} 1600");

            outputFile.Add("FUEL_MOISTURES_DATA: 255");                                             //all the fuel moistures are the same and hardcoded below for now.
            for (int i = 1; i < 256; i++)
            {
                outputFile.Add((i - 1).ToString() + $" {this.fuelMoisture[0]} {this.fuelMoisture[1]} {this.fuelMoisture[2]} {this.fuelMoisture[3]} {this.fuelMoisture[4]}");
            }
            outputFile.Add("RAWS_ELEVATION: 10");                                               
            outputFile.Add("RAWS_UNITS: Metric"); 
            outputFile.Add("WIND_SPEED_UNITS: 1"); //All values declared metric
            outputFile.Add($"RAWS: {(this.totalRAWSdays * 24).ToString()}");                   //Calculate and declare how many weather points will follow
            for (int i = 0; i < this.totalRAWSdays; i++)                                          //Output all the weather data in FARSITE flavor. Precipitation is defaulted to 20%
            {
                for (int j = 0; j < 24; j++)
                {
                    outputFile.Add($"{year} {month} {i + 1} {j * 100} {(int)this.tempProfile[j]} {(int)this.humidProfile[j]} 0.00 {(int)this.windMag} {this.windDir} 0");
                }
            }
            //THe FARSITE simulations will happen with the below parameters. They are currently hardcoded. Notice how GRIDDED WIND is being used here.
            outputFile.Add("FOLIAR_MOISTURE_CONTENT: 80");
            outputFile.Add("CROWN_FIRE_METHOD: Finney");
            outputFile.Add("NUMBER_PROCESSORS: 16");
            outputFile.Add($"WIND_SPEED: {this.windMag}");
            outputFile.Add($"WIND_DIRECTION: {this.windDir}");
            outputFile.Add("SPREAD_DIRECTION_FROM_MAX: 0");
            outputFile.Add("GRIDDED_WINDS_GENERATE: Yes");
            outputFile.Add("GRIDDED_WINDS_RESOLUTION: 30.0");

            outputFile.Add("SPREADRATE:");
            outputFile.Add("MAXSPREADDIR:");
            outputFile.Add("SOLARRADIATION:");
            outputFile.Add("FUELMOISTURE1:");
            outputFile.Add("FUELMOISTURE10:");
            outputFile.Add("FUELMOISTURE100:");
            outputFile.Add("FUELMOISTURE1000:");

            File.WriteAllLines(Path, outputFile.ToArray());

            //Console.WriteLine("Input File Created!");
        }
        public static int[,] readASC_int(string path)              //Read FARSITE output files, delete the first 6 lines as they do not contain useful information (for this program)
        {
            float[,] floatOutput = readASC_float(path);
            int[,] intOutput = new int[floatOutput.GetLength(0), floatOutput.GetLength(0)];
            
            for (int i = 0; i < floatOutput.GetLength(0); i++)
            {
                for (int j = 0; j < floatOutput.GetLength(1); j++)
                {
                    intOutput[i, j] = (int)floatOutput[i, j]; // Explicit cast from float to int
                }
            }
            
            return intOutput; //return the output matrix
        }
        
        public static float[,] readASC_float(string path)              //Read FARSITE output files, delete the first 6 lines as they do not contain useful information (for this program)
        {
            string[] FileRawInput = File.ReadAllLines(path);
            List<string> ParsedInput = FileRawInput.ToList();
            ParsedInput.RemoveRange(0, 6);
            FileRawInput = ParsedInput.ToArray();
 
            return parseFile(FileRawInput); //return the output matrix
        }

        public float[,] readSafetyMatrix_float(string path)              //Read FARSITE output files, delete the first 6 lines as they do not contain useful information (for this program)
        {
            string[] FileRawInput = File.ReadAllLines(path);
            List<string> ParsedInput = FileRawInput.ToList();
            FileRawInput = ParsedInput.ToArray();

            return parseFile(FileRawInput);                                                //return the output matrix
        }

        private static float[,] parseFile(string[] input)                          //This code is also shamelessly stolen from the internet to convert a raster from a string array format (as is done by reading a file) to a 2D numeric array..
        {
            var arrays = new List<float[]>();

            foreach (var line in input)                                     //for each line variable in the list(?) lines
            {
                var lineArray = new List<float>();                          //make a new list of floats
                foreach (string s in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))   //for each variable s in the input array
                {
                    string temp = "";
                    if (s.StartsWith("[") && s.EndsWith("]"))
                    {
                        temp = s.Substring(1, s.Length - 2); // Remove the first and last characters
                    }
                    else
                    {
                        temp = s;
                    }
                    lineArray.Add(float.Parse(temp, System.Globalization.NumberStyles.Float)); //add the element to the list of floats created before the loop
                }
                arrays.Add(lineArray.ToArray());                            //add the lineArray array to the general arrays list
            }
            var numberOfRows = input.Count();                               //save the number of rows of the parsed and edited data in lines
            var numberOfValues = arrays.Sum(s => s.Length);                 //save the total number of values in arrays (i.e. all elements)

            int minorLength = arrays[0].Length;                             //create a variable to save the minor dimension of the arrays variable
            float[,] NumOut = new float[arrays.Count, minorLength];         //create output matrix
            for (int i = 0; i < arrays.Count; i++)                          //for all elements in arrays
            {
                var array = arrays[i];                                      //save the array in the arrays element
                for (int j = 0; j < minorLength; j++)                       //for the length of the minor array
                {
                    try
                    {
                        NumOut[i, j] = array[j];
                    }
                    catch
                    {
                        NumOut[i, j] = 0;
                    }                         //place each element in the output matrix
                }
            }
            return NumOut;
        }

        public float[,] parseInputFilesToMemory(string filePath)                //Read input files that are not farsite data (so no need to delete the first 6 lines)
        {
            List<string> lines = new List<string>();

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Find the position of '%' character
                    int commentIndex = line.IndexOf('%');

                    // Extract the substring before '%' or the entire line if '%' is not found
                    string content = commentIndex != -1 ? line.Substring(0, commentIndex) : line;

                    // Add the content to the list
                    lines.Add(content);
                }
            }

            // Convert the list to an array
            string[] linesArray = lines.ToArray();

            //string[] FileRawInput = File.ReadAllLines(path);
            //List<string> ParsedInput = FileRawInput.ToList();
            //FileRawInput = ParsedInput.ToArray();

            return parseFile(linesArray);                                                  //return the output matrix
        }

        public static void cleanupIters(string directoryPath)
        {
            // Pattern to match folders in the form IterXXX
            string folderPattern = @"^Iter\d{3}$";

            try
            {
                // Get all subdirectories in the specified directory
                var directories = Directory.GetDirectories(directoryPath);

                foreach (var dir in directories)
                {
                    // Get the folder name (without the full path)
                    string folderName = Path.GetFileName(dir);

                    // Check if the folder name matches the pattern
                    if (Regex.IsMatch(folderName, folderPattern))
                    {
                        // Delete the folder and its contents
                        Directory.Delete(dir, true); // true = delete contents recursively
                        Console.WriteLine($"Deleted folder: {folderName}");
                    }
                }

                Console.WriteLine("Cleanup completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        public static void Copy(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach(var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)),true);

            foreach(var directory in Directory.GetDirectories(sourceDir))
                Copy(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
        }
        public static void Output_ASC_File(string[] header, float[,] boundary, string PerilOutput)      //output variable to a new text file, shamelessly stolen
        {
            using (var sw = new StreamWriter(PerilOutput))  //beyond here the code has been shamelessly stolen
            {
                for (int i = 0; i < header.Length; i++)
                {
                    sw.WriteLine(header[i]);
                }
                for (int i = 0; i < boundary.GetLength(0); i++)   //for all elements in the output array
                {
                    for (int j = 0; j < boundary.GetLength(1); j++)
                    {
                        sw.Write(boundary[i, j] + " ");       //write the element in the file
                    }
                    sw.Write("\n");
                }
                sw.Flush();                                 //i dont really know
                sw.Close();                                 //close opened output text file
            }
        }
        public static void Output_ASC_File(string[] header, int[,] boundary, string PerilOutput)      //output variable to a new text file, shamelessly stolen
        {
            using (var sw = new StreamWriter(PerilOutput))  //beyond here the code has been shamelessly stolen
            {
                for (int i = 0; i < header.Length; i++)
                {
                    sw.WriteLine(header[i]);
                }
                for (int i = 0; i < boundary.GetLength(0); i++)   //for all elements in the output array
                {
                    for (int j = 0; j < boundary.GetLength(1); j++)
                    {
                        int intOut = (boundary[i,j]<=0) ? -9999 : boundary[i, j];
                        sw.Write(intOut + " ");       //write the element in the file
                    }
                    sw.Write("\n");
                }
                sw.Flush();                                 //i dont really know
                sw.Close();                                 //close opened output text file
            }
        }

        public void OutputColumnFile(int[,] boundary, string PerilOutput)      //output variable to a new text file, shamelessly stolen
        {
            using (var sw = new StreamWriter(PerilOutput))  //beyond here the code has been shamelessly stolen
            {
                for (int i = 0; i < boundary.GetLength(0); i++)   //for all elements in the output array
                {
                    for (int j = 0; j < 2; j++)
                    {
                        sw.Write(boundary[i, j] + " ");       //write the element in the file
                    }
                    sw.Write("\n");
                }
                sw.Flush();                                 //i dont really know
                sw.Close();                                 //close opened output text file
            }
        }

        static private float PointDistance(int x1, int y1, int x2, int y2)
        {
            // Calculate the distance between (x1, y1) and (x2, y2) using the Euclidean distance formula
            float deltaX = x1 - x2;
            float deltaY = y1 - y2;

            return (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public int[] SelectIgnitionPoint(string fuelMapPath, bool randWind)                  //Select ignition point based on target ignition area.
        {
            Random rand = new Random();                                     //create new random

            int ncols = 0;
            int nrows = 0;
            float xllcorner = 0;
            float yllcorner = 0;
            float cellsize = 0;

            try
            {
                using (StreamReader reader = new StreamReader(fuelMapPath + "fuel.asc"))
                {
                    // Read the first five lines
                    for (int i = 0; i < 5; i++)
                    {
                        string line = reader.ReadLine();
                        // Use regular expression to split the line into key and value
                        Match match = Regex.Match(line, @"(\S+)\s+(\S+)");
                        if (match.Success)
                        {
                            string key = match.Groups[1].Value.ToLower();
                            float value = float.Parse(match.Groups[2].Value);

                            // Assign values based on the key
                            switch (key)
                            {
                                case "ncols":
                                    ncols = (int)value;
                                    break;
                                case "nrows":
                                    nrows = (int)value;
                                    break;
                                case "xllcorner":
                                    xllcorner = value;
                                    break;
                                case "yllcorner":
                                    yllcorner = value;
                                    break;
                                case "cellsize":
                                    cellsize = value;
                                    break;
                                default:
                                    Console.WriteLine($"Unknown key: {key}");
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Line format not recognized: {line}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading header: {ex.Message}");
            }

            int maxXcoord = (int)(xllcorner+cellsize*ncols);                                        //These are the maximum and minimum coordinates of the raster. I do not know what format they are or how to extract them from the LCP file yet. I will do more reserach to automatically get them. For now they are hardcoded.
            int minXcoord = (int)xllcorner;
            int maxYcoord = (int)(yllcorner+cellsize*nrows);
            int minYcoord = (int)yllcorner;
            
            PointF WUIcenter = GetCentroid(this.WUI);

            this.xIgnition_raster = (int)((rand.NextDouble()*0.8+0.1) * ncols);        //get random ignition location
            this.yIgnition_raster = (int)((rand.NextDouble()*0.8+0.1) * nrows);         //avoid the absolute boundaries of the landscape

            this.IgnitionFuelModel = (int)this.fuelMap[this.yIgnition_raster, this.xIgnition_raster];      //find the fuel inside the ignition point

            while (this.IgnitionFuelModel == -9999 || this.IgnitionFuelModel == 91 || this.IgnitionFuelModel == 92 || this.IgnitionFuelModel == 93 || this.IgnitionFuelModel == 98 || this.IgnitionFuelModel == 99 || PointDistance(this.xIgnition_raster,this.yIgnition_raster,(int)WUIcenter.X, (int)WUIcenter.Y) < Math.Min(ncols / 3, nrows / 3) ) //if the ignition point has nonfuel on it, or is outside the designated area, retry
            {
                this.xIgnition_raster = (int)((rand.NextDouble()*0.8+0.1) * ncols);
                this.yIgnition_raster = (int)((rand.NextDouble()*0.8+0.1) * nrows);
                this.IgnitionFuelModel = (int)this.fuelMap[this.yIgnition_raster, this.xIgnition_raster];
            }

            double[] proportionalIgnitionPoint = new double[2] { (float)this.xIgnition_raster / (float)ncols, (float)this.yIgnition_raster / (float)nrows };  //convert from raster values to proportional/nondimensional values (0-1 progress from the origin)
            
            if (randWind)
            {
                this.windDir = (int)(rand.NextDouble() * 360);
            }
            else
            {
                this.windDir = (90 + (int)((180 / Math.PI) * Math.Atan2((this.yIgnition_raster - (int)WUIcenter.Y), (this.xIgnition_raster - (int)WUIcenter.X))))%360;   //calculate the wind direction to point towards the center of the raster. 
                if (this.windDir < 0){windDir += 360;}
            }

            this.xIgnition_prj = (int)(minXcoord + (maxXcoord - minXcoord) * proportionalIgnitionPoint[0]);
            this.yIgnition_prj = (int)(maxYcoord - (maxYcoord - minYcoord) * proportionalIgnitionPoint[1]);
            
            return [this.xIgnition_prj, this.yIgnition_prj];
        }
        public void saveUsedData(string Path)                   //dump the used values of each case to a log file.
        {
            string output = $"{this.windMag} {this.windDir} {this.actualMaxHumid} {this.actualMaxTemp} {this.actualMaxTime} {this.actualMinHumid} {this.actualMinTemp} {this.actualMinTime} {this.xIgnition_raster} {this.yIgnition_raster} {this.IgnitionFuelModel} {this.actualASET}";        //dump all the values in a string
            string path = Path + "/log.txt";
            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine("windMag windDir maxHumid maxTemp maxTime minHumid minTemp minTime xIgnition_raster yIgnition_raster IgnitionFuel ASET");     //if the file does not exist, create it. The write the header.
                }
            }
            Console.WriteLine("ITERATION VARIABLES:");
            Console.WriteLine($"Wind Magnitude: {this.windMag:0.0#} km/h");
            Console.WriteLine($"Wind Direction: {this.windDir:0.0#} from the vertical counterclockwise (because flammap?)");
            Console.WriteLine($"Maximum Humidity: {this.actualMaxHumid:0.0#} %");
            Console.WriteLine($"Maximum Temperature: {this.actualMaxTemp:0.0#} oC");
            Console.WriteLine($"Time of Max Temperature: {this.actualMaxTime:0.0#} HH");
            Console.WriteLine($"Minimum Humidity: {this.actualMinHumid:0.0#} %");
            Console.WriteLine($"Minimum Temperature: {this.actualMinTemp:0.0#} oC");
            Console.WriteLine($"Time of Min Temperature: {this.actualMinTime:0.0#} HH");
            Console.WriteLine($"Ignition X-coordinate: {this.xIgnition_raster:0.0#} ");
            Console.WriteLine($"Ignition Y-Coordinate: {this.yIgnition_raster:0.0#} ");
            Console.WriteLine($"Ignition Fuel Model: {this.IgnitionFuelModel:0.0#} ");
            Console.WriteLine($"ASET: {this.actualASET:0.0#} min");
            Console.WriteLine("---------------------------------------");
            using (StreamWriter sw = File.AppendText(path))
            {
                sw.WriteLine(output);
                //Console.WriteLine(output);                      //dump the values in the file.
            }
            //Console.WriteLine("Log File Created!");
        }
        public void WriteStarterFileFARSITE(string Path)               //Write a file that is needed for FARSITE to start. Should not be changed.
        {
            string output = $"{Path}Input/INPUT.lcp {Path}ROX.input {Path}ROX.shp 0 {Path}Median_Outputs/ 1";
            string path = Path + "/Farsite/ROX.txt";
            if (File.Exists(path)) { File.Delete(path); }
            using (StreamWriter sw = File.CreateText(path))
            {
                sw.WriteLine(output);
            }
        }

        public void WriteStarterFileFLAMMAP(string Path)               //Write a file that is needed for FARSITE to start. Should not be changed.
        {
            string output = $"{Path}Input/INPUT.lcp {Path}ROX.input {Path}Median_Outputs/ 0";
            string path = Path + "/ROX.txt";
            if (File.Exists(path)) { File.Delete(path); }
            using (StreamWriter sw = File.CreateText(path))
            {
                sw.WriteLine(output);
            }
        }
        public float demo_CalcConvergence(int currentIteration, string Path)
        {
            float[,] previousOutput = readASC_float(Path + $"/Output/ProbabilisticTriggerBoundary.asc");
            float[,] currentSF = readASC_float(Path + $"/Output/TriggerBoundary_{currentIteration}.asc");

            float[,] currentOutput = new float[currentSF.GetLength(0), currentSF.GetLength(1)];

            for (int i = 0;i<currentSF.GetLength(0); i++)
            {
                for (int j = 0; j<currentSF.GetLength(1); j++)
                {
                    if (previousOutput[i, j] < 0){previousOutput[i, j] = 0;}
                    if (currentSF[i, j] < 0){currentSF[i, j] = 0;}
                    currentOutput[i, j] = previousOutput[i, j] + currentSF[i, j];
                }
            }

            // Initialize max value with the smallest possible float value
            float maxPrev = previousOutput.Cast<float>().Max();
            float maxCurr = currentOutput.Cast<float>().Max();

            float maxError = 0;
            int checkIntervals = 5;
            for (int interval = 0; interval < checkIntervals; interval++)
            {
                float thresholdPrev = interval * maxPrev / checkIntervals;
                float thresholdCurr = interval * maxCurr / checkIntervals;

                int binaryPrev = previousOutput.Cast<float>().Count(value => value > thresholdPrev);
                int binaryCurr = currentOutput.Cast<float>().Count(value => value > thresholdCurr);
                float error = Math.Abs((float)(binaryCurr - binaryPrev)/binaryPrev);
                if (error>maxError) { maxError = error; }
            }

            return maxError;
        }
        public float CalcConvergence(int currentIteration, string Path, string model)
        {
            float[,] previousOutput = readASC_float(Path + $"Outputs/SafetyMatrix_{model}.txt");
            float[,] currentSF = readASC_float(Path + $"Outputs/SafetyMatrix_{model}_" + currentIteration.ToString() + ".txt");

            float[,] currentOutput = new float[currentSF.GetLength(0), currentSF.GetLength(1)];

            for (int i = 0;i<currentSF.GetLength(0); i++)
            {
                for (int j = 0; j<currentSF.GetLength(1); j++)
                {
                    currentOutput[i, j] = previousOutput[i, j] + currentSF[i, j];
                }
            }

            // Initialize max value with the smallest possible float value
            float maxPrev = previousOutput.Cast<float>().Max();
            float maxCurr = currentOutput.Cast<float>().Max();

            float maxError = 0;

            for (int interval = 0; interval < 10; interval++)
            {
                float thresholdPrev = interval * maxPrev / 10;
                float thresholdCurr = interval * maxCurr / 10;

                int binaryPrev = previousOutput.Cast<float>().Count(value => value > thresholdPrev);
                int binaryCurr = currentOutput.Cast<float>().Count(value => value > thresholdCurr);
                float error = Math.Abs((float)(binaryCurr - binaryPrev)/binaryPrev);
                if (error>maxError) { maxError = error; }
            }

            return maxError;
        }
        public static T[,] GetSlice<T>(T[,,] array, int dim1Index)
        {
            int dim2 = array.GetLength(1);
            int dim3 = array.GetLength(2);
            T[,] slice = new T[dim2, dim3];

            for (int j = 0; j < dim2; j++)
            {
                for (int k = 0; k < dim3; k++)
                {
                    slice[j, k] = array[dim1Index, j, k];
                }
            }

            return slice;
        }
        public static double[] convertCoords(double utmX, double utmY, int utmZone, bool isNorthernHemisphere)
        {
            var utm = ProjectedCoordinateSystem.WGS84_UTM(utmZone, isNorthernHemisphere);
            var geographic = GeographicCoordinateSystem.WGS84;

            // Create the transformation
            var transformFactory = new CoordinateTransformationFactory();
            var transform = transformFactory.CreateFromCoordinateSystems(utm, geographic);

            double[] wgs84 = transform.MathTransform.Transform(new[] { utmX, utmY });

            return wgs84;
        }

        public static float getElevation(int x, int y, string path)
        {
            float[,] elevation = ModelSetup.readASC_float(path);
            return elevation[x, y];
        }

        public static string[] getHeader(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file {filePath} does not exist.");

            string[] headerLines = new string[6];
            using (StreamReader reader = new StreamReader(filePath))
            {
                for (int i = 0; i < 6; i++)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                        throw new FormatException($"Unexpected end of file while reading the header at line {i + 1}.");
                
                    headerLines[i] = line.Trim(); // Store the entire line in the array
                }
            }
            return headerLines;
        }
        public static float[,] readTiff(string filePath)
        {
            GdalBase.ConfigureAll();
            Gdal.AllRegister();

            // Open the GeoTIFF file
            Dataset dataset = Gdal.Open(filePath, Access.GA_ReadOnly);

            // Get raster dimensions
            int width = dataset.RasterXSize;
            int height = dataset.RasterYSize;
            int bandCount = dataset.RasterCount;

            Console.WriteLine($"Raster Dimensions: {width} x {height}");
            Console.WriteLine($"Number of Bands: {bandCount}");

            // Read the first band (assuming a single-band GeoTIFF for simplicity)
            Band band = dataset.GetRasterBand(1);

            // Allocate memory for the raster data
            float[] rasterData = new float[width * height];

            // Read the raster band data into the array
            band.ReadRaster(0, 0, width, height, rasterData, width, height, 0, 0);

            // Optionally, reshape the flat array into a 2D matrix (height x width)
            float[,] matrix = new float[height, width];
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    matrix[row, col] = rasterData[row * width + col];
                }
            }
            // Clean up
            dataset.Dispose();
            return matrix;
        }
        public static PointF GetCentroid(List<PointF> poly)
        {
            float accumulatedArea = 0.0f;
            float centerX = 0.0f;
            float centerY = 0.0f;

            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                float temp = poly[i].X * poly[j].Y - poly[j].X * poly[i].Y;
                accumulatedArea += temp;
                centerX += (poly[i].X + poly[j].X) * temp;
                centerY += (poly[i].Y + poly[j].Y) * temp;
            }

            if (Math.Abs(accumulatedArea) < 1E-7f)
                return PointF.Empty;  // Avoid division by zero

            accumulatedArea *= 3f;
            return new PointF(centerX / accumulatedArea, centerY / accumulatedArea);
        }

        public static void prepareNextIteration(string path)
        {
            string[] prevResultFolders =
            {
                @"\ELMFIRE\Output\",
                @"\ELMFIRE\Median_output",
                @"\ELMFIRE\Input",
                @"\EPD\Input",
                @"\LSTM\Input",
                @"\Farsite\Median_Outputs",
                @"\Farsite\Input",
                @"\WISE\Output",
                @"\WISE\Input",
                @"/FDS LS1\Input",
                @"\FDS LS4\Input",
            };
            foreach (string folder in prevResultFolders)
            {
                string[] filePaths = Directory.GetFiles(path + folder);
                foreach (string filePath in filePaths)
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
            }
            foreach (var javaProc in Process.GetProcessesByName("java"))
            {
                javaProc.Kill();
            }
        }
        public static (double mag, double dir) CalculateGradient(float[,] matrix, int row, int col, float cellsize)
        {
            int numRows = matrix.GetLength(0);
            int numCols = matrix.GetLength(1);

            // Ensure the point is within the bounds of the matrix
            if (row <= 0 || row >= numRows - 1 || col <= 0 || col >= numCols - 1)
                throw new ArgumentException("The point must not be on the boundary of the matrix.");

            // Compute partial derivatives using central differences
            double dx = matrix[row, col + 1] - matrix[row, col - 1]; // Horizontal
            double dy = matrix[row + 1, col] - matrix[row - 1, col]; // Vertical

            double magnitude = (2 * cellsize) / Math.Sqrt(dx * dx + dy * dy);

            // Calculate direction (in degrees)
            double thetaHorizontal = ((Math.Atan2(dy, dx) * (180.0 / Math.PI)) - 90) % 360;
            double direction = 180 + thetaHorizontal;
            if (direction < 0) direction += 360;

            return (magnitude, direction);
        }
        /// <summary>
        /// Transposes a 2D matrix.
        /// </summary>
        /// <param name="matrix">The input matrix to transpose.</param>
        /// <returns>A new matrix that is the transpose of the input matrix.</returns>
        public static float[,] TransposeMatrix(float[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            float[,] transposed = new float[cols, rows];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    transposed[j, i] = matrix[i, j];
                }
            }

            return transposed;
        }
        
        public static int[,] TransposeMatrix(int[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            int[,] transposed = new int[cols, rows];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    transposed[j, i] = matrix[i, j];
                }
            }

            return transposed;
        }

        public int[] InitialiseIterationAndGetIgnition(string path)
        {
            int[] coordinates = SelectIgnitionPoint(path + "/Farsite/Input/", false); //choose ignition coordinates
            randomizeValues(); //randomise the weather and wind values
            saveUsedData(path); //dump the selected variables in log.txt
            createTempAndHumidProfile(); //extrapolate diurnal temperature and humidity profiles
            return coordinates;
        }

        public void GetFMC(string path)
        {
            createAndWriteFileFLAMMAP(path + "/Farsite/inputFLAMMAP.input");
            RunModels.runCommand([$"cd '{path}Farsite'", $"./setenv.bat", "./bin/testFLAMMAP.exe FLAMMAP.txt"],
                @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
            
            while(!File.Exists(path + "Farsite/Median_Outputs/FLAMMAP_FUELMOISTURE1.asc")){Thread.Sleep(200);}

            int[] fuelsPresent = this.fuelMap
                .Cast<int>()
                .Where(fuel => fuel != -9999) // exclude -9999
                .Distinct()
                .ToArray();
            float[,]fuelMoistures = new float[fuelsPresent.Length,4];

            int[,] fuelmap13 = readASC_int(path + "Input/FDS LS1/Input/fuel13.asc");
            int[] fuelsPresent13 = fuelmap13
                .Cast<int>()
                .Where(fuel => fuel != -9999) // exclude -9999
                .Distinct()
                .ToArray();
            float[,]fuelMoistures13 = new float[fuelsPresent13.Length,4];
            
            float[,] hour1fmc = readASC_float(path + @"Farsite/Median_Outputs/FLAMMAP_FUELMOISTURE1.asc");
            float[,] hour10fmc = readASC_float(path + @"Farsite/Median_Outputs/FLAMMAP_FUELMOISTURE10.asc");
            float[,] hour100fmc = readASC_float(path + @"Farsite/Median_Outputs/FLAMMAP_FUELMOISTURE100.asc");

            for (int i = 0; i < this.fuelMap.GetLength(0); i++)
            {
                for (int j = 0; j < this.fuelMap.GetLength(1); j++)
                {
                    if (hour1fmc[i, j] > 0 && hour10fmc[i, j] > 0 && hour100fmc[i, j] > 0 && fuelmap13[i,j] > 0)
                    {
                        int loc = Array.IndexOf(fuelsPresent,this.fuelMap[i, j]);
                        fuelMoistures[loc,0] ++;
                        fuelMoistures[loc,1] += hour1fmc[i,j];
                        fuelMoistures[loc,2] += hour10fmc[i,j];
                        fuelMoistures[loc,3] += hour100fmc[i,j];
                    
                        int loc2 = Array.IndexOf(fuelsPresent13,fuelmap13[i, j]);
                        fuelMoistures13[loc2,0] ++;
                        fuelMoistures13[loc2,1] += hour1fmc[i,j];
                        fuelMoistures13[loc2,2] += hour10fmc[i,j];
                        fuelMoistures13[loc2,3] += hour100fmc[i,j];
                    } 
                }
            }
            
            int[,] fmcOut = new int [fuelsPresent.Length,6];
            for (int i = 0; i < fmcOut.GetLength(0); i++)
            {
                fmcOut[i, 0] = fuelsPresent[i];
                fmcOut[i, 1] = (int)(100 * fuelMoistures[i, 1] / fuelMoistures[i, 0]);
                fmcOut[i, 2] = (int)(100 * fuelMoistures[i, 2] / fuelMoistures[i, 0]);
                fmcOut[i, 3] = (int)(100 * fuelMoistures[i, 3] / fuelMoistures[i, 0]);
                fmcOut[i, 4] = 60;
                fmcOut[i, 5] = 90;
            }
            //remmeber to alter the FMC creation function
            
            this.fmc = fmcOut;
            
            int[,] fmcOut13 = new int [fuelsPresent.Length,6];
            for (int i = 0; i < fmcOut.GetLength(0); i++)
            {
                fmcOut13[i, 0] = fuelsPresent13[i];
                fmcOut13[i, 1] = (int)(100 * fuelMoistures13[i, 1] / fuelMoistures13[i, 0]);
                fmcOut13[i, 2] = (int)(100 * fuelMoistures13[i, 2] / fuelMoistures13[i, 0]);
                fmcOut13[i, 3] = (int)(100 * fuelMoistures13[i, 3] / fuelMoistures13[i, 0]);
                fmcOut13[i, 4] = 60;
                fmcOut13[i, 5] = 90;
            }
            //remmeber to alter the FMC creation function
            
            this.fmc_A13 = fmcOut13;
        }
        
        public static void CreateMultiBandGeoTiff(
        float[,] rasterData2D,    // or short[,] if using Int16
        int numberOfBands,
        string outputFilename,
        string sampleGeoTiff,
        DataType gdalDataType)
        {
            // 1. Open the "sample" GeoTIFF to get CRS and GeoTransform
            using (Dataset sampleDs = Gdal.Open(sampleGeoTiff, Access.GA_ReadOnly))
            {
                if (sampleDs == null)
                {
                    throw new Exception($"Could not open sample GeoTIFF: {sampleGeoTiff}");
                }

                // Get projection (CRS) in WKT
                string wktProjection = sampleDs.GetProjection();

                // Get GeoTransform
                double[] sampleGeoTransform = new double[6];
                sampleDs.GetGeoTransform(sampleGeoTransform);
            }

            // At this point, sampleDs is out of scope if we used 'using(...)'.
            // We can re-open or store the relevant info outside the using block:
            double[] geoTransform;
            string projWkt;

            // Let's store them before disposing sampleDs:
            {
                // We'll open again (or do the logic in a single block):
                using (var sampleDs = Gdal.Open(sampleGeoTiff, Access.GA_ReadOnly))
                {
                    geoTransform = new double[6];
                    sampleDs.GetGeoTransform(geoTransform);
                    projWkt = sampleDs.GetProjection();
                }
            }

            // 2. Retrieve the dimensions from the 2D array
            int height = rasterData2D.GetLength(0);
            int width  = rasterData2D.GetLength(1);

            // 3. Prepare to create the new GeoTIFF
            GDAL_Driver gtiffDriver = Gdal.GetDriverByName("GTiff");
            if (gtiffDriver == null)
            {
                throw new Exception("Could not get GTiff driver. Is GDAL properly installed?");
            }

            // Example creation options (optional)
            string[] creationOptions = new string[]
            {
                "COMPRESS=LZW",
                "TILED=YES"
            };

            // 4. Create the output dataset
            using (Dataset outDs = gtiffDriver.Create(
                outputFilename,
                width,
                height,
                numberOfBands,
                gdalDataType,
                creationOptions))
            {
                if (outDs == null)
                {
                    throw new Exception("Could not create output GeoTIFF dataset.");
                }

                // Set projection and geo-transform from the sample
                outDs.SetProjection(projWkt);
                outDs.SetGeoTransform(geoTransform);

                // 5. Flatten the 2D array for writing to each band
                // We'll write the SAME data to every band.
                float[] flatArray = new float[width * height];
                int index = 0;

                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        flatArray[index] = rasterData2D[row, col];
                        index++;
                    }
                }

                // 6. Write data to each band
                for (int b = 1; b <= numberOfBands; b++)
                {
                    Band band = outDs.GetRasterBand(b);
                    band.SetNoDataValue(-9999);
                    if (band == null)
                    {
                        throw new Exception($"Could not get raster band {b} from output dataset.");
                    }

                    band.WriteRaster(
                        0, 0,              // offset in the output dataset
                        width, height,     // size to write
                        flatArray,         // buffer
                        width, height,     // buffer dimensions
                        0, 0               // pixel spacing
                    );

                    band.FlushCache();
                }

                // 7. Done
                outDs.FlushCache();
            }
            Console.WriteLine($"Created multi-band GeoTIFF: {outputFilename}");
        }
        public static int FindHighestCommonX(string folderPath, IEnumerable<string> modelNames)
        {
            // Convert modelNames to a list to avoid multiple enumerations
            var modelList = modelNames.ToList();

            // Dictionary: modelName -> set of X values
            var xValuesByModel = new Dictionary<string, HashSet<int>>();

            // Initialize dictionary with empty sets for each model
            foreach (var model in modelList)
            {
                xValuesByModel[model] = new HashSet<int>();
            }

            // Get all files in the specified folder (adjust the search pattern as needed)
            var files = Directory.GetFiles(folderPath, "SafetyMatrix_*_*.*");

            // Parse each file name
            foreach (var filePath in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var parts = fileName.Split('_');
            
                // We expect three parts: "SafetyMatrix", <modelName>, <X>
                if (parts.Length < 3) 
                    continue; // Not matching our expected pattern, skip

                var possibleModel = parts[1];
                if (!xValuesByModel.ContainsKey(possibleModel))
                    continue; // This file's modelName isn't in our list

                if (int.TryParse(parts[2], out int x))
                {
                    xValuesByModel[possibleModel].Add(x);
                }
            }

            // Intersect the sets to find X values common to all models
            // Start from the first model's set of X values
            var commonXs = new HashSet<int>(xValuesByModel[modelList[0]]);

            // Intersect with each subsequent model's set
            foreach (var model in modelList.Skip(1))
            {
                commonXs.IntersectWith(xValuesByModel[model]);
            }

            // If no common X exists, return -1
            if (commonXs.Count == 0)
                return -1;

            // Otherwise, return the maximum common X
            return commonXs.Max();
        }

        public static void wuiPointsToShapefile(int[,] wuiIn, float cellsize, string[] header, int[] rasterDims, string sourcePRJ, string shpFilePath)
        {
            // 1. Register all GDAL/OGR drivers
            Gdal.AllRegister();   // This registers both raster (GDAL) and vector (OGR) drivers

            string[] xllParts = header[2].Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string[] yllParts = header[3].Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Parse the values as float (or double, if you prefer)
            float xll = float.Parse(xllParts[1], CultureInfo.InvariantCulture);
            float yll = float.Parse(yllParts[1], CultureInfo.InvariantCulture);
            
            // Note: For a proper polygon, first and last point should match or we call ring.CloseRings().
            List<(float,float)> polygonPoints = new List<(float,float)>();
            for (int i = 0; i < wuiIn.GetLength(0); i++)
            {
                polygonPoints.Add((xll + cellsize * (wuiIn[i, 0]),yll + cellsize * (rasterDims[1] - wuiIn[i, 1])));
            }

            // 3. Get the ESRI Shapefile driver
            var shpDriver = Ogr.GetDriverByName("ESRI Shapefile");
            if (shpDriver == null)
            {
                Console.WriteLine("Cannot get 'ESRI Shapefile' driver. Is GDAL/OGR installed properly?");
                return;
            }

            // 4. If the file exists, delete it
            if (File.Exists(shpFilePath)) 
            {
                shpDriver.DeleteDataSource(shpFilePath);
            }

            // 5. Create the new shapefile (DataSource)
            DataSource dataSource = shpDriver.CreateDataSource(shpFilePath, null);
            if (dataSource == null)
            {
                Console.WriteLine("Could not create shapefile.");
                return;
            }

            // 6. (Optional) Set a Spatial Reference. For example, EPSG:4326
            SpatialReference srs = new SpatialReference("");
            string wktPrj = File.ReadAllText(sourcePRJ);
            SpatialReference sr = new SpatialReference("");
            sr.ImportFromWkt(ref wktPrj);

            // 7. Create a layer of polygon geometry
            Layer layer = dataSource.CreateLayer("layerName", srs, wkbGeometryType.wkbPolygon, null);
            if (layer == null)
            {
                Console.WriteLine("Could not create layer in shapefile.");
                return;
            }

            // 8. Create a field definition (attribute column)
            FieldDefn idField = new FieldDefn("ID", FieldType.OFTInteger);
            layer.CreateField(idField, 1);

            // 9. Build a polygon geometry from the list of points
            OSGeo.OGR.Geometry ring = new OSGeo.OGR.Geometry(wkbGeometryType.wkbLinearRing);
            foreach (var (x, y) in polygonPoints)
            {
                ring.AddPoint_2D(x, y);
            }
            // Optionally ensure the ring is closed
            ring.CloseRings();

            // Create a polygon from the ring
            OSGeo.OGR.Geometry polygon = new OSGeo.OGR.Geometry(wkbGeometryType.wkbPolygon);
            polygon.AddGeometry(ring);

            // 10. Create a feature, set its geometry and attribute(s)
            OSGeo.OGR.FeatureDefn layerDefn = layer.GetLayerDefn();
            OSGeo.OGR.Feature feature = new OSGeo.OGR.Feature(layerDefn);

            feature.SetField("ID", 1);
            feature.SetGeometry(polygon);

            // 11. Add the feature to the layer
            layer.CreateFeature(feature);

            // 12. Cleanup
            feature.Dispose();
            dataSource.FlushCache();
            dataSource.Dispose();

            Console.WriteLine($"Shapefile created: {shpFilePath}");
        }
    }
    class Vector
    {
        public double X { get; private set; }
        public double Y { get; private set; }

        // Constructor using Cartesian coordinates
        public Vector(double x, double y)
        {
            X = x;
            Y = y;
        }

        // Constructor using polar coordinates (magnitude and direction)
        public static Vector FromPolar(double magnitude, double directionInDegrees)
        {
            double directionInRadians = directionInDegrees * Math.PI / 180.0; // Convert degrees to radians
            double x = magnitude * Math.Cos(directionInRadians);
            double y = magnitude * Math.Sin(directionInRadians);
            return new Vector(x, y);
        }

        // Calculate magnitude of the vector
        public double Magnitude => Math.Sqrt(X * X + Y * Y);

        // Calculate direction of the vector in degrees
        public double Direction => Math.Atan2(Y, X) * 180.0 / Math.PI; // Convert radians to degrees

        // Add two vectors
        public static Vector Add(Vector v1, Vector v2)
        {
            double newX = v1.X + v2.X;
            double newY = v1.Y + v2.Y;
            return new Vector(newX, newY);
        }
        
        public static Vector Max(List<Vector> vectors)
        {
            double max = float.MinValue;
            int maxIndex = -1;
            for (int i = 0; i < vectors.Count; i++)
            {
                if (vectors[i].Magnitude > max)
                {
                    max=vectors[i].Magnitude;
                    maxIndex = i;
                }
            }
            return vectors[maxIndex];
        }

        // Add a set of vectors
        public static Vector Add(IEnumerable<Vector> vectors)
        {
            double totalX = vectors.Sum(v => v.X);
            double totalY = vectors.Sum(v => v.Y);
            return new Vector(totalX, totalY);
        }
        
    }
    public class createShapefile
    {
        public static void createAndWriteShapefile(double xCoord, double yCoord, string Path)               //create a shapefile for the ignition. Shamelessly stolen from the internet. Uses a NuGet package (NetTopologySuite) to work
        {
            string path = Path + "ROX";
            string firstNameAttribute = "Type";
            string lastNameAttribute = "Location";
            var geomFactory = NtsGeometryServices.Instance.CreateGeometryFactory();
            Geometry g1 = (Geometry)geomFactory.CreatePoint(new Coordinate(xCoord, yCoord));
            AttributesTable t1 = new AttributesTable();
            t1.Add(firstNameAttribute, "IgnitionPoint");
            t1.Add(lastNameAttribute, "ROX");
            Feature feat1 = new Feature(g1, t1);
            IList<Feature> features = new List<Feature>() { feat1 };
            NetTopologySuite.IO.Esri.Shapefile.WriteAllFeatures(features, path + ".shp");

            //Console.WriteLine("Shapefile Created!");
        }
    }
}
