/* #######################################################################


    <><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><>
         k-PERIL (Population Evacuation Trigger Algorithm)
    <><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><>

    ================================================================
    CODE WRITTEN AND EDITED BY NIKOLAOS KALOGEROPOULOS
    ORIGINALLY CREATED BY HARRY MITCHELL
    ================================================================

    ----------------------------------------------------------------
    DEPARTMENT OF MECHANICAL ENGINEERING
    IMPERIAL COLLEGE LONDON
    ----------------------------------------------------------------
    MORE DETAILS CAN BE FOUND IN https://doi.org/10.1016/j.firesaf.2023.103854. 
 * 
 * #######################################################################
 * 
 * WHAT IT DOES
 * 
 * k-PERIL is a program that can create trigger boundaries for any target area. 
 * Trigger boundaries are lines surrounding urban areas that are threatened 
 * by a wildfire. For a given Available Safe Egress Time (ASET) / Evacuation
 * time  of that area, the boundary is made so that, when the fire reaches
 * it, it will take an ASET amount of time to become a threat to the urban 
 * area. As such, when the fire reaches the trigger boundary, decision makers/
 * emergency responders should trigger evacuation so there is enough time 
 * for a full evacuation.
 * 
 * In particular, this program is a testing suite for the k-PERIL algorithm, which 
 * calculates the safety boundary around an urban area for a single wildfire.
 * However, the algorithm has been made so that it can also run a 
 * probabilistic simulation, for any number of plausible/theorised wildfires.
 * The lump sum of all the generated boundaries, for any number of input 
 * wildfires, is the probabilistic trigger boundary. 
 * 
 * One could in theory simulate all the fires they want to investigate
 * in software like FARSITE, Prometheus etc. and then make a small program
 * themselves using k-PERIL to calculate the trigger boundaries. For more
 * specialised cases, or for very particular fire conditions, or if input
 * data is very analytically given, this is a viable solution. In most cases,
 * accurate input data (moisture, wind, temperature, ignition source etc.) are
 * impossible to predict or even tabulate. As such, this program is made to take in
 * average values of environmental conditions, and use them to create 
 * a number of random values. 
 * 
 * Each environmental parameter (Max and Min Temperature, Humidity, Time
 * thereof, wind magnitude) are specified with an average value and 
 * standard deviation. Each simulation uses values randomly generated, 
 * following a normal distribution from the above values. Wind direction
 * always points to the center of the raster, to ensure a uniform fire 
 * coverage / a fire that covers the whole raster. Fuel Humidity values
 * are taken as uniform (and currently hardcoded) since it is near 
 * impossible to obtain. 
 * 
 * Weather data are extrapolated to a diurnal cycle based on the maximum
 * and minimum values. 
 * 
 * Once all the data is selected and saved, this program runs a command-line version
 * of FARSITE or Flammap that will solve the set fire simulation case, creating 
 * (among other things) two ROS data rasters. Those are then read by 
 * k-PERIL and it creates a boundary for this specified fire. This repeats
 * for as many time (hardcoded right now) as needed, and in the end the code
 * will output a raster where each cell represents how many times each 
 * point was inside a safety matrix. 
 * 
 * #######################################################################
 * 
 * HOW IT WORKS
 * 
 * RCG is made to take in a few files in the input folder:
 * 
 * >VARS.txt: this file contains all the input parameters of the simulation:

                Currently all the units for the above are in metric, ASET is in minutes, 
                var... values indicate standard deviations, and urban1x and urban1y indicate 
                the X and Y values of the first urban node (X and Y values in relation to 
                the raster size, with 0,0 being the top left corner of the raster, and 1,0 being the next adjacent cell. 
                increasing with +X and -Y. English values are not yet supported.

                When specifying Urban nodes, inly include the edges of the polygon that
                encompasses your target urban area. k-PERIL can then calculate and use
                all the nodes that are inside the polygon whose corners you specified. 

    >INPUT.lcp and INPUT.prj: The landscape and projection files of the area
you want to work on. 

    >FUELTEMPLATE.asc (optionally also FUELTEMPLATE.prj): the isolated fuel 
layer of INPUT.lcp. This is so that the simulation can avoid nonfuel ignition
points and cause an error. 

#######################################################################

SPECIAL FEATURES:

> Each simulation's input values are saved to a log.txt file in the main
directory. You can then use it to plot the used variables and see or
explain that they follow a normal distribution. It is also useful
for debugging purposes. All the rate of spread magnitude matrices are also
saved for debugging and analysis purposes.

#######################################################################

CHANGE LOG

1.0: Release

#######################################################################

Known Bugs / Issues


#######################################################################

*/


using System;
using System.IO;
using System.Linq;
using k_PERIL_DLL;

using System.Diagnostics;
using System.Data;
using System.Threading;

