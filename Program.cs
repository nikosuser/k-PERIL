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
using MaxRev.Gdal.Core;
using OSGeo.GDAL;


namespace RoxCaseGen
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //recalc_tbs();
            Main_source();
            //Main_demo();
        }

        static void recalc_tbs()
        {
            string path = Environment.GetEnvironmentVariable("RUNPATH");
            var tbOutputNames = new List<string> { "ELMFIRE", "EPD", "Farsite", "LSTM", "WISE" };
            int highestCommonX = ModelSetup.FindHighestCommonX(path + "/Outputs/", tbOutputNames);

            foreach (string name in tbOutputNames)
            {
                string[] header = ModelSetup.GetHeader($"{path}/Outputs/SafetyMatrix_{name}.txt");
                int[,] currentTb = ModelSetup.readASC_int($"{path}/Outputs/SafetyMatrix_{name}.txt");
                int[,] tb = new int[currentTb.GetLength(0), currentTb.GetLength(1)];
                for (int i = 1; i < highestCommonX+1; i++)
                {
                    int[,] readFile = ModelSetup.readASC_int($"{path}/Outputs/SafetyMatrix_{name}_{i}.txt");
                    for (int j = 0; j < tb.GetLength(0); j++)
                    {
                        for (int k = 0; k < tb.GetLength(1); k++)
                        {
                            readFile[j, k] = readFile[j, k] < 0 ? 0 : readFile[j, k];
                            tb[j,k]+=readFile[j,k];
                        }
                    }
                }
                ModelSetup.Output_ASC_File(header, tb,$"{path}/Outputs/SafetyMatrix_{name}.txt");
            }
        }
        static void Main_source()
        {
            string path = Environment.GetEnvironmentVariable("RUNPATH");
            string gdalDataPath ="C:/Users/nikos/.nuget/packages/maxrev.gdal.core/3.10.0.306/runtimes/any/native/gdal-data";
            Environment.SetEnvironmentVariable ("GDAL_DATA", gdalDataPath);
            Gdal.SetConfigOption ("GDAL_DATA", gdalDataPath);
            string gdalSharePath = "C:/Users/nikos/.nuget/packages/gdal.native/3.10.0/build/gdal/share/";
            Environment.SetEnvironmentVariable ("PROJ_LIB", gdalSharePath);
            Gdal.SetConfigOption("PROJ_LIB", gdalSharePath);

            if (!string.IsNullOrEmpty(path))
            {
                Console.WriteLine($"RUNPATH is: {path}");
            }
            else
            {
                Console.WriteLine("RUNPATH is not set.");
            }
            
            string[] models = { "Farsite",
                "WISE", 
                "ELMFIRE",
                "EPD", 
                "LSTM", 
                "FDS LS1"};

            const int conditioningDays = 30;
            int[] burnHours = [1000,600,600,600,600,600];
            const int cellsize = 200;
            const bool mlTrain = false;
            const bool continueFromPrevious = false;
            
            //cd C:\WISE_Builder-1.0.6-beta.5; java -jar WISE_Builder.jar -s -j C:\jobs
            
            //cd C:\WISE_Manager-0.6.beta.5; java -jar WISE_Manager_Ui.jar
            
            //Hoursekeeping
            //Delete Previous Run Leftovers:

            int simNo = 0;

            if (continueFromPrevious)
            {
                var tbOutputNames = new List<string> { "ELMFIRE", "EPD", "Farsite", "LSTM", "WISE" };
                int highestCommonX = ModelSetup.FindHighestCommonX(path + "/Outputs/", tbOutputNames);

                simNo = mlTrain ? (int)(Directory.GetFiles($"{path}/ML/").Length/3)+1 : highestCommonX; 
                
                //reset convergence stuff
            }
            else
            {
                if (Directory.Exists(path + "/Outputs"))
                {
                    Directory.Delete(path+ "/Outputs", true);
                }
                Directory.CreateDirectory(path+ "/Outputs");
                if (Directory.Exists(path + "/Log/"))
                {
                    Directory.Delete(path+ "/Log/", true);
                }
                File.Delete(path + "/log.txt");
                File.Delete(path + "/times.txt");
                Directory.CreateDirectory(path+ "/Log/");
                ModelSetup.CleanupIters(path+"/FDS LS1/");
                ModelSetup.CleanupIters(path+"/FDS LS4/");
                simNo = mlTrain ? (int)(Directory.GetFiles($"{path}/ML/").Length/3)+1 : 0;
            }

            ModelSetup.PrepareNextIteration(path);
            RunModels.CheckInputsExist(path);
            Peril peril = new Peril();
            ModelSetup please = new ModelSetup();
            
            float[,] fileInput = please.ParseInputFilesToMemory(path + "Input/VARS.txt");
            
            int[,] wuiIn = new int[(int)fileInput[14, 0], 2];              //pase in the WUI polygon corners
            for (int i = 0; i < fileInput[14, 0]; i++)
            {
                wuiIn[i, 0] = (int)fileInput[15 + 2 * i, 0];
                wuiIn[i, 1] = (int)fileInput[15 + 2 * i + 1, 0];
            }
            
            List<PointF> wuiF = new List<PointF>();
            for (int i = 0; i < wuiIn.GetLength(0); i++)
            {
                wuiF.Add(new PointF(wuiIn[i, 0], wuiIn[i, 1]));
            }  
            please.Wui = wuiF;
            please.FuelMap = ModelSetup.readASC_int(path + "Input/Farsite/Input/fuel.asc");
            int[,] wui = Peril.GetPolygonEdgeNodes(wuiIn);                 //get all the useful WUI polygon nodes based on the given corners. 
            string[] header = ModelSetup.GetHeader(path + "Input/Farsite/Input/fuel.asc");
            ModelSetup.WuiPointsToShapefile(wuiIn, cellsize, header, [please.FuelMap.GetLength(0),please.FuelMap.GetLength(1)],path + "Input/Farsite/Input/fuel.prj",path + "/WUI.shp");
            
            ModelSetup.Output_ASC_File(header,wui, path + "/WUIboundary.txt");       //save the used urban nodes in file for debugging/visualisation/further inspection.
            
            please.SetValues(path);
            please.ConditioningDays = conditioningDays;
            please.Cellsize = cellsize;
            please.FuelMoisture = [6, 7, 8, 60, 90];
            please.RawStartTime = new DateTime(2025, 1, 1, 0, 0, 0);
            bool[] modelsDone = new bool[models.Length];
            int[,,] allSafetyBoundaries =
                new int[models.GetLength(0), please.FuelMap.GetLength(0), please.FuelMap.GetLength(1)];
            int[] consecutiveConvergence = new int[models.GetLength(0)];
            float[] currentError=[1,1,1,1,1,1,1];

            float executionTime = 0;

            while (!modelsDone.All(x => x))
            {
                if (mlTrain)
                {
                    RunModels.CheckInputsExist(path);
                    Console.WriteLine($"Sim no: {simNo}, current convergence: ");
                    int[] ignitionCoords = please.InitialiseIterationAndGetIgnition(path);
                    please.ConditioningDays = 1 + burnHours[1] / 24;
                    RunModels.SetupFarsiteIteration(path + "Farsite/Input/", ignitionCoords, please);
                    RunModels.ConvertToSpecificModel("Farsite", path, please);
                    RunModels.RunModel(models[0], path, please, simNo);
                    Thread.Sleep(500);
                    if (File.Exists($"{path}/Farsite/Median_Outputs/_ArrivalTime.asc"))
                    {
                        File.Copy($"{path}/Farsite/Median_Outputs/_ArrivalTime.asc",
                            $"{path}/ML/Farsite_AT_{simNo}.asc");
                        File.Copy($"{path}/Farsite/Median_Outputs/_SpreadDirection.asc",
                            $"{path}/ML/Farsite_RAZ_{simNo}.asc");
                        File.Copy($"{path}/Farsite/Median_Outputs/_SpreadRate.asc",
                            $"{path}/ML/Farsite_ROS_{simNo}.asc");
                    }
                    Thread.Sleep(1000);
                    simNo++;
                }
                else
                {
                    RunModels.CheckInputsExist(path);
                    simNo++;
                    Console.WriteLine($"Sim no: {simNo}, current convergence: ");
                    for (int i = 0; i < models.Length; i++)
                    {
                        Console.WriteLine($"{models[i]}: {currentError[i]}, {modelsDone[i]}");
                    }

                    int[] ignitionCoords = please.InitialiseIterationAndGetIgnition(path);
                    please.GetFmc(path);
                    please.BurnHours = burnHours[0];
                    RunModels.SetupFarsiteIteration(path + "Farsite/Input/", ignitionCoords, please);

                    string failFlag = "";
                    for (int i = 0; i < models.GetLength(0); i++)
                    {
                        please.BurnHours = burnHours[i];
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        RunModels.ConvertToSpecificModel(models[i], path, please);
                        try
                        {
                            RunModels.RunModel(models[i], path, please, simNo);
                            float[,] ros = RunModels.RetrieveResult(models[i], path, "ROS", please);
                            float[,] azimuth = RunModels.RetrieveResult(models[i], path, "Azimuth", please);
                            //ros = ModelSetup.TransposeMatrix(ros);
                            //azimuth = ModelSetup.TransposeMatrix(azimuth);
                            ModelSetup.Output_ASC_File(header, ros, $"{path}/Log/{models[i]}_ROS_{simNo}.asc");
                            ModelSetup.Output_ASC_File(header, azimuth, $"{path}/Log/{models[i]}_AZ_{simNo}.asc");

                            int[,] temp = peril.CalcSingularBoundary(please.Cellsize, (int)please.ActualAset,
                                please.WindMag, wui, ros,
                                azimuth); //Call k-PERIL to find the boundary of this simulation

                            temp = ModelSetup.TransposeMatrix(temp);
                            for (int j = 0; j < temp.GetLength(0); j++)
                            {
                                for (int k = 0; k < temp.GetLength(1); k++)
                                {
                                    allSafetyBoundaries[i, j, k] += temp[j, k];
                                }
                            }

                            ModelSetup.Output_ASC_File(header, temp,
                                path + $"Outputs/SafetyMatrix_{models[i]}_" + simNo + ".txt");
                            if (simNo > 20)
                            {
                                currentError[i] = please.CalcConvergence(simNo, path, models[i]);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"==========Model {models[i]} failed, skipping.==============");
                            failFlag = "_failed";
                        }

                        ModelSetup.Output_ASC_File(header, ModelSetup.GetSlice(allSafetyBoundaries, i),
                            path + $"Outputs/SafetyMatrix_{models[i]}{failFlag}.txt");
                        if (currentError[i] < 0.01)
                        {
                            consecutiveConvergence[i]++;
                        }
                        else
                        {
                            consecutiveConvergence[i] = 0;
                        }

                        if (consecutiveConvergence[i] >= 20)
                        {
                            modelsDone[i] = true;
                        }

                        failFlag = "";
                        //modelsDone[modelsDone.Length-1] = true;
                        //modelsDone[modelsDone.Length-2] = true;
                        stopwatch.Stop();
                        File.AppendAllText($"{path}/times.txt", $"{stopwatch.Elapsed.TotalMilliseconds} ");
                    }
                    RunModels.LogArrivalTimes(path, simNo);
                    Console.WriteLine($"Iteration finished: {simNo}");
                    //Console.ReadLine();
                    ModelSetup.PrepareNextIteration(path);
                    File.AppendAllText($"{path}/times.txt", Environment.NewLine);
                }
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
                ModelSetup.Output_ASC_File(header, output, path + $"Outputs/SafetyMatrix_{models[i]}.txt");
            }
        }

        static void Main_demo()
        {
            string strWorkPath =
                Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            ModelSetup please = new ModelSetup();
            
            if (Directory.Exists(strWorkPath + "/Output") && Directory.EnumerateFiles(strWorkPath + "/Output").Any())
            {
                Directory.Delete(strWorkPath+ "/Output", true);
            }
            Directory.CreateDirectory(strWorkPath+ "/Output");
            
            float[,] fileInput = please.ParseInputFilesToMemory(strWorkPath + "/Input/variables.txt");
            string[] header = ModelSetup.GetHeader(strWorkPath + "/Input/fuel.asc");
            please.FuelMap = ModelSetup.readASC_int(strWorkPath + "/Input/fuel.asc");

            int[,] wuiIn = new int[(int)fileInput[15, 0], 2];              //pase in the WUI polygon corners
            for (int i = 0; i < fileInput[15, 0]; i++)
            {
                wuiIn[i, 0] = (int)fileInput[16 + 2 * i, 0];
                wuiIn[i, 1] = (int)fileInput[16 + 2 * i + 1, 0];
            }
            ModelSetup.WuiPointsToShapefile(wuiIn, 30, header, [please.FuelMap.GetLength(0),please.FuelMap.GetLength(1)],strWorkPath + "/Input/fuel.prj",strWorkPath + "/Output/WUI.shp");
            int[,] wui = Peril.GetPolygonEdgeNodes(wuiIn);
            
            Peril peril = new Peril();
            
            var fileCount = (from file in Directory.EnumerateFiles(strWorkPath + "/SimResults/", "*.asc", SearchOption.AllDirectories)
                select file).Count();
            int[,] probTb =
                new int[please.FuelMap.GetLength(0), please.FuelMap.GetLength(1)];
            int consecutiveConvergence = 0;
            for (int i = 0; i < fileCount/2; i++)
            {
                var ros = ModelSetup.readASC_float(strWorkPath + "/SimResults/ELMFIRE_ROS_" + i + ".asc");
                var az = ModelSetup.readASC_float(strWorkPath + "/SimResults/ELMFIRE_AZ_" + i + ".asc");
                int[,] temp = peril.CalcSingularBoundary(30, (int)80,
                    15, wui, ros,az); 
                temp = ModelSetup.TransposeMatrix(temp);
                ModelSetup.Output_ASC_File(header, temp,
                    strWorkPath + $"/Output/TriggerBoundary_{i+1}.asc");

                float currentError = 1;
                if (i >= 2)
                {
                    currentError = please.demo_CalcConvergence(i+1, strWorkPath);
                }
                consecutiveConvergence = (currentError<0.01) ? consecutiveConvergence + 1 : 0;

                for (int j = 0; j < temp.GetLength(0); j++)
                {
                    for (int k = 0; k < temp.GetLength(1); k++)
                    {
                        probTb[j, k] += temp[j, k];
                    }
                }
                ModelSetup.Output_ASC_File(header, probTb,
                    strWorkPath + $"/Output/ProbabilisticTriggerBoundary.asc");

                Console.WriteLine($"===========================================");
                Console.WriteLine($"Trigger Boundary {i+1} finished.");
                Console.WriteLine($"===========================================");
                Console.WriteLine($"Current convergence number: {currentError}, goal is 0.01 and below for 20 consecutive iterations");
                Console.WriteLine($"Convergence number under 0.01 for the last {consecutiveConvergence} consecutive iterations.");
                Console.WriteLine($"===========================================");
                if (consecutiveConvergence >= 20)
                {
                    break;
                }
            }
            Console.WriteLine($"===========================================");
            Console.WriteLine($"Probabilistic Trigger Boundary Calculation Finished");
            Console.WriteLine($"Press any key to continue...");
            Console.WriteLine($"===========================================");
            Console.ReadKey();
        }
    }
}
