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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using k_PERIL_DLL;

using System.Diagnostics;
using System.Data;
using System.Drawing;
using System.Threading;

namespace RoxCaseGen
{
    internal class Program
    {
        static void Main(string[] args)
        {
            const string path = @"D:/OneDrive - Imperial College London/Imperial/PhD\k2PERIL/"; //use this line if you are running the code from VS, change the value to your folder.
            const int burnDuration = 24;
            string[] models = { "Farsite",
                "WISE", 
                //"ELMFIRE", 
                //"EPD", 
                //"LSTM", 
                "FDS LS1",
                "FDS LS4" };
            
            //cd C:\WISE_Builder-1.0.6-beta.5; java -jar WISE_Builder.jar -s -j C:\jobs
            
            //cd C:\WISE_Manager-0.6.beta.5; java -jar WISE_Manager_Ui.jar
            
            //Hoursekeeping
            //Delete Previous Run Leftovers:
            if (Directory.Exists(path + "/Outputs"))
            {
                Directory.Delete(path+ "/Outputs", true);
            }
            Directory.CreateDirectory(path+ "/Outputs");
            ModelSetup.prepareNextIteration(path);
            RunModels.checkInputsExist(path);
            PERIL peril = new PERIL();
            ModelSetup please = new ModelSetup();
            
            please.burnDuration = burnDuration;
            please.fuelMoisture = [6, 7, 8, 60, 90];
            
            float[,] fileInput = please.parseInputFilesToMemory(path + "Input/VARS.txt");
            
            int[,] WUI_in = new int[(int)fileInput[17, 0], 2];              //pase in the WUI polygon corners
            for (int i = 0; i < fileInput[17, 0]; i++)
            {
                WUI_in[i, 0] = (int)fileInput[18 + 2 * i, 0];
                WUI_in[i, 1] = (int)fileInput[18 + 2 * i + 1, 0];
            }
            
            List<PointF> WUI_f = new List<PointF>();
            for (int i = 0; i < WUI_in.GetLength(0); i++)
            {
                WUI_f.Add(new PointF(WUI_in[i, 0], WUI_in[i, 1]));
            }  
            please.WUI = WUI_f;
            
            int[,] WUI = PERIL.getPolygonEdgeNodes(WUI_in);                 //get all the useful WUI polygon nodes based on the given corners. 
            ModelSetup.OutputFile(WUI, path + "/WUIboundary.txt");       //save the used urban nodes in file for debugging/visualisation/further inspection.
            please.setValues(path);
            please.fuelMap = ModelSetup.readASC_int(path + "Input/FUELTEMPLATE.asc");
            bool[] modelsDone = new bool[models.Length];
            int[,,] allSafetyBoundaries = new int[6,please.fuelMap.GetLength(0), please.fuelMap.GetLength(1)];
            int simNo = 0;
            int[] consecutiveConvergence = new int[6];
            float currentError=1;
            while (!modelsDone.All(x => x))
            {
                Console.WriteLine($"Sim no: {simNo}, current convergence: ");
                for (int i = 0; i < models.Length; i++)
                {
                    Console.WriteLine($"{models[i]}: {modelsDone[i]}");
                }
                RunModels.setupFarsiteIteration(path + "Farsite/Input/", please);
                for (int i = 0; i < models.GetLength(0); i++)
                {
                    RunModels.convertToSpecificModel(models[i], path, please);
                    RunModels.RunModel(models[i], path, please,simNo);
                    float[,] ros = RunModels.retrieveResult(models[i],path,"ROS", please);
                    float[,] azimuth = RunModels.retrieveResult(models[i],path,"Azimuth", please);
                    
                    int[,] temp = peril.calcSingularBoundary(30, (int)please.actualASET, please.windMag, WUI, ros, azimuth);            //Call k-PERIL to find the boundary of this simulation

                    for (int j = 0; j < temp.GetLength(0); j++)
                    {
                        for (int k = 0; k < temp.GetLength(1); k++)
                        {
                            allSafetyBoundaries[i,j,k] += temp[j, k];
                        }
                    }
                    
                    ModelSetup.OutputFile(temp, path + $"Outputs/SafetyMatrix_{models[i]}_" + simNo + ".txt");
                    if (simNo > 20) { currentError = please.CalcConvergence(simNo, path, models[i]); }
                    ModelSetup.OutputFile(ModelSetup.GetSlice(allSafetyBoundaries,i), path + $"Outputs/SafetyMatrix_{models[i]}.txt");
                    if (currentError < 0.003) { consecutiveConvergence[i]++; }
                    else { consecutiveConvergence[i]=0; }
                    if (consecutiveConvergence[i] >= 20) { modelsDone[i] = true; }
                    modelsDone[modelsDone.Length-1] = true;
                    modelsDone[modelsDone.Length-2] = true;
                }
                Console.ReadLine();
                ModelSetup.prepareNextIteration(path);
            }

            for (int i = 0; i < models.GetLength(0); i++)
            {
                int[,] output = new int[allSafetyBoundaries.GetLength(1), allSafetyBoundaries.GetLength(2)];
                for (int j = 0; j < output.GetLength(0); j++)
                {
                    for (int k = 0; k < output.GetLength(1); k++)
                    {
                        output[j, k] = allSafetyBoundaries[i, j, k]; 
                    }
                }
                ModelSetup.OutputFile(output, path + $"Outputs/SafetyMatrix_{models[i]}.txt");
            }
        }
    }
}
