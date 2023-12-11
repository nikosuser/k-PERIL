/* 

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
using k_PERIL_DLL;

using NetTopologySuite;
using NetTopologySuite.IO;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using System.Reflection;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;
using System.Data;
using BitMiracle.LibTiff.Classic;
using System.Diagnostics.CodeAnalysis;
using NetTopologySuite.Index.Strtree;
using System.Text.RegularExpressions;

namespace RoxCaseGen
{
    public class FlammapSetup
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

        int conditioningDays;

        float actualMaxTemp;
        float actualMinTemp;
        float actualMaxTime;
        float actualMinTime;
        float actualMaxHumid;
        float actualMinHumid;

        public float actualASET;

        public float windMag;
        public int windDir;

        float[] humidProfile;
        float[] tempProfile;

        string year;
        string month;

        int xIgnition;
        int yIgnition;

        int IgnitionFuelModel;

        public int ASET;
        public int varASET;

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

            this.conditioningDays = (int)fileInput[14, 0];
            this.ASET = (int)fileInput[fileInput.GetLength(0) - 3, 0];
            this.varASET = (int)fileInput[fileInput.GetLength(0) - 2, 0];
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
                for (int i = minTime; i <= maxTime; i++)
                {
                    tempProfile[i] = (float)(minTemp + (maxTemp - minTemp) * (0.5 + 0.5 * (float)Math.Cos((maxTime - i) * Math.PI / (maxTime - minTime))));
                    humidProfile[i] = (float)(maxHumid - (maxHumid - minHumid) * (0.5 + 0.5 * (float)Math.Cos((maxTime - i) * Math.PI / (maxTime - minTime))));
                }
                for (int j = 0; j < minTime; j++)
                {
                    tempProfile[j] = (float)(maxTemp - (maxTemp - minTemp) * (0.5 + 0.5 * (float)Math.Cos((j + 24 - maxTime) * Math.PI / (24 + maxTime - minTime))));
                    humidProfile[j] = (float)(minHumid + (maxHumid - minHumid) * (0.5 + 0.5 * (float)Math.Cos((j + 24 - maxTime) * Math.PI / (24 + maxTime - minTime))));
                }
                for (int k = maxTime + 1; k < 24; k++)
                {
                    tempProfile[k] = (float)(minTemp + (maxTemp - minTemp) * (0.5 + 0.5 * (float)Math.Cos((k - minTime) * Math.PI / (24 - maxTime + minTime))));
                    humidProfile[k] = (float)(maxHumid - (maxHumid - minHumid) * (0.5 + 0.5 * (float)Math.Cos((k - minTime) * Math.PI / (24 - maxTime + minTime))));
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
            List<string> outputFile = new List<string>(); //[15 + 256 + this.conditioningDays * 24 + 20];           //instantiate the output array
            this.year = "2022";                                                                     //date of starting data, doesnt really matter.
            this.month = "9";
            outputFile.Add($"CONDITIONING_PERIOD_END: {this.month} {this.conditioningDays} 1600");
            outputFile.Add("FUEL_MOISTURES_DATA: 255");                                             //all the fuel moistures are the same and hardcoded below for now.
            for (int i = 1; i < 256; i++)
            {
                outputFile.Add((i - 1).ToString() + " 6 7 8 60 90");
            }
            outputFile.Add("RAWS_ELEVATION: 200");                                               //random value added for now. Maybe it is too high?
            outputFile.Add("RAWS_UNITS: Metric");                                                 //All values declared metric
            outputFile.Add($"RAWS: {(this.conditioningDays * 24).ToString()}");                   //Calculate and declare how many weather points will follow
            for (int i = 0; i < this.conditioningDays; i++)                                          //Output all the weather data in FARSITE flavor. Precipitation is defaulted to 20%
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
            outputFile.Add($"WIND_SPEED: {this.windMag}");
            outputFile.Add($"WIND_DIRECTION: {this.windDir}");
            outputFile.Add("SPREAD_DIRECTION_FROM_MAX: 0");
            outputFile.Add("GRIDDED_WINDS_GENERATE: Yes");
            outputFile.Add("GRIDDED_WINDS_RESOLUTION: 10.0");

            outputFile.Add($"FARSITE_START_TIME: {this.month} {this.conditioningDays} 0000");
            outputFile.Add($"FARSITE_END_TIME: {this.month} {this.conditioningDays} 2300");
            outputFile.Add("FARSITE_TIMESTEP: 60");
            outputFile.Add("FARSITE_DISTANCE_RES: 60.0");
            outputFile.Add("FARSITE_PERIMIETER_RES: 60.0");
            outputFile.Add("FARSITE_MIN_IGNITION_VERTEX_DISTANCE: 15.0");
            outputFile.Add("FARSITE_SPOT_GRID_RESOLUTION: 30.0");
            outputFile.Add("FARSITE_SPOT_PROBABILITY: 0.05");
            outputFile.Add("FARSITE_SPOT_IGNITION_DELAY: 0");
            outputFile.Add("FARSITE_MINIMUM_SPOT_DISTANCE: 60");
            outputFile.Add("FARSITE_ACCELERATION_ON: 1");
            outputFile.Add($"FARSITE_IGNITION_FILE: {Path}ROX.shp");

            File.WriteAllLines(Path + "ROX.input", outputFile.ToArray());

            //Console.WriteLine("Input File Created!");
        }

        public void createAndWriteFileFLAMMAP(string Path)                     //create the input file required by the FARSITE console application.
        {
            List<string> outputFile = new List<string>(); //[15 + 256 + this.conditioningDays * 24 + 20];           //instantiate the output array
            this.year = "2022";                                                                     //date of starting data, doesnt really matter.
            this.month = "6";

            outputFile.Add($"CONDITIONING_PERIOD_END: {this.month} {this.conditioningDays} 1600");

            outputFile.Add("FUEL_MOISTURES_DATA: 255");                                             //all the fuel moistures are the same and hardcoded below for now.
            for (int i = 1; i < 256; i++)
            {
                outputFile.Add((i - 1).ToString() + " 4 6 13 30 60");
            }
            outputFile.Add("RAWS_ELEVATION: 190");                                               
            outputFile.Add("RAWS_UNITS: Metric");                                                 //All values declared metric
            outputFile.Add($"RAWS: {(this.conditioningDays * 24).ToString()}");                   //Calculate and declare how many weather points will follow
            for (int i = 0; i < this.conditioningDays; i++)                                          //Output all the weather data in FARSITE flavor. Precipitation is defaulted to 20%
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

            File.WriteAllLines(Path + "ROX.input", outputFile.ToArray());

            //Console.WriteLine("Input File Created!");
        }
        public float[,] parseFARSITEFilesToMemory(string path)              //Read FARSITE output files, delete the first 6 lines as they do not contain useful information (for this program)
        {
            string[] FileRawInput = File.ReadAllLines(path);
            List<string> ParsedInput = FileRawInput.ToList();
            ParsedInput.RemoveRange(0, 6);
            FileRawInput = ParsedInput.ToArray();

            return parseFile(FileRawInput);                                                //return the output matrix
        }

        public float[,] parseSafetyMatrixToMemory(string path)              //Read FARSITE output files, delete the first 6 lines as they do not contain useful information (for this program)
        {
            string[] FileRawInput = File.ReadAllLines(path);
            List<string> ParsedInput = FileRawInput.ToList();
            FileRawInput = ParsedInput.ToArray();

            return parseFile(FileRawInput);                                                //return the output matrix
        }

        private float[,] parseFile(string[] input)                          //This code is also shamelessly stolen from the internet to convert a raster from a string array format (as is done by reading a file) to a 2D numeric array..
        {
            var arrays = new List<float[]>();

            foreach (var line in input)                                     //for each line variable in the list(?) lines
            {
                var lineArray = new List<float>();                          //make a new list of floats
                foreach (var s in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))   //for each variable s in the input array
                {
                    lineArray.Add(float.Parse(s, System.Globalization.NumberStyles.Float)); //add the element to the list of floats created before the loop
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
        public static void OutputFile(int[,] boundary, string PerilOutput)      //output variable to a new text file, shamelessly stolen
        {
            using (var sw = new StreamWriter(PerilOutput))  //beyond here the code has been shamelessly stolen
            {
                for (int i = 0; i < boundary.GetLength(1); i++)   //for all elements in the output array
                {
                    for (int j = 0; j < boundary.GetLength(0); j++)
                    {
                        sw.Write(boundary[j, i] + " ");       //write the element in the file
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

        public int[] SelectIgnitionPoint(float[,] fuelMap, string fuelMapPath, bool randWind)                  //Select ignition point based on target ignition area.
        {
            Random rand = new Random();                                     //create new random

            int ncols = 0;
            int nrows = 0;
            float xllcorner = 0;
            float yllcorner = 0;
            float cellsize = 0;

            try
            {
                using (StreamReader reader = new StreamReader(fuelMapPath + "Input/FUELTEMPLATE.asc"))
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

            this.xIgnition = (int)(rand.NextDouble() * ncols);        //get random ignition location
            this.yIgnition = (int)(rand.NextDouble() * nrows);

            this.IgnitionFuelModel = (int)fuelMap[this.yIgnition, this.xIgnition];      //find the fuel inside the ignition point

            while (this.IgnitionFuelModel == -9999 || this.IgnitionFuelModel == 91 || this.IgnitionFuelModel == 98 || this.IgnitionFuelModel == 99 || PointDistance(this.xIgnition,this.yIgnition,ncols/2, nrows / 2) < Math.Min(ncols / 4, nrows / 4) ) //if the ignition point has nonfuel on it, or is outside the designated area, retry
            {
                this.xIgnition = (int)(rand.NextDouble() * ncols);
                this.yIgnition = (int)(rand.NextDouble() * nrows);
                this.IgnitionFuelModel = (int)fuelMap[this.yIgnition, this.xIgnition];
            }

            double[] proportionalIgnitionPoint = new double[2] { (float)this.xIgnition / (float)ncols, (float)this.yIgnition / (float)nrows };  //convert from raster values to proportional/nondimensional values (0-1 progress from the origin)
            
            if (randWind)
            {
                this.windDir = (int)(rand.NextDouble() * 360);
            }
            else
            {
                this.windDir = (360 - (int)((180 / Math.PI) * Math.Atan2((this.yIgnition - (int)(nrows / 2)), (this.xIgnition - (int)(ncols / 2)))))%360;   //calculate the wind direction to point towards the center of the raster. 
            }
            int[] actualIgnition = new int[2] { (int)(minXcoord + (maxXcoord - minXcoord) * proportionalIgnitionPoint[0]), (int)(maxYcoord - (maxYcoord - minYcoord) * proportionalIgnitionPoint[1]) }; //convert the ignition to actual coorginates

            return actualIgnition;
        }
        public void saveUsedData(string Path)                   //dump the used values of each case to a log file.
        {
            string output = $"{this.windMag} {this.windDir} {this.actualMaxHumid} {this.actualMaxTemp} {this.actualMaxTime} {this.actualMinHumid} {this.actualMinTemp} {this.actualMinTime} {this.xIgnition} {this.yIgnition} {this.IgnitionFuelModel} {this.actualASET}";        //dump all the values in a string
            string path = Path + "/log.txt";
            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine("windMag windDir maxHumid maxTemp maxTime minHumid minTemp minTime xIgnition yIgnition IgnitionFuel ASET");     //if the file does not exist, create it. The write the header.
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
            Console.WriteLine($"Ignition X-coordinate: {this.xIgnition:0.0#} ");
            Console.WriteLine($"Ignition Y-Coordinate: {this.yIgnition:0.0#} ");
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
            string path = Path + "/ROX.txt";
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

        public float calcConvergence(int currentIteration, string Path)
        {
            float[,] previousOutput = parseSafetyMatrixToMemory(Path + "Outputs/SafetyMatrix.txt");
            float[,] currentSF = parseSafetyMatrixToMemory(Path + "Outputs/SafetyMatrix" + currentIteration.ToString() + ".txt");

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


    }


    public class createShapefile
    {
        public static void createAndWriteShapefile(double xCoord, double yCoord, string Path)               //create a shapefile for the ignition. Shamelessly stolen from the internet. Uses a NuGet package (NetTopologySuite) to work
        {
            string path = Path + "ROX";
            string firstNameAttribute = "Type";
            string lastNameAttribute = "Location";

            //create geometry factory
            var geomFactory = NtsGeometryServices.Instance.CreateGeometryFactory();

            //create the default table with fields - alternately use DBaseField classes
            AttributesTable t1 = new AttributesTable();
            t1.Add(firstNameAttribute, "IgnitionPoint");
            t1.Add(lastNameAttribute, "ROX");

            //create geometries and features
            Geometry g1 = (Geometry)geomFactory.CreatePoint(new Coordinate(xCoord, yCoord));

            Feature feat1 = new Feature(g1, t1);

            //create attribute list
            IList<Feature> features = new List<Feature>() { feat1 };
            ShapefileDataWriter writer = new ShapefileDataWriter(path) { Header = ShapefileDataWriter.GetHeader(features[0], features.Count) };

            System.Collections.IList featList = (System.Collections.IList)features;
            writer.Write((IEnumerable<IFeature>)featList);

            //Console.WriteLine("Shapefile Created!");
        }
    }
}
