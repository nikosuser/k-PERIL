using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NetTopologySuite.Noding;
using System.Text;
using OSGeo.GDAL;

namespace RoxCaseGen
{
    public class RunModels
    {
        int[,] _safetyMatrix;

        public static void RunCommand(string[] commands, string exeLocation, string commandJoiner)
        {
            Process process = new Process();
            Console.WriteLine(String.Join(commandJoiner, commands));
            process.StartInfo = new ProcessStartInfo(exeLocation, String.Join("; ", commands))
            {
                UseShellExecute = true,
                CreateNoWindow = false,
                //RedirectStandardOutput = true
            };
            process.Start();
            process.WaitForExit();

            /*
            string script = string.Join(" ; ", commands);

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        $"-NoExit -Command \"Start-Process -FilePath '{exeLocation}' -ArgumentList '{script}' -Wait\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit();

                    // Capture the output and errors
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    // Print results
                    Console.WriteLine("Output:");
                    Console.WriteLine(output);

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("Errors:");
                        Console.WriteLine(error);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            */
        }

        public static void CopySpecificFiles(string sourceFolder, string destinationFolder, string[] fileExtensions)
        {
            // Ensure the source directory exists
            if (!Directory.Exists(sourceFolder))
            {
                throw new DirectoryNotFoundException($"Source folder not found: {sourceFolder}");
            }

            // Ensure the destination directory exists; create it if it doesn't
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            // Get all files in the source folder
            string[] files = Directory.GetFiles(sourceFolder);

            foreach (string file in files)
            {
                // Get the file extension
                string fileExtension = Path.GetExtension(file);

                // Check if the file extension is in the allowed list
                if (fileExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                {
                    // Get the file name
                    string fileName = Path.GetFileName(file);

                    // Construct the destination file path
                    string destinationFile = Path.Combine(destinationFolder, fileName);

                    // Copy the file to the destination folder
                    File.Copy(file, destinationFile, overwrite: true);
                }
            }

            Console.WriteLine(
                $"Files with extensions {string.Join(", ", fileExtensions)} copied from {sourceFolder} to {destinationFolder}");
        }

        static void CheckFileExists(string file)
        {
            try
            {
                if (!File.Exists(file))
                {
                    throw new FileNotFoundException();
                }
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"File {file} was not found during setup.");
            }
        }