namespace RoxCaseGen
{
    internal class Program
    {
        static void Main(string[] args)
        {


            // ----------------------CHANGE VALUES HERE------------------------

            bool runFromExe = false;            //true if you want to compile this code to an executable, false if you want to run it from source. 
            bool newData = true;                //this is in case FARSITE crashes but you do not want to erase the points you have already calculated. Set it to false and the code will continue from the last point.

            string Path;

            if (runFromExe)
            {
                string exePath = Environment.CurrentDirectory;
                Path = System.IO.Path.GetFullPath(System.IO.Path.Combine(exePath, @"\"));
            }
            else
            {
                Path = @"C:\Users\nikos\source\repos\RoxCaseGen\";            //use this line if you are running the code from VS, change the value to your folder..
            }

            //-----------------------------------------------------------------

            if (newData)
            {
                if (Directory.Exists(Path + "Median_Outputs/"))
                {
                    DirectoryInfo di = new DirectoryInfo(Path + "Median_Outputs/");
                    foreach (FileInfo file in di.EnumerateFiles())
                    {
                        file.Delete();
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path + "Median_Outputs/");
                }
            }
            FlammapSetup please = new FlammapSetup();

            float[,] fileInput = please.parseInputFilesToMemory(Path + "Input/VARS.txt");           

            int[,] safetyMatrix = new int[(int)fileInput[15, 0], (int)fileInput[16, 0]];

            if (!newData)                                                   //if this is a continuation of a failed previous study
            {
                if (File.Exists(Path + "Outputs/SafetyMatrix.txt"))
                {
                    float[,] temp = please.parseSafetyMatrixToMemory(Path + "Outputs/SafetyMatrix.txt");
                    for (int i = 0; i < safetyMatrix.GetLength(0); i++)
                    {
                        for (int j = 0; j < safetyMatrix.GetLength(1); j++)
                        {
                            safetyMatrix[i, j] = (int)temp[j, i];           //read safetymatrix already calculated
                        }
                    }
                }
                else
                {
                    if (File.Exists(Path + "log.txt")) { File.Delete(Path + "log.txt"); }
                    if (File.Exists(Path + "Outputs/chosenRasterBoundary.txt")) { File.Delete(Path + "Outputs/chosenRasterBoundary.txt"); }
                    if (File.Exists(Path + "Outputs/chosenBoundary.txt")) { File.Delete(Path + "Outputs/chosenBoundary.txt"); } //if a new case study is starting, delete the previous log file.
                }
            }
            else
            {
                if (File.Exists(Path + "log.txt")) { File.Delete(Path + "log.txt"); }
                if (Directory.Exists(Path + "Outputs/")) { Directory.Delete(Path + "Outputs/", true); }
                Directory.CreateDirectory(Path + "Outputs/");             //To remove all the output files from the previous run
            }

            int[,] WUI_in = new int[(int)fileInput[17, 0], 2];              //pase in the WUI polygon corners

            for (int i = 0; i < fileInput[17, 0]; i++)
            {
                WUI_in[i, 0] = (int)fileInput[18 + 2 * i, 0];
                WUI_in[i, 1] = (int)fileInput[18 + 2 * i + 1, 0];
            }

            PERIL peril = new PERIL();

            int[,] WUI = PERIL.getPolygonEdgeNodes(WUI_in);                 //get all the useful WUI polygon nodes based on the given corners. 

            FlammapSetup.OutputFile(WUI, Path + "Outputs/WUIboundary.txt");       //save the used urban nodes in file for debugging/visualisation/further inspection.
            float[,] fuelMap = please.parseFARSITEFilesToMemory(Path + "Input/FUELTEMPLATE.asc");

            please.setValues(Path);

            Process FireModel = new Process();                                //declare the FARSITE command line process
            Process SetEnv= new Process();
            SetEnv.StartInfo.FileName = Path + "/FLAMMAP/SetEnv.bat";

            int fireModel = (int)fileInput[fileInput.GetLength(0) - 1, 0];
            switch (fireModel)
            {
                case 0:             //FARSITE
                    FireModel.StartInfo.FileName = Path + "FLAMMAP/TestFARSITE.exe";          //set some of its operating parameters
                    please.WriteStarterFileFARSITE(Path);
                    break;
                case 1:             //FLAMMAP
                    FireModel.StartInfo.FileName = Path + "RunFLAMMAP.bat";          //set some of its operating parameters
                    please.WriteStarterFileFLAMMAP(Path);
                    break;
            }

            FireModel.StartInfo.Arguments = $"{Path}/ROX.txt";
            FireModel.StartInfo.UseShellExecute = false;
            FireModel.StartInfo.CreateNoWindow = false;
            SetEnv.StartInfo.UseShellExecute = false;
            SetEnv.StartInfo.CreateNoWindow = false;
            SetEnv.Start();

            float currentError = 1;
            float consecutiveConvergence = 0;
            int simNo = 0;
            while (consecutiveConvergence<20)
            {
                simNo++;
                Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
                Console.WriteLine($"Initiating RUN {simNo}");
                Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
                Console.WriteLine($"Current Convergence Criterion is {currentError}.");
                Console.WriteLine($"{consecutiveConvergence} of 20 consecutive criteria under 0.01.");
                Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
                if (simNo <= 20)
                {
                    Console.WriteLine($"Convergence not checked yet, not enough results");
                    Console.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^");
                }



                File.Delete(Path + "ROX.shp");                              //delete existing ignition files
                File.Delete(Path + "ROX.dbf");
                File.Delete(Path + "ROX.shx");
                File.Delete(Path + "ROX.input");                            //delete existing FARSITE input file
                int[] coordinates = please.SelectIgnitionPoint(fuelMap,Path, false);    //choose ignition coordinates
                please.randomizeValues();                                   //randomise the weather and wind values
                please.saveUsedData(Path);                                  //dump the selected variables in log.txt
                please.createTempAndHumidProfile();                         //extrapolate diurnal temperature and humidity profiles

                switch (fireModel)
                {
                    case 0:             //FARSITE
                        please.createAndWriteFileFARSITE(Path);                            //create the FARSITE input file
                        break;
                    case 1:             //FLAMMAP
                        please.createAndWriteFileFLAMMAP(Path);
                        break;
                }
                createShapefile.createAndWriteShapefile(coordinates[0], coordinates[1], Path);  //create the shapefile for the ignition point

                FireModel.Start();                                            //Start command line FARSITE

                int delayMonitor = 0;                     
                bool FarsiteKill=false;

                while (!File.Exists(Path + "Median_Outputs/_SpreadRate.asc"))       //Check whether ROS data has been generated (i.e. FARSITE has finished)
                {
                    /*
                    switch (fireModel)          //convert geoTIFF to ASCII
                    {
                        case 0:             //FARSITE
                            break;
                        case 1:             //FLAMMAP
                            if (File.Exists(Path + "Median_Outputs/_SpreadRate.tif"))
                            {
                                GeoTiffHelpers.GeoTiffHelpers.convertGeotiffToAsc(Path + "Median_Outputs/_SpreadRate.tif", Path + "Median_Outputs/_SpreadRate.asc");
                                GeoTiffHelpers.GeoTiffHelpers.convertGeotiffToAsc(Path + "Median_Outputs/_SpreadDirection.tif", Path + "Median_Outputs/_SpreadDirection.asc");
                            }
                            break;
                    }
                    */

                    if (delayMonitor > 300)
                    {
                        Console.WriteLine("MODEL PROCESS TAKING TOO LONG (POSSIBLY LOOPS). STOPPING AND SKIPPING CASE");      //if FARSITE takes more than 10 minutes, kill the process
                        FireModel.Kill();
                        FarsiteKill = true;
                        break;
                    }
                    System.Threading.Thread.Sleep(1000);
                    delayMonitor++;
                }
                if (!FarsiteKill)               //do the following unless FARSITE was force-killed earlier
                {
                    System.Threading.Thread.Sleep(500);                     //wait so that some file write is done
                    
                    float[,] ROS = please.parseFARSITEFilesToMemory(Path + "Median_Outputs/_SPREADRATE.asc");               //Read ROS magnitude data
                    File.Move(Path + "Median_Outputs/_SPREADRATE.asc", Path + "Median_Outputs/_SPREADRATE_" + simNo.ToString() + ".asc");
                    float[,] Azimuth = new float[ROS.GetLength(0), ROS.GetLength(1)];
                    Thread.Sleep(1000);
                    switch (fireModel) { 
                        case 0:
                            Azimuth = please.parseFARSITEFilesToMemory(Path + "Median_Outputs/_SpreadDirection.asc");      //read ROS direction data
                            File.Move(Path + "Median_Outputs/_SpreadDirection.asc", Path + "Median_Outputs/_SpreadDirection_" + simNo.ToString() + ".asc");
                            break;
                        case 1:
                            Azimuth = please.parseFARSITEFilesToMemory(Path + "Median_Outputs/_MAXSPREADDIR.asc");      //read ROS direction data
                            File.Move(Path + "Median_Outputs/_MAXSPREADDIR.asc", Path + "Median_Outputs/_MAXSPREADDIR_" + simNo.ToString() + ".asc");
                            break;
                    }
                    
                    int[,] temp = peril.calcSingularBoundary(30, (int)please.actualASET, please.windMag, WUI, ROS, Azimuth);            //Call k-PERIL to find the boundary of this simulation

                    for (int j = 0; j < temp.GetLength(0); j++)
                    {
                        for (int k = 0; k < temp.GetLength(1); k++)
                        {
                            safetyMatrix[j, k] += temp[j, k];                                                               //add the trigger boundary safe area to the overall safety matrix 
                        }
                    }
                    
                    FlammapSetup.OutputFile(temp, Path + "Outputs/SafetyMatrix" + simNo + ".txt");
                    if (simNo > 20) { currentError = please.calcConvergence(simNo, Path); }
                    FlammapSetup.OutputFile(safetyMatrix, Path + "Outputs/SafetyMatrix.txt");
                    if (currentError < 0.01) { consecutiveConvergence++; }
                    else { consecutiveConvergence=0; }
                }
            }
            FlammapSetup.OutputFile(safetyMatrix, Path + "Outputs/SafetyMatrix.txt");
            //int[,] areaBoundary = PERIL.getDenseBoundaryFromProbBoundary(safetyMatrix, 20);       //not used anymore.
            //FlammapSetup.OutputFile(areaBoundary, Path + "Outputs/chosenRasterBoundary.txt");
        }
    }
}