        static void InitiateModels()
        {
            //delete outputs from previous iterations



            //delete existing jobs in wise
            System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(@"C:\Users\");

            //start manager and builder

        }

        public static void CheckInputsExist(string path)
        {
            string[] necessaryFiles =
            [
                "/VARS.txt",
                "/FUELTEMPLATE.asc",
                "/Farsite/Input/elevation.asc",
                "/Farsite/Input/slope.asc",
                "/Farsite/Input/aspect.asc",
                "/Farsite/Input/fuel.asc",
                "/Farsite/Input/canopycover.asc",
                "/Farsite/Input/canopybulkdensity.asc",
                "/Farsite/Input/canopybaseheight.asc",
                "/Farsite/Input/canopyheight.asc",
                "/Farsite/Input/landscape.lcp",
                "/Farsite/ROX.txt",
                "/WISE/Input/fuel.asc",
                "/WISE/Input/fuel.prj",
                "/WISE/Input/elevation.asc",
                "/WISE/Input/elevation.prj",
                "/WISE/Input/fbp_lookup_table.lut",
                "/FDS LS1/Input/peril.qgz",
                "/FDS LS1/Input/fuel13.tif",
                "/FDS LS1/Input/dem_big.tif",
                "/FDS LS1/Input/Landfire.gov_F13.csv",
                "/ELMFIRE/Input/adj.tif",
                "/ELMFIRE/Input/asp.tif",
                "/ELMFIRE/Input/cbd.tif",
                "/ELMFIRE/Input/cbh.tif",
                "/ELMFIRE/Input/cc.tif",
                "/ELMFIRE/Input/ch.tif",
                "/ELMFIRE/Input/dem.tif",
                "/ELMFIRE/Input/fbfm40.tif",
                "/ELMFIRE/Input/slp.tif",
                "/ELMFIRE/Input/phi.tif",
                "/ELMFIRE/run.sh",
                "/ELMFIRE/elmfire.data.in",
                "/EPD/fuel.asc"
            ];

            foreach (string file in necessaryFiles)
            {
                CheckFileExists(path + "/Input/" + file);
                File.Copy(path + "Input" + file, path + file, true);
            }
        }

        public static void SetupFarsiteIteration(string path, int[] coordinates, ModelSetup please)
        {
            please.CreateAndWriteFileFarsite(path); //create the FARSITE input file

            CreateShapefile.CreateAndWriteShapefile(coordinates[0], coordinates[1],
                path); //create the shapefile for the ignition point
        }

        public static void ConvertToSpecificModel(string model, string path, ModelSetup please)
        {
            switch (model)
            {
                case "Farsite":
                    break;
                case "WISE":
                    DateTime currTime = please.RawStartTime;
                    string[,] wiseWeather = new string[please.BurnHours+2, 7];
                    wiseWeather[0, 0] = "HOURLY";
                    wiseWeather[0, 1] = "HOUR";
                    wiseWeather[0, 2] = "TEMP";
                    wiseWeather[0, 3] = "RH";
                    wiseWeather[0, 4] = "WD";
                    wiseWeather[0, 5] = "WS";
                    wiseWeather[0, 6] = "PRECIP";
                    for (int i = 1; i < please.BurnHours+2; i++) {
                        wiseWeather[i, 0] =
                            $"{currTime.Day.ToString("D" + 2)}/{currTime.Month.ToString("D" + 2)}/{currTime.Year}";
                        wiseWeather[i, 1] = currTime.Hour.ToString("D" + 2);
                        wiseWeather[i, 2] = please.ActualMaxTemp.ToString();
                        wiseWeather[i, 3] = please.ActualMinHumid.ToString();
                        wiseWeather[i, 4] = please.WindDir.ToString();
                        wiseWeather[i, 5] = please.WindMag.ToString();
                        wiseWeather[i, 6] = 0.ToString();
                        currTime = currTime.AddHours(1);
                    }

                    // Get the number of rows and columns
                    using (StreamWriter writer = new StreamWriter(path + "WISE/Input/weather.txt"))
                    {
                        for (int i = 0; i < wiseWeather.GetLength(0); i++)
                        {
                            string[] row = new string[wiseWeather.GetLength(1)];
                            for (int j = 0; j < wiseWeather.GetLength(1); j++)
                            {
                                row[j] = wiseWeather[i, j];
                            }

                            writer.Write(string.Join(",", row) + "\n");
                        }
                    }

                    //how get elevation of weather station?
                    double[] ignitionCoords = ModelSetup.ConvertCoords((double)please.XIgnitionPrj,
                        (double)please.YIgnitionPrj, 12, true);
                    Console.WriteLine($"{ignitionCoords[0]},{ignitionCoords[1]}");
                    Dictionary<string, string> simulationSetup = new Dictionary<string, string>();

                    simulationSetup.Add("Input Directory", path + "WISE/Input/");
                    simulationSetup.Add("FBP Fuel Map File Name", "fuel.asc");
                    simulationSetup.Add("FBP Fuel Map Lookup Table File Name", "fbp_lookup_table.lut");
                    simulationSetup.Add("Elevation File Name", "elevation.asc");
                    simulationSetup.Add("Elevation Projection File Name", "elevation.prj");
                    simulationSetup.Add("Weather File Name", "weather.txt");
                    simulationSetup.Add($"Ignition Time",
                        $"{please.RawStartTime.ToString("s")}");
                    simulationSetup.Add("Ignition Coords", $"{ignitionCoords[1]} ,{ignitionCoords[0]}");
                    simulationSetup.Add("Simulation End Time",
                        $"{please.RawStartTime.AddHours(please.BurnHours).ToString("s")}");
                    simulationSetup.Add("Weather Station Height",
                        $"{ModelSetup.GetElevation((int)please.XIgnitionRaster, (int)please.YIgnitionRaster, path + "WISE/Input/elevation.asc")}"); //<------------
                    simulationSetup.Add("Weather Station Coords",
                        $"{ignitionCoords[1]} ,{ignitionCoords[0]}"); //<------------
                    simulationSetup.Add("Weather Station Start Date",
                        $"{please.RawStartTime.Year}-{please.RawStartTime.Month.ToString("D" + 2)}-{please.RawStartTime.Day.ToString("D" + 2)}");
                    simulationSetup.Add("Weather Station End Date",
                        $"{please.RawStartTime.AddHours(please.BurnHours+1).Year}-{please.RawStartTime.AddHours(please.BurnHours+1).Month.ToString("D" + 2)}-{please.RawStartTime.AddHours(please.BurnHours+1).Day.ToString("D" + 2)}");

                    using (StreamWriter file = new StreamWriter(path + @"WISE/wise_in.txt"))
                    {
                        foreach (var entry in simulationSetup)
                        {
                            file.WriteLine("{0}", entry.Value);
                        }
                    }

                    break;
                case "ELMFIRE":
                    string gdalDataPath ="C:\\Users\\nikos\\.nuget\\packages\\maxrev.gdal.core\\3.10.0.306\\runtimes\\any\\native\\gdal-data";
                    Environment.SetEnvironmentVariable ("GDAL_DATA", gdalDataPath);
                    Gdal.SetConfigOption ("GDAL_DATA", gdalDataPath);
                    string gdalSharePath = "C:/Users/nikos/.nuget/packages/gdal.native/3.10.0/build/gdal/share/";
                    Environment.SetEnvironmentVariable ("PROJ_LIB", gdalSharePath);
                    Gdal.SetConfigOption("PROJ_LIB", gdalSharePath);
                    Gdal.AllRegister();
                    ModelSetup.CreateMultiBandGeoTiff(ModelSetup.readASC_float($"{path}/Farsite/Median_Outputs/FLAMMAP_FUELMOISTURE1.asc"),1,100,$"{path}/ELMFIRE/input/m1.tif",$"{path}/ELMFIRE/input/dem.tif",DataType.GDT_Float32);
                    ModelSetup.CreateMultiBandGeoTiff(ModelSetup.readASC_float($"{path}/Farsite/Median_Outputs/FLAMMAP_FUELMOISTURE10.asc"),1,100,$"{path}/ELMFIRE/input/m10.tif",$"{path}/ELMFIRE/input/dem.tif",DataType.GDT_Float32);
                    ModelSetup.CreateMultiBandGeoTiff(ModelSetup.readASC_float($"{path}/Farsite/Median_Outputs/FLAMMAP_FUELMOISTURE100.asc"),1,100,$"{path}/ELMFIRE/input/m100.tif",$"{path}/ELMFIRE/input/dem.tif",DataType.GDT_Float32);
                    float[,] matrix = new float[please.FuelMap.GetLength(0), please.FuelMap.GetLength(1)];
                    float[] outputValues = [please.FuelMoisture[3],please.FuelMoisture[4],please.WindMag,please.WindDir];
                    string[] outputNames = ["lh", "lw", "ws", "wd"];
                    for (int i = 0; i < outputValues.GetLength(0); i++)
                    {
                        for (int r = 0; r < please.FuelMap.GetLength(0); r++)
                        {
                            for (int c = 0; c < please.FuelMap.GetLength(1); c++)
                            {
                                matrix[r, c] = outputValues[i];
                            }
                        }
                        ModelSetup.CreateMultiBandGeoTiff(matrix,1,1,$"{path}/ELMFIRE/input/{outputNames[i]}.tif",$"{path}/ELMFIRE/input/dem.tif",DataType.GDT_Float32);
                    }
                    
                    string text = File.ReadAllText(path + @"ELMFIRE/run.sh");
                    text = text.Replace("SIMULATION_TSTOP=32400.0", $"SIMULATION_TSTOP={please.BurnHours * 3600}");
                    text = text.Replace("XIGN=232043.4", $"XIGN={please.XIgnitionPrj}");
                    text = text.Replace("YIGN=4215113.9", $"YIGN={please.YIgnitionPrj}");
                    File.WriteAllText(path + @"ELMFIRE/run.sh", text);

                    ModelSetup.Copy(path + "ELMFIRE/",
                        @"\\wsl.localhost\\Ubuntu-22.04\\home\\nikosuser\\elmfire\\tutorials\\kPERIL\");
                    
                    break;
                case "EPD":
                case "LSTM":
                    currTime = please.RawStartTime;
                    List<string> wxsOutput = new List<string>();
                    List<string> fmsOutput = new List<string>();
                    for (int i = 0; i < please.Fmc.GetLength(0); i++)
                    {
                        fmsOutput.Add($" {please.Fmc[i,0]} {please.Fmc[i,1]} {please.Fmc[i,2]} {please.Fmc[i,3]} {please.Fmc[i,4]} {please.Fmc[i,5]}");
                    }

                    wxsOutput.Add("RAWS_ELEVATION: 10"); //random value added for now. Maybe it is too high?
                    wxsOutput.Add("RAWS_UNITS: Metric"); //All values declared metric
                    wxsOutput.Add(
                        $"RAWS: {(please.BurnHours).ToString()}"); //Calculate and declare how many weather points will follow
                    for (int i = 0; i < please.BurnHours; i++)
                    {
                            wxsOutput.Add($"{currTime.Year.ToString("D")} {currTime.Month.ToString("D")} {currTime.Day.ToString("D")} {(currTime.Hour*100).ToString("D")} {(int)please.ActualMaxTemp} {(int)please.ActualMinHumid} 0.00 {(int)please.WindMag} {please.WindDir} 0");
                            currTime = currTime.AddHours(1);
                    }

                    File.WriteAllLines(path + "/" + model + "/Input/moisture.fms", fmsOutput.ToArray());
                    File.WriteAllLines(path + "/" + model + "/Input/weather.wxs", wxsOutput.ToArray());

                    string sourceFolder = path + "/Farsite/Input/";
                    string destinationFolder = path + "/" + model + "/Input/";

                    string[] fileExtensions = { ".asc", ".fms", ".wxs", ".shp" };

                    try
                    {
                        CopySpecificFiles(sourceFolder, destinationFolder, fileExtensions);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }

                    break;
                case "FDS LS1":
                case "FDS LS4":
                    List<string> fdsWeather = new List<string>();
                    fdsWeather.Add("times,speed,direction");
                    for (int i = 0; i < please.BurnHours; i++)
                    {
                        fdsWeather.Add($"{i * 3600},{please.WindMag / 3.6},{please.WindDir}");
                    }

                    File.WriteAllLines(path + "/" + model + "/Input/weather.csv", fdsWeather.ToArray());

                    string addCrStoPointCommand =
                        @$" run native:reprojectlayer --distance_units=meters --area_units=m2 --ellipsoid=EPSG:7043 --INPUT='{path}/Farsite/Input/ROX.shp' --TARGET_CRS='EPSG:26712' --CONVERT_CURVED_GEOMETRIES=false --OPERATION= --OUTPUT='{path}/{model}/Input/ignition.shp'";
                    string qgisProcessorPromptPath = @"C:\Program Files\QGIS 3.34.14\bin/";
                    string[] commandsPoint = new string[]
                    {
                        $"cd '{qgisProcessorPromptPath}'",
                        $@".\qgis_process-qgis-ltr.bat {addCrStoPointCommand}"
                    };
                    RunCommand(commandsPoint, @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");

                    string inputPath = $"{path}/{model}/Input/";
                    string qgisCommand = 
                        @$"--PROJECT_PATH='{path}/FDS LS1/Input/peril.qgz' " +
                        @$"--distance_units=meters --area_units=m2 " +
                        @$"--ellipsoid=EPSG:7043 " +
                        @$"--chid='mati' " +
                        @$"--fds_path='{inputPath}' " +
                        @$"--extent_layer='{path}\Input\FDS LS1\Input\fuelExtent.shp' " +
                        @$"--pixel_size=200 " +
                        @$"--dem_layer='{path}/FDS LS1/Input/dem_big.tif' " +
                        @$"--landuse_layer='{path}/FDS LS1/Input/fuel13.tif' " +
                        @$"--landuse_type_filepath='{path}\Input\FDS LS1\Input\Landfire.gov_F13.csv' " +
                        @$"--fire_layer='{path}/FDS LS1/Input/ignition.shp' " +
                        @$"--wind_filepath='{path}/FDS LS1/Input/weather.csv' " +
                        @$"--tex_pixel_size={please.Cellsize} " +
                        @$"--tex_layer='{path}/FDS LS1/Input/fuel13.tif' " +
                        @$"--nmesh=35 " +
                        @$"--cell_size={please.Cellsize} " +
                        @$"--t_begin=0 " +
                        @$"--t_end={please.BurnHours*3600} " +
                        @$"--text_filepath= " +
                        $@"--UtmGrid=TEMPORARY_OUTPUT " + 
                        @$"--export_obst=true";

                    string[] commands = new string[]
                    {
                        "C:",
                        $"cd '{qgisProcessorPromptPath}'",
                        $@".\qgis_process-qgis-ltr.bat run 'NIST FDS:Export FDS case' {qgisCommand}"
                    };
                    Console.WriteLine(commands);
                    int lsmode = (model == "FDS LS1") ? 1 : 4;

                    RunCommand(commands, @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    string filePath = $"{path}/{model}/Input/mati.fds"; // Replace with the actual path to your file

                    string fdstext = File.ReadAllText(filePath);
                    fdstext = fdstext.Replace("_REAC ID='Wood' SOOT_YIELD=0.005 O=2.5 C=3.4 H=6.2",
                        $"&REAC ID='Wood' SOOT_YIELD=0.005 O=2.5 C=3.4 H=6.2");
                    fdstext = fdstext.Replace("      LEVEL_SET_MODE=1 ", $"      LEVEL_SET_MODE={lsmode} ");
                    fdstext = fdstext.Replace("&WIND SPEED=1., RAMP_SPEED_T='ws', RAMP_DIRECTION_T='wd' /", $"&WIND SPEED=1., RAMP_SPEED='ws', RAMP_DIRECTION='wd' /");
                    fdstext = fdstext.Replace("&TIME T_END=0. /", $"&TIME T_END={please.BurnHours * 60} /");
                    for (int i = 0; i < please.FmcA13.GetLength(0); i++)
                    {
                        fdstext = fdstext.Replace($"VEG_LSET_FUEL_INDEX={please.FmcA13[i,0].ToString()} /",
                            $"VEG_LSET_FUEL_INDEX={please.FmcA13[i,0].ToString()}, VEG_LSET_M1={(please.FmcA13[i,1]/100f).ToString("F")}, VEG_LSET_M10={(please.FmcA13[i,2]/100f).ToString("F")}, VEG_LSET_M100={(please.FmcA13[i,3]/100f).ToString("F")}, VEG_LSET_MLW={(please.FmcA13[i,4] / 100f).ToString("F")}, VEG_LSET_MLH={(please.FmcA13[i,5] / 100f).ToString("F")} /");
                    }

                    File.WriteAllText(filePath, fdstext);
                    //yes I am doing double work with this, but I cannot be bothered to change the above.
                    string[] lines = File.ReadAllLines(filePath);

                    int targetIndex = Array.FindIndex(lines,
                        line => line.Contains("&SLCF PBY=0.00 QUANTITY='TEMPERATURE' VECTOR=T /"));

                    if (targetIndex != -1)
                    {
                        var updatedLines = lines.Take(targetIndex + 1)
                            .Concat(new[] { "&SLCF AGL_SLICE=5. QUANTITY='TIME OF ARRIVAL' /" })
                            .Concat(lines.Skip(targetIndex + 1))
                            .ToArray();

                        // Write the updated lines back to the file
                        File.WriteAllLines(filePath, updatedLines);
                    }
                    else
                    {
                        Console.WriteLine("Target line not found!");
                    }

                    targetIndex = Array.FindIndex(lines,
                        line => line.Contains(" 4: Wind and fire fully-coupled."));

                    if (targetIndex != -1)
                    {
                        var updatedLines = lines.Take(targetIndex + 1)
                            .Concat(new[] { "&RADI RADIATION=F/" })
                            .Concat(lines.Skip(targetIndex + 1))
                            .ToArray();

                        // Write the updated lines back to the file
                        File.WriteAllLines(filePath, updatedLines);
                    }
                    else
                    {
                        Console.WriteLine("Target line not found!");
                    }
                    Thread.Sleep(1000);
                    break;
            }
        }

        public static void RunModel(string model, string path, ModelSetup modelSetup, int simNo)
        {
            Console.WriteLine($"Running Model: {model}");
            switch (model)
            {
                case "Farsite":
                    RunCommand([$"cd '{path}Farsite'", $"./setenv.bat", "./bin/testFARSITE.exe ROX.txt"],
                        @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    break;
                case "WISE":
                    string gdalDataPath ="C:\\Program Files\\Prometheus\\gdal-data\\";
                    Environment.SetEnvironmentVariable ("GDAL_DATA", gdalDataPath);
                    Gdal.SetConfigOption ("GDAL_DATA", gdalDataPath);
                    string gdalSharePath = "C:\\Program Files\\Prometheus\\proj_nad\\";
                    Environment.SetEnvironmentVariable ("PROJ_LIB", gdalSharePath);
                    Gdal.SetConfigOption("PROJ_LIB", gdalSharePath);
                    Gdal.AllRegister();
                    Console.WriteLine("Starting WISE Manager");
                    ProcessStartInfo startInfoManager = new ProcessStartInfo(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", @"cd C:\WISE_Manager-0.6.beta.5; java -jar WISE_Manager_Ui.jar")
                    {
                        UseShellExecute = false, // Allows redirection
                        RedirectStandardOutput = true, // Manage output
                        RedirectStandardError = true,
                        CreateNoWindow = true // Prevents the window from appearing
                    };
                    Process procBuilderManager = Process.Start(startInfoManager);
                    
                    Thread.Sleep(15000);
/*
                    //Process builder = startBuilder();
                    Console.WriteLine("Starting WISE Builder");
                    ProcessStartInfo startInfo = new ProcessStartInfo(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", @"cd C:\WISE_Builder-1.0.6-beta.5; java -jar WISE_Builder.jar -s -j C:\jobs")
                    {
                        UseShellExecute = false, // Allows redirection
                        RedirectStandardOutput = true, // Manage output
                        RedirectStandardError = true,
                        CreateNoWindow = true // Prevents the window from appearing
                    };
                    Process procBuilder = Process.Start(startInfo);
                    
                    Thread.Sleep(8000);
                    */
                    string[] wiseResults =
                        Directory.GetDirectories(@"C:\\jobs", "job_*", SearchOption.TopDirectoryOnly);
                    if (wiseResults.Length > 0)
                    {
                        for (int i = 0; i < wiseResults.Length; i++)
                        {
                            Directory.Delete(wiseResults[i], true);
                        }
                    }

                    RunCommand(
                    [
                        $@"cd '{Environment.GetEnvironmentVariable("WISEPREPROCPATH")}'",
                        $@"& './WISE Preprocessor.exe' '{path}\WISE\wise_in.txt'"
                    ], @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    
                    wiseResults =
                        Directory.GetDirectories(@"C:\\jobs", "job_*", SearchOption.TopDirectoryOnly);
                    while (wiseResults.Length == 0)
                    {
                        wiseResults =
                            Directory.GetDirectories(@"C:\\jobs", "job_*", SearchOption.TopDirectoryOnly);
                        Thread.Sleep(1000);
                    }
                    RunCommand(["cd 'C:/Program Files/WISE/'",@$".\wise.exe -t '{wiseResults[0]}\job.fgmj'"],@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    
                    Console.WriteLine("Killing WISE Builder");
                    //procBuilder.Kill();

                    break;
                case "ELMFIRE":
                    RunCommand(
                    [
                        @"cd \\wsl.localhost\Ubuntu-22.04\home\nikosuser\elmfire\tutorials\kPERIL",
                        "bash ./run.sh"
                    ], @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    break;
                case "EPD":
                case "LSTM":
                    /*
                    string anacondaPromptPath =
                        @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
                    string[] commands = new string[]
                    {
                        @"C:\\Users\\nikos\\miniconda3\\Scripts\\activate.bat",
                        "conda activate newEnv",
                        $@"python D:\\GoogleModel\\wildfire_conv_ltsm\\preprocessor.py california {model} '{path}/EPD/Input' {modelSetup.burnDuration} {modelSetup.xIgnition_raster} {modelSetup.yIgnition_raster} 2022 09 {modelSetup.totalRAWSdays - modelSetup.burnDuration/24} {2300 - modelSetup.burnDuration%24}",
                        "pause",
                        "exit"
                    };
                    runCommand(commands, anacondaPromptPath);
                    */
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            RedirectStandardInput = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            WorkingDirectory = path + @"\" + model,
                        }
                    };
                    process.Start();
                    // Pass multiple commands to cmd.exe
                    using (var sw = process.StandardInput)
                    {
                        if (sw.BaseStream.CanWrite)
                        {
                            sw.WriteLine(@"powershell -ExecutionPolicy ByPass -NoExit -Command ""& 'C:\Users\nikos\miniconda3\shell\condabin\conda-hook.ps1'""");
                            sw.WriteLine("conda activate newEnv");
                            sw.WriteLine(
                                $"python C:/GoogleModels/preprocessor.py california {model} \"{path}/{model}/Input\" {modelSetup.BurnHours} {modelSetup.XIgnitionRaster} {modelSetup.YIgnitionRaster} {modelSetup.RawStartTime.Year} {modelSetup.RawStartTime.Month} {modelSetup.RawStartTime.Day} {modelSetup.RawStartTime.Hour}00");
                            sw.WriteLine("exit");
                        }
                    }

                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = process.StandardOutput.ReadLine();
                        Console.WriteLine(line);
                    }

                    break;
                case "FDS LS1":
                case "FDS LS4":
                    //runCommand([$"\"C:/Program Files/firemodels/FDS6/bin/fdsinit.bat\"","D:",$"cd \"{path}{model}/Input/\"","mpiexec -n 12 fds mati.fds"],@"C:\WINDOWS\system32\cmd.exe"," && ");
                    
                    //running it now is way too slow (10M cells). Save it in a folder and yeet it to the hpc later. 
                    string copyFolder = path + model + "/Iter" + simNo.ToString("000");
                    if (Directory.Exists(copyFolder))
                    {
                        Directory.Delete(copyFolder, true);
                    }
                    Directory.CreateDirectory(copyFolder);
                    File.Copy($@"{path}{model}/Input/mati.fds", copyFolder+"/mati.fds", true);
                    if (File.Exists($"{path}{model}/Input/mati_tex.png"))
                    {
                        File.Copy($"{path}{model}/Input/mati_tex.png", copyFolder + "/mati_tex.png", true);
                    }

                    // Path to the text file
                    string fdsfilePath = $@"{path}{model}/Input/mati.fds";

                    // Target string pattern to find
                    string targetPattern = @"(\d+)\s·\s(\d+)\smeshes\s";

                    int coreNo = 0;

                    try
                    {
                        // Read all lines of the file
                        string[] lines = File.ReadAllLines(fdsfilePath);

                        foreach (string line in lines)
                        {
                            if (line.Contains("meshes"))
                            {
                                // Match the pattern to extract the two numbers
                                Match match = Regex.Match(line, targetPattern);

                                if (match.Success)
                                {
                                    // Extract the first two numbers
                                    int num1 = int.Parse(match.Groups[1].Value);
                                    int num2 = int.Parse(match.Groups[2].Value);

                                    // Calculate the multiplication
                                    coreNo = num1 * num2;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                    
                    
                    string scriptContent = @$"#!/bin/bash
#PBS -lwalltime=01:00:00
#PBS -lselect=1:ncpus={coreNo}:mem=32gb
#PBS -o output.txt
#PBS -e error.txt

cd $PBS_O_WORKDIR

module load mpi
module load fds

mpirun fds $HOME/peril/Iter{simNo.ToString("000")}/mati.fds";

                    // File path
                    string filePath = $"{copyFolder}/mati{simNo.ToString("000")}.sh";

                    // Write the file with LF line endings
                    File.WriteAllText(filePath, scriptContent.Replace("\r\n", "\n"));

                    scriptContent = $"qsub mati{simNo.ToString("000")}.sh";
                    filePath = $"{copyFolder}/run.sh";
                    File.WriteAllText(filePath, scriptContent.Replace("\r\n", "\n"));
                    break;
            }
        }

        public static float[,] RetrieveResult(string model, string path, string outputKind, ModelSetup please)
        {
            float[,] output = new float[please.FuelMap.GetLength(0), please.FuelMap.GetLength(1)];
            string file = "";
            switch (model)
            {
                case "Farsite":
                    if (outputKind == "ROS")
                    {
                        file = path + @"Farsite/Median_Outputs/_SpreadRate.asc";
                    }
                    else if (outputKind == "Azimuth")
                    {
                        file = path + @"Farsite/Median_Outputs/_SpreadDirection.asc";
                    }

                    output = ModelSetup.readASC_float(file);
                    break;
                case "WISE":
                    if (outputKind == "ROS")
                    {
                        file = "ROS";
                    }
                    else if (outputKind == "Azimuth")
                    {
                        file = "RAZ";
                    }

                    Thread.Sleep(2000);
                    // Define the base directory and target subfolder
                    string baseDirectory = @"C:\jobs";
                    string targetSubfolder = @"Outputs\scen0";
                    string fileName = file + ".tif";
                    string destinationPath =
                        @$"{path}\WISE\Output\" + file +
                        ".tif";

                    var jobDirectories =
                        Directory.GetDirectories(baseDirectory, "job_*", SearchOption.TopDirectoryOnly);

                    foreach (var jobDir in jobDirectories)
                    {
                        // Construct the full path to the expected file
                        string filePath = Path.Combine(jobDir, targetSubfolder, fileName);

                        while (!File.Exists(filePath))
                        {
                        }

                        // File found, copy it to the destination
                        Thread.Sleep(2000);
                        File.Copy(filePath, destinationPath, true); // true to overwrite if it already exists
                        if (!File.Exists(path+"WISE/Output/AT.tif"))
                        {
                            File.Copy(jobDir + "\\" + targetSubfolder + "\\" + "AT.tif", path+"WISE/Output/AT.tif", true);
                        }
                    }
                    output = ModelSetup.ReadTiff(destinationPath);
                    break;
                case "ELMFIRE":
                    string sourceFolder =
                        @"\\wsl.localhost\Ubuntu-22.04\home\nikosuser\elmfire\tutorials\kPERIL\outputs/";
                    string destinationFolder =
                        @$"{path}\ELMFIRE\Median_output/";

                    string elMoutputname = outputKind == "ROS" ? "vs" : "time_of_arrival";

                    Regex regex = new Regex(@$"{elMoutputname}_\d+_\d+\.tif");
                    bool found = false;
                    while (!found)
                    {
                        if (Directory.Exists(sourceFolder))
                        {
                            var files = Directory.GetFiles(sourceFolder);
                            foreach (var elMfile in files)
                            {
                                if (regex.IsMatch(Path.GetFileName(elMfile)))
                                {
                                    Thread.Sleep(2000);
                                    string destinationFile = destinationFolder + elMoutputname + ".tif";
                                    Directory.CreateDirectory(destinationFolder); // Ensure destination exists
                                    File.Copy(elMfile, destinationFile, overwrite: true);
                                    found = true;
                                }
                            }

                            Thread.Sleep(1000);
                        }
                    }

                    float[,] outputElmfire = ModelSetup.ReadTiff(Path.Combine(destinationFolder, elMoutputname + ".tif"));

                    if (outputKind == "Azimuth")
                    {
                        Console.WriteLine($"ELMFIRE: Converting Arrival Time to Azimuth ... ");
                        for (int i = 0; i < output.GetLength(0); i++)
                        {
                            for (int j = 0; j < output.GetLength(1); j++)
                            {
                                if (outputElmfire[i, j] < 0)
                                {
                                    output[i, j] = -9999;
                                }
                                else if (i == 0)
                                {
                                    output[i, j] = -9999;
                                }
                                else if (i == output.GetLength(0) - 1)
                                {
                                    output[i, j] = -9999;
                                }
                                else if (j == 0)
                                {
                                    output[i, j] = -9999;
                                }
                                else if (j == output.GetLength(1) - 1)
                                {
                                    output[i, j] = -9999;
                                }
                                else
                                {
                                    var ros = ModelSetup.CalculateGradient(outputElmfire, i, j, please.Cellsize);
                                    output[i, j] = (float)ros.dir;
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < outputElmfire.GetLength(0); i++)
                        {
                            for (int j = 0; j < outputElmfire.GetLength(1); j++)
                            {
                                output[i, j] = outputElmfire[i, j] / 3.281f;
                            }
                        }
                    }
                    break;
                case "EPD":
                case "LSTM":
                    string googlePath = @$"{path}\{model}\Input\";

                    while (!File.Exists(googlePath + model + "_AT_OS.asc"))
                    {
                        Thread.Sleep(500);
                    }

                    float[,] outputEpd = ModelSetup.readASC_float(googlePath + model + "_AT_OS.asc");

                    Console.WriteLine($"Google: Converting Arrival Time to ROS ... ");
                    for (int j = 0; j < output.GetLength(0); j++)
                    {
                        for (int i = 0; i < output.GetLength(1); i++)
                        {
                            if ((int)outputEpd[i,j] == -9999)
                            {
                                output[i, j] = -9999;
                            }
                            else if (i == 0)
                            {
                                output[i, j] = -9999;
                            }
                            else if (i == output.GetLength(0) - 1)
                            {
                                output[i, j] = -9999;
                            }
                            else if (j == 0)
                            {
                                output[i, j] = -9999;
                            }
                            else if (j == output.GetLength(1) - 1)
                            {
                                output[i, j] = -9999;
                            }
                            else
                            {
                                var ros = ModelSetup.CalculateGradient(outputEpd, i, j, please.Cellsize);
                                output[i, j] = (outputKind == "ROS")
                                    ? (float)ros.mag
                                    : (float)ros.dir;
                            }
                        }
                    }
                    break;
                case "FDS LS1":
                case "FDS LS4":
                    /*
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            RedirectStandardInput = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            WorkingDirectory = path + @"\" + model,
                        }
                    };
                    process.Start();
                    // Pass multiple commands to cmd.exe
                    using (var sw = process.StandardInput)
                    {
                        if (sw.BaseStream.CanWrite)
                        {
                            // Vital to activate Anaconda
                            sw.WriteLine(@"C:\\Users\\nikos\\miniconda3\\Scripts\\activate.bat");
                            // Activate your environment
                            sw.WriteLine("conda activate newEnv");
                            // Any other commands you want to run
                            sw.WriteLine(
                                $@"python 'D:\\OneDrive - Imperial College London\\Imperial\\PhD\\FDS2GIS\\findArrivalTime.py' '{path}/{model}/Input/mati.fds/' mati {please.cellsize} {path}/Input/Farsite/Input/elevation");
                            sw.WriteLine("exit");
                        }
                    }

                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = process.StandardOutput.ReadLine();
                        Console.WriteLine(line);
                    }
                    */
                    break;
            }
            return output;
        }

        public static Process StartBuilder()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WorkingDirectory = @"C:\WISE_Builder-1.0.6-beta.5",
                }
            };
            process.Start();
            // Pass multiple commands to cmd.exe
            using (var sw = process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    sw.WriteLine(@"cd C:\WISE_Builder-1.0.6-beta.5; java -jar WISE_Builder.jar -s -j C:\jobs");
                }
            }
            return process;
        }
        public static void LogArrivalTimes(string path, int simNo)
        {
            string[] necessaryFiles =
            [
                "/Farsite/Median_Outputs/_ArrivalTime.asc",
                "/Farsite/Median_Outputs/_ArrivalTime.prj",
                "/WISE/Output/AT.tif",
                "/ELMFIRE/Median_output/time_of_arrival.tif",
                "/EPD/Input/EPD_AT_OS.asc",
                "/LSTM/Input/LSTM_AT_OS.asc"
            ];

            foreach (string file in necessaryFiles)
            {
                // Remove any leading slash before splitting
                string[] parts = path.TrimStart('/').Split('/');
                // The first part should be the root folder
                string rootFolder = parts.Length > 0 ? parts[0] : string.Empty;
                if (File.Exists(path + "/" + file))
                    File.Copy(path + "/" + file, $"{path}/Log/{simNo.ToString("000")}_{Path.GetFileName(file)}", true);
            }
        }
    }
}